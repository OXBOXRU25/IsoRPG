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
    ///   - **точка хвата** — середина между основанием пальца и его кончиком,
    ///     усреднённая по четырём пальцам, В СЖАТОМ КУЛАКЕ. Кончики там
    ///     возвращаются к ладони, и эта середина попадает ровно внутрь
    ///     захвата;
    ///   - **разворот лезвия** — плашмя к ладони: гарда вдоль направления
    ///     пальцев.
    ///
    /// Мерить обязательно в сжатом кулаке. Первый заход мерил по раскрытой
    /// ладони и добавлял догаданные 12 мм — Павлон сразу увидел итог:
    /// «рукоять выходит за внешние пределы руки». У раскрытой ладони захвата
    /// нет вовсе, и добавлять к ней нечего.
    ///
    /// Что НЕ решается отсюда: сами пальцы. Их держит анимация — этим
    /// занимается задание `hand-pose`, слой с маской на кисть.
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
        /// Клип, в котором кисть СЖАТА. Мерить надо в нём.
        ///
        /// Первый заход мерил по раскрытой ладони и добавлял подобранные 12 мм
        /// «на толщину рукояти» — Павлон сразу увидел итог: «рукоять выходит за
        /// внешние пределы руки, она не в сжатой ладони». Так и было: у
        /// раскрытой ладони никакого захвата нет, и добавка была догадкой.
        ///
        /// В сжатом кулаке подбирать нечего: кончики пальцев возвращаются к
        /// ладони, и между основанием и кончиком получается тот самый тоннель,
        /// в котором лежит рукоять. Его середина — и есть точка хвата.
        /// </summary>
        private const string FistClip =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/" +
            "Armed/RPG-Character@Armed-Idle.FBX";

        /// <summary>
        /// Прокрутка вокруг оси клинка, градусы. Правка Павлона по кадру
        /// 02.09.2026: «кинжал лежит плашмя, надо развернуть в правую сторону
        /// примерно наполовину» — то есть на половину прямого угла, из
        /// положения плашмя к ребру. Наклон при этом не меняется: вращение
        /// идёт вокруг самого клинка.
        ///
        /// Вокруг ЛОКАЛЬНОЙ оси Y модели: у кинжалов Synty клинок идёт вдоль
        /// неё, значит это и есть его собственная ось.
        /// </summary>
        private const float Roll = 45f;

        /// <summary>
        /// Знак проворота для левой руки.
        ///
        /// Проворот задаётся в системе самой модели, а базисы рук зеркальны
        /// по смыслу: клинок у правой выходит со стороны указательного вправо,
        /// у левой — влево. Один и тот же угол крутит их в ПРОТИВОПОЛОЖНЫЕ
        /// стороны относительно тела. Павлон 02.09.2026 увидел это сразу:
        /// «только левый кинжал повернулся, надо так же правый».
        /// </summary>
        private const float LeftRollSign = -1f;

        /// <summary>
        /// Доводка глубины сверх расчёта, метры. Ноль — чистый расчёт по
        /// костям. Правится только по кадру от Павлона: «немного сильнее в
        /// глубь ладони».
        /// </summary>
        private const float DepthNudge = 0.015f;

        /// <summary>
        /// Подъём по ВЕРТИКАЛИ, метры.
        ///
        /// Отдельно от глубины, потому что это другая ось. Павлон 03.09.2026:
        /// «кинжал надо поднять вертикально вверх, не меняя угол и поворот» —
        /// а я до этого двигал вдоль пальцев, в глубь ладони, и подъёма он не
        /// видел вовсе. Обе доводки нужны: глубина сажает рукоять в кулак,
        /// вертикаль убирает пальцы, проходящие сквозь неё.
        ///
        /// Вертикаль берётся мировая и переводится в систему держателя в позе
        /// замера: в кости своя ось «вверх» не совпадает с мировой, и сдвиг по
        /// локальной оси дал бы наклонный подъём.
        /// </summary>
        private const float LiftUp = 0.018f;

        /// <summary>
        /// Левую руку получать ОТРАЖЕНИЕМ правой, а не считать по её костям.
        ///
        /// Расчёт по своим костям честнее, но даёт видимую разницу: считаем мы
        /// в вооружённой стойке, а в ней руки согнуты по-разному, и точка
        /// хвата выходит не та же. Павлон 02.09.2026: «вторую руку сделать зеркально
        /// положение кинжала и угол наклона». Симметрия тут важнее точности:
        /// две руки одного человека игрок сравнивает между собой, а не с
        /// анатомией.
        ///
        /// Отражение: у точки меняет знак X, у кватерниона — знаки y и z.
        ///
        /// ОТКЛЮЧЕНО тем же вечером: узнав, что руки в стойке согнуты
        /// по-разному, Павлон снял требование — «не надо тогда симметрию, я
        /// не знал про руки». Расчёт по своим костям вернулся. Флаг оставлен:
        /// он и есть запись о том, что этот путь пробовали и почему ушли.
        /// </summary>
        private const bool MirrorLeft = false;

        /// <summary>Посчитанное. Читает щуп, чтобы поставить это серединой ряда.</summary>
        public static Vector3 Grip { get; private set; } = new Vector3(-0.0904f, 0.0060f, 0.0259f);
        public static Vector3 Fitted { get; private set; } = new Vector3(6.47f, 93.00f, 178.34f);
        public static Vector3 GripLeft { get; private set; }
        public static Vector3 FittedLeft { get; private set; }

        /// <summary>Куда «глубже в ладонь»: направление пальцев в системе держателя. Нужно щупу.</summary>
        public static Vector3 DepthRight { get; private set; } = Vector3.forward;

        public static Vector3 DepthLeft { get; private set; } = Vector3.forward;

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

            // Сжимаем кулак ДО замера: в раскрытой ладони мерить нечего.
            var fist = AssetDatabase.LoadAllAssetsAtPath(FistClip)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (fist != null)
            {
                var keep = hero.transform.position;
                fist.SampleAnimation(hero, 0f);
                hero.transform.position = keep;
            }
            else
            {
                Debug.LogWarning("[IsoRPG] Клипа со сжатой кистью нет — замер пойдёт " +
                                 "по раскрытой ладони и снова соврёт.");
            }

            bool okRight = Fit(hero, "r", RightSlotBones, bladeLocal, guardLocal, Roll,
                               out var gripR, out var anglesR, out var depthR, out string logR);

            bool okLeft = Fit(hero, "l", LeftSlotBones, bladeLocal, guardLocal, Roll * LeftRollSign,
                              out var gripL, out var anglesL, out var depthL, out string logL);

            Object.DestroyImmediate(hero);

            if (!okRight)
            {
                Debug.LogError("[IsoRPG] Правая рука не посчиталась — оставляю прежние числа.");
                return;
            }

            Grip = gripR;
            Fitted = anglesR;
            DepthRight = depthR;

            if (MirrorLeft)
            {
                var q = Quaternion.Euler(Fitted);

                GripLeft = new Vector3(-Grip.x, Grip.y, Grip.z);
                FittedLeft = new Quaternion(q.x, -q.y, -q.z, q.w).eulerAngles;
                DepthLeft = okLeft ? depthL : depthR;
            }
            else if (okLeft) { GripLeft = gripL; FittedLeft = anglesL; DepthLeft = depthL; }
            else { GripLeft = gripR; FittedLeft = anglesR; DepthLeft = depthR; }

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
                                Vector3 bladeLocal, Vector3 guardLocal, float roll,
                                out Vector3 grip, out Vector3 angles, out Vector3 depth, out string log)
        {
            grip = Vector3.zero;
            angles = Vector3.zero;
            depth = Vector3.forward;
            log = "  " + side + ": не посчиталось";

            var all = hero.GetComponentsInChildren<Transform>(true);

            var slot = slotNames.Select(n => all.FirstOrDefault(t => t.name == n))
                                .FirstOrDefault(t => t != null);

            var index = all.FirstOrDefault(t => t.name == "index_01_" + side);
            var pinky = all.FirstOrDefault(t => t.name == "pinky_01_" + side);
            var middle = all.FirstOrDefault(t => t.name == "middle_01_" + side);
            var ring = all.FirstOrDefault(t => t.name == "ring_01_" + side);
            var tip = all.FirstOrDefault(t => t.name == "index_03_" + side);

            // Кончики всех четырёх пальцев: в сжатом кулаке они возвращаются к
            // ладони, и середина «основание — кончик» лежит внутри захвата.
            var tips = new[] { "index", "middle", "ring", "pinky" }
                .Select(f => all.FirstOrDefault(t => t.name == f + "_03_" + side))
                .ToArray();

            if (slot == null || index == null || pinky == null || middle == null ||
                ring == null || tip == null || tips.Any(t => t == null))
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
            // Середина между основанием пальца и его кончиком, усреднённая по
            // четырём пальцам. В сжатом кулаке кончики возвращаются к ладони,
            // и эта середина попадает ровно внутрь захвата — там, где рукоять
            // и лежит. Ничего подбирать не нужно: раньше здесь стояли
            // догаданные 12 мм от косточек, и рукоять выходила за наружный
            // край ладони.
            var bases = new[] { index, middle, ring, pinky };

            var handle = Vector3.zero;

            for (int i = 0; i < 4; i++)
                handle += (bases[i].position + tips[i].position) * 0.5f;

            handle *= 0.25f;

            var knuckles = (index.position + middle.position + ring.position + pinky.position) * 0.25f;

            // --- в систему кости-держателя -----------------------------------
            grip = slot.InverseTransformPoint(handle);

            var bladeInSlot = slot.InverseTransformDirection(blade);
            var guardInSlot = slot.InverseTransformDirection(guard);

            var from = Quaternion.LookRotation(bladeLocal, guardLocal);
            var to = Quaternion.LookRotation(bladeInSlot, guardInSlot);

            // Прокрутка вокруг клинка и доводка глубины — уже поверх расчёта.
            var fitted = to * Quaternion.Inverse(from) * Quaternion.Euler(0f, roll, 0f);

            angles = fitted.eulerAngles;
            depth = slot.InverseTransformDirection(fingers);
            grip += depth * DepthNudge;

            // Вертикальный подъём — по мировой вертикали, переведённой в
            // систему держателя.
            grip += slot.InverseTransformDirection(Vector3.up) * LiftUp;

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
