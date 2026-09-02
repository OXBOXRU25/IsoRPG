using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп хвата: ставит в ряд героев с разными доворотами кинжала.
    ///
    /// Ориентацию клинка в руке аналитически не вывести — у кости своя ось, у
    /// модели оружия своя, и совпадают они только случайно. Числа примерки
    /// сняты в Blender (память проекта `dagger-grip-fit`), но Blender считает
    /// вертикалью Z и крутит углы в порядке XYZ, а Unity — Y и ZXY.
    /// Покомпонентная перестановка осей между этими системами НЕ равна
    /// повороту, и ровно на ней кинжал и лёг не в ту сторону.
    ///
    /// Поэтому не гадаем по кругу, а меряем: один прогон, два кадра, на них
    /// сразу видно, какой доворот верный.
    ///
    /// Две вещи, на которых прошлый щуп соврал, и обе учтены:
    ///
    ///   - **поза.** Мерили на Т-позе, а в бою кисть развёрнута иначе — и
    ///     «правильный» доворот оказался боком. Здесь герой ставится в кадр
    ///     БОЕВОГО клипа, на середину замаха;
    ///   - **ракурс.** С одного вида клинок закрывает кисть, и половина
    ///     выводов делается о том, чего не видно. Снимаем с двух сторон.
    ///
    /// Ещё одна ловушка прошлого захода: щуп искал кость `prop_r`, которой у
    /// нашего героя нет вовсе. Ищем тот же список костей, что и боевой код.
    /// </summary>
    public static class GripProbe
    {
        private const string Hero = "Human-Custom2";

        /// <summary>Тот же кинжал, что считает и ставит задание grip-fit.</summary>
        private const string Dagger = GripFit.DaggerPath;

        /// <summary>Кости-держатели по порядку поиска — тот же список, что в WeaponVisual.</summary>
        private static readonly string[] RightSlotBones = { "handslot.r", "prop_r", "hand_r" };

        private static readonly string[] LeftSlotBones = { "handslot.l", "prop_l", "hand_l" };

        /// <summary>Смещение в кости, метры. Из замера Blender, через grip-fit.</summary>
        private static Vector3 Grip => GripFit.Grip;

        /// <summary>
        /// Середина ряда — доворот, посчитанный заданием `grip-fit` по матрице
        /// из Blender. Остальные варианты — довороты от него на прямой угол по
        /// каждой оси: если кость `hand_r` в Unity развёрнута не так, как в
        /// Blender, верный вариант окажется одним из соседей, и это видно на
        /// кадре сразу.
        ///
        /// `grip-fit` должен идти в очереди ПЕРЕД щупом — иначе тут останется
        /// прежнее (неверное) число, и ряд будет построен вокруг него.
        /// </summary>
        private static Vector3 Current => GripFit.Fitted;

        /// <summary>
        /// Что проверяем. Доворот применяется ПОСЛЕ основного, в системе
        /// самого оружия, — так каждая проба поворачивает клинок вокруг его
        /// собственной оси, а не вокруг оси мира.
        /// </summary>
        private static readonly float[] Deltas =
        {
            -0.012f, -0.008f, -0.004f, 0f, 0.004f, 0.008f, 0.012f, 0.016f,
        };

        /// <summary>Боевой клип: на нём и надо смотреть, а не на стойке.</summary>
        private const string AttackClip =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/" +
            "Armed/RPG-Character@Armed-Idle.FBX";

        /// <summary>Доля клипа, на которой замах раскрыт и клинок весь виден.</summary>
        private const float AttackAt = 0f;

        /// <summary>Куда смотреть камере: середина ряда и кулак первого варианта.</summary>
        public static Vector3 Centre { get; private set; } = new Vector3(0f, 1.35f, 0f);

        public static Vector3 FirstHand { get; private set; } = new Vector3(0f, 1.35f, 0f);

        [MenuItem("Tools/IsoRPG/Щуп: хват оружия", priority = 47)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) return;

            var heroPrefab = FindPrefab(Hero);
            var daggerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dagger);

            if (heroPrefab == null || daggerPrefab == null)
            {
                Debug.LogError("[IsoRPG] Нет героя или кинжала для щупа хвата.");
                return;
            }

            var attack = AssetDatabase.LoadAllAssetsAtPath(AttackClip)
                                      .OfType<AnimationClip>()
                                      .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (attack == null)
                Debug.LogWarning("[IsoRPG] Боевого клипа нет — замер пойдёт на Т-позе и снова соврёт.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Солнце").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
            sun.intensity = 1.6f;

            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Земля";
            ground.transform.localScale = Vector3.one * 4f;

            // Шире метра: у соседей клинки торчат в стороны и перекрывают друг
            // друга — ряд становится нечитаемым ровно там, где его смотрят.
            float step = 1.5f;

            // Герои стоят лицом к камере: судить хват со спины нельзя, кисть
            // закрыта телом. Камера снимает с юга (yaw 180), значит герой
            // должен смотреть туда же.
            var facing = Quaternion.Euler(0f, 180f, 0f);

            for (int i = 0; i < Deltas.Length; i++)
            {
                // Ряд идёт по ГЛУБИНЕ посадки: Павлон 02.09.2026 назвал ровно
                // это — «рукоять выходит за внешние пределы руки, надо сажать
                // глубже в ладонь». Доворот при этом один на весь ряд.
                var deeper = Grip + GripFit.DepthRight * Deltas[i];
                var deeperLeft = GripFit.GripLeft + GripFit.DepthLeft * Deltas[i];

                var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);
                hero.name = (i + 1) + ". глубже на " + (Deltas[i] * 1000f).ToString("0") + " мм";
                hero.transform.position = new Vector3((i - (Deltas.Length - 1) * 0.5f) * step, 0f, 0f);
                hero.transform.rotation = facing;

                // Поза ДО того, как цеплять оружие: кисть в бою развёрнута
                // иначе, чем в покое, и мерить хват надо в ней.
                //
                // Клип держит кривую положения корня, и наложение позы
                // утаскивает объект в начало координат — в первом заходе все
                // слиплись в одну кучу. Позицию возвращаем сами.
                if (attack != null)
                {
                    var keep = hero.transform.position;
                    attack.SampleAnimation(hero, attack.length * AttackAt);
                    hero.transform.position = keep;
                }

                var bone = FindAnyBone(hero, RightSlotBones);

                if (bone == null)
                {
                    Debug.LogWarning($"[IsoRPG] У героя нет ни одной кости-держателя " +
                                     $"({string.Join(", ", RightSlotBones)}).");
                    continue;
                }

                var blade = (GameObject)PrefabUtility.InstantiatePrefab(daggerPrefab, bone);
                blade.transform.localPosition = deeper;
                blade.transform.localRotation = Quaternion.Euler(Current);
                blade.transform.localScale = Vector3.one;

                // Второй клинок — в левую, СВОИМИ числами, а не отражением.
                //
                // Кисти у Sidekick не зеркальны: щуп намерил расхождение
                // отражённой правой и левой в 204° по трём осям. Левая рука
                // считается по своим костям тем же правилом — Павлон увидел
                // это на кадре раньше, чем щуп намерил.
                var leftBone = FindAnyBone(hero, LeftSlotBones);

                if (leftBone != null)
                {
                    var second = (GameObject)PrefabUtility.InstantiatePrefab(daggerPrefab, leftBone);
                    second.transform.localPosition = deeperLeft;
                    second.transform.localRotation = Quaternion.Euler(GripFit.FittedLeft);
                    second.transform.localScale = Vector3.one;
                }

                // Куда наводить камеру. Целимся в кулак, а не в середину
                // героя: судим мы хват, всё остальное в кадре лишнее.
                if (i == 0) FirstHand = bone.position;
            }

            Centre = new Vector3(0f, FirstHand.y, FirstHand.z);

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/GripProbe.unity");

            Debug.Log("[IsoRPG] Щуп хвата: " + Deltas.Length + " вариантов слева направо, " +
                      "доворот " + Current + ", глубина — " +
                      string.Join(" | ", Deltas.Select(d => (d * 1000f).ToString("0") + " мм")) +
                      ". Кость: " + string.Join("/", RightSlotBones) +
                      ", кинжал " + System.IO.Path.GetFileNameWithoutExtension(Dagger) +
                      ", поза — середина замаха, кулак на высоте " + FirstHand.y.ToString("0.00") + " м.");
        }

        /// <summary>Первая найденная кость из списка: наборы называют её по-разному.</summary>
        private static Transform FindAnyBone(GameObject root, string[] names)
        {
            var all = root.GetComponentsInChildren<Transform>(true);

            foreach (string boneName in names)
            {
                var bone = all.FirstOrDefault(t => t.name == boneName);
                if (bone != null) return bone;
            }

            return null;
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
