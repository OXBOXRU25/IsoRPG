using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Считает посадку оружия в руке ПО КОСТЯМ КИСТИ, а не по числам из Blender.
    ///
    /// История вопроса. Числа примерки приезжали из соседнего чата — сперва
    /// углами Эйлера, потом матрицей, — и оба раза кинжал ложился мимо. Причин
    /// оказалось три, и ни одну нельзя было увидеть в Blender:
    ///
    ///   1. **мерили от `hand_r`, а вешается на `prop_r`.** У скелета Sidekick
    ///      есть отдельная кость-держатель, смещённая на 7.5 см вперёд от
    ///      кисти, и `WeaponVisual` находит её первой. Все довороты применялись
    ///      к другой кости — отсюда «кинжал проходит сквозь начало кисти»;
    ///   2. **кисти у Sidekick не зеркальны** — щуп намерил расхождение
    ///      отражённой правой и левой в 204° по трём осям. Отражение, которым
    ///      мы получали левую руку, врало по построению;
    ///   3. **числа Blender сняты на конвертированном скелете** (`fbx2gltf`),
    ///      а не на том, что в движке.
    ///
    /// Поэтому считаем прямо здесь, по костям, и для каждой руки по её
    /// собственным — не отражением. Всё выводится, ничего не подбирается:
    ///
    ///   - **ось рукояти** — линия оснований пальцев: у сжатого кулака рукоять
    ///     лежит вдоль неё. Направление клинка — от мизинца к указательному и
    ///     дальше, наружу;
    ///   - **точка хвата** — середина этой линии, сдвинутая к кончикам пальцев
    ///     на толщину рукояти: рукоять лежит в ложбине между согнутыми
    ///     пальцами и ладонью, а не на самих косточках;
    ///   - **разворот лезвия** — плашмя к ладони: нормаль к плоскости
    ///     «рукоять × пальцы».
    ///
    /// Что НЕ решается отсюда: пальцы. Их держит анимация, и в безоружной
    /// стойке Synty ладонь раскрыта — рукоять она не обхватит, как её ни
    /// клади. Это отдельная задача (слой с маской на кисть либо вооружённый
    /// набор стоек ExplosiveLLC).
    /// </summary>
    public static class GripFit
    {
        /// <summary>Кинжал, который примеряли и утвердили. НЕ _01 — тот был в первом ряду сравнения.</summary>
        public const string DaggerPath =
            "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Wep_Dagger_02.prefab";

        private const string Hero = "Human-Custom2";
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        /// <summary>Кости-держатели по порядку поиска — тот же список, что в WeaponVisual.</summary>
        private static readonly string[] RightSlotBones = { "handslot.r", "prop_r", "hand_r" };
        private static readonly string[] LeftSlotBones = { "handslot.l", "prop_l", "hand_l" };

        /// <summary>
        /// На сколько отодвинуть рукоять от косточек к кончикам пальцев, метры.
        ///
        /// Половина толщины рукояти: она лежит в ложбине сжатого кулака, а не
        /// на самих суставах. Единственное подбираемое число во всём расчёте.
        /// </summary>
        private const float HandleLift = 0.012f;

        /// <summary>Посчитанное. Читает щуп, чтобы поставить это серединой ряда.</summary>
        public static Vector3 Grip { get; private set; } = new Vector3(-0.0904f, 0.0060f, 0.0259f);
        public static Vector3 Fitted { get; private set; } = new Vector3(6.47f, 93.00f, 178.34f);
        public static Vector3 GripLeft { get; private set; }
        public static Vector3 FittedLeft { get; private set; }

        [MenuItem("Tools/IsoRPG/Оружие: пересчитать хват по костям", priority = 46)]
        public static void Apply()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaggerPath);
            var heroPrefab = FindPrefab(Hero);

            if (prefab == null || heroPrefab == null)
            {
                Debug.LogError("[IsoRPG] Нет кинжала или героя для расчёта хвата.");
                return;
            }

            if (!ModelAxes(prefab, out var bladeLocal, out var guardLocal, out var size))
            {
                Debug.LogError("[IsoRPG] У кинжала не нашлось меша — оси не померить.");
                return;
            }

            var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);

            bool okRight = Fit(hero, "r", RightSlotBones, bladeLocal, guardLocal,
                               out var gripR, out var anglesR, out string logR);

            bool okLeft = Fit(hero, "l", LeftSlotBones, bladeLocal, guardLocal,
                              out var gripL, out var anglesL, out string logL);

            Object.DestroyImmediate(hero);

            if (!okRight)
            {
                Debug.LogError("[IsoRPG] Правая рука не посчиталась — оставляю прежние числа.");
                return;
            }

            Grip = gripR;
            Fitted = anglesR;

            if (okLeft) { GripLeft = gripL; FittedLeft = anglesL; }
            else { GripLeft = gripR; FittedLeft = anglesR; }

            Debug.Log($"[IsoRPG] Хват посчитан по костям кисти.\n" +
                      $"  кинжал: {System.IO.Path.GetFileNameWithoutExtension(DaggerPath)}, " +
                      $"габарит {size.x:0.000} x {size.y:0.000} x {size.z:0.000} м, " +
                      $"клинок в модели {bladeLocal}, гарда {guardLocal}\n" +
                      logR + "\n" + logL + "\n" +
                      $"  ПРАВАЯ: смещение {Grip.x:0.0000} / {Grip.y:0.0000} / {Grip.z:0.0000}, " +
                      $"доворот {Fitted.x:0.0} / {Fitted.y:0.0} / {Fitted.z:0.0}\n" +
                      $"  ЛЕВАЯ:  смещение {GripLeft.x:0.0000} / {GripLeft.y:0.0000} / {GripLeft.z:0.0000}, " +
                      $"доворот {FittedLeft.x:0.0} / {FittedLeft.y:0.0} / {FittedLeft.z:0.0}");

            ApplyToScene();
            ApplyToItems(prefab);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Посадка для одной руки: где рукоять и как повёрнут клинок.
        ///
        /// Считается в системе кости-держателя — той самой, куда оружие потом
        /// и вложится. Кости пальцев при этом берутся у кисти: держатель у
        /// Sidekick сидит внутри неё.
        /// </summary>
        private static bool Fit(GameObject hero, string side, string[] slotNames,
                                Vector3 bladeLocal, Vector3 guardLocal,
                                out Vector3 grip, out Vector3 angles, out string log)
        {
            grip = Vector3.zero;
            angles = Vector3.zero;
            log = "  " + side + ": не посчиталось";

            var all = hero.GetComponentsInChildren<Transform>(true);

            var slot = slotNames.Select(n => all.FirstOrDefault(t => t.name == n))
                                .FirstOrDefault(t => t != null);

            var index = all.FirstOrDefault(t => t.name == "index_01_" + side);
            var pinky = all.FirstOrDefault(t => t.name == "pinky_01_" + side);
            var middle = all.FirstOrDefault(t => t.name == "middle_01_" + side);
            var ring = all.FirstOrDefault(t => t.name == "ring_01_" + side);
            var tip = all.FirstOrDefault(t => t.name == "index_03_" + side);

            if (slot == null || index == null || pinky == null || middle == null ||
                ring == null || tip == null)
            {
                return false;
            }

            // --- ось рукояти: линия оснований пальцев -----------------------
            //
            // От мизинца к указательному: клинок выходит из кулака со стороны
            // указательного, это и есть прямой хват.
            var blade = (index.position - pinky.position).normalized;

            // --- куда смотрят пальцы ----------------------------------------
            var fingers = (tip.position - index.position).normalized;

            // --- плашмя к ладони ---------------------------------------------
            //
            // Гарда стоит поперёк клинка в плоскости лезвия, а лезвие лежит
            // плашмя вдоль ладони — значит гарда смотрит туда же, куда пальцы.
            var guard = Vector3.ProjectOnPlane(fingers, blade).normalized;

            if (guard.sqrMagnitude < 0.01f) return false;

            // --- точка хвата -------------------------------------------------
            //
            // Середина линии оснований, отодвинутая к кончикам на толщину
            // рукояти: она лежит в ложбине сжатого кулака, а не на суставах.
            var knuckles = (index.position + middle.position + ring.position + pinky.position) * 0.25f;
            var handle = knuckles + fingers * HandleLift;

            // --- в систему кости-держателя -----------------------------------
            grip = slot.InverseTransformPoint(handle);

            var bladeInSlot = slot.InverseTransformDirection(blade);
            var guardInSlot = slot.InverseTransformDirection(guard);

            var from = Quaternion.LookRotation(bladeLocal, guardLocal);
            var to = Quaternion.LookRotation(bladeInSlot, guardInSlot);

            angles = (to * Quaternion.Inverse(from)).eulerAngles;

            log = $"  {side}: держатель «{slot.name}», косточки {knuckles - slot.position}, " +
                  $"клинок в держателе {bladeInSlot}, гарда {guardInSlot}";

            return true;
        }

        /// <summary>
        /// Поставить числа герою в сцене.
        ///
        /// Обязательно: значение, заданное и в коде, и в сцене, работает ИЗ
        /// СЦЕНЫ — правка кода не догоняет уже расставленный компонент.
        /// </summary>
        private static void ApplyToScene()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int done = 0;

            foreach (var visual in Object.FindObjectsByType<IsoRPG.Items.WeaponVisual>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                visual.SetGrip(Grip, Fitted, GripLeft, FittedLeft);
                EditorUtility.SetDirty(visual);
                done++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Хват проставлен в сцене: компонентов {done}.");
        }

        /// <summary>
        /// Перевести кинжалы в каталоге на утверждённую модель.
        ///
        /// В каталоге стоял `SM_Prop_Dagger_01` из набора персонажей, а
        /// `ItemsBuilder` по умолчанию отдаёт `SM_Wep_Dagger_01` из набора
        /// оружия. Примеряли и утверждали `_02`. Три модели с разной опорой —
        /// под чужую числа не сядут, сколько ни крути.
        /// </summary>
        private static void ApplyToItems(GameObject dagger)
        {
            int changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(IsoRPG.Items.ItemDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<IsoRPG.Items.ItemDefinition>(path);

                if (item == null || item.worldModel == null) continue;

                // По слову «Dagger» в имени модели, а не по одному известному
                // имени: третье имя мы так и не замечали.
                if (!item.worldModel.name.Contains("Dagger")) continue;
                if (item.worldModel == dagger) continue;

                Debug.Log($"[IsoRPG] {item.name}: модель {item.worldModel.name} → {dagger.name}");

                item.worldModel = dagger;
                EditorUtility.SetDirty(item);
                changed++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Кинжалов переведено на утверждённую модель: {changed}.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Оси модели по её же мешу: самый длинный габарит — клинок, второй —
        /// гарда. Знак — в какую сторону от опоры меш уходит дальше.
        /// </summary>
        private static bool ModelAxes(GameObject prefab, out Vector3 blade, out Vector3 guard,
                                      out Vector3 size)
        {
            blade = Vector3.forward;
            guard = Vector3.up;
            size = Vector3.zero;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true)
                                .Where(f => f.sharedMesh != null)
                                .ToArray();

            if (filters.Length == 0) return false;

            bool started = false;
            var box = new Bounds();

            foreach (var filter in filters)
            {
                var local = filter.sharedMesh.bounds;

                var toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var centre = toRoot.MultiplyPoint3x4(local.center);
                var extent = toRoot.MultiplyVector(local.extents);

                var part = new Bounds(centre, new Vector3(
                    Mathf.Abs(extent.x) * 2f, Mathf.Abs(extent.y) * 2f, Mathf.Abs(extent.z) * 2f));

                if (!started) { box = part; started = true; }
                else box.Encapsulate(part);
            }

            size = box.size;

            int longest = Longest(size, -1);
            int second = Longest(size, longest);

            blade = Axis(longest, Sign(box, longest));
            guard = Axis(second, Sign(box, second));

            return true;
        }

        private static int Longest(Vector3 size, int skip)
        {
            int best = -1;
            float value = -1f;

            for (int i = 0; i < 3; i++)
            {
                if (i == skip) continue;
                if (size[i] <= value) continue;

                value = size[i];
                best = i;
            }

            return best;
        }

        private static float Sign(Bounds box, int axis) =>
            Mathf.Abs(box.max[axis]) >= Mathf.Abs(box.min[axis]) ? 1f : -1f;

        private static Vector3 Axis(int index, float sign)
        {
            var v = Vector3.zero;
            v[index] = sign;
            return v;
        }

        private static GameObject FindPrefab(string prefabName)
        {
            var guid = AssetDatabase.FindAssets(prefabName + " t:Prefab").FirstOrDefault();

            return guid == null
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
