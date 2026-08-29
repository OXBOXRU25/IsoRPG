using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Меняет игрушечные деревья старого набора на настоящие.
    ///
    /// Деревья KayKit ростом с персонажа — это осознанный «кукольный»
    /// масштаб всего набора. Но рядом с постройками Synty, где дверь выше
    /// героя, они читаются как игрушки, и весь мир вместе с ними становится
    /// игрушечным.
    ///
    /// Замена идёт ПО МЕСТУ: берём каждое дерево в сцене, запоминаем, где
    /// оно стояло, и ставим на то же место дерево из нового набора. Лес
    /// сохраняет рисунок — где была опушка, там и останется.
    ///
    /// Два урока, оплаченных проверкой в игре:
    ///
    /// **Гигантов не берём.** У них ветки-корни свисают почти до земли и
    /// торчат вширь на много метров: красиво в кадре и невыносимо в игре —
    /// герой застревает постоянно, а препятствие по стволу такую крону не
    /// описывает даже приблизительно.
    ///
    /// **Держим расстояние.** Позиции старых деревьев рассчитаны на модели
    /// ростом с человека; поставив на их места деревья вчетверо крупнее, мы
    /// получили мешанину, где мелкое растёт внутри большого. Поэтому дерево
    /// ставится, только если рядом нет соседа.
    /// </summary>
    public static class TreeSwap
    {
        private const string Synty = "Assets/_Game/Prefabs/Synty";

        /// <summary>
        /// Насколько далеко должны стоять соседи, метров.
        ///
        /// Было семь — и лес выходил стеной. У этих деревьев не только крона
        /// широкая: у них корни расходятся лапами на несколько метров, и это
        /// нарисовано намеренно, такой у набора силуэт. Значит место дереву
        /// нужно не по стволу и даже не по кроне, а по корням — иначе лапы
        /// соседей переплетаются, и вместо леса выходит куча.
        ///
        /// Двенадцать — примерно два размаха корней плюс просвет, в который
        /// человек видит, что за деревом что-то есть.
        /// </summary>
        private const float Spacing = 12f;

        private static readonly (string path, float weight, float scale)[] Replacements =
        {
            (Synty + "/SM_Env_Tree_Large_01.prefab",  0.35f, 2.0f),
            (Synty + "/SM_Env_Tree_Round_04.prefab",  0.25f, 1.9f),
            (Synty + "/SM_Env_Tree_Thin_02.prefab",   0.20f, 2.1f),
            (Synty + "/SM_Env_Tree_Thin_03.prefab",   0.20f, 2.1f),
        };

        [MenuItem("Tools/IsoRPG/Деревья: заменить на крупные", priority = 61)]
        public static void Swap()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude,
                                                           FindObjectsSortMode.None)
                            .Where(g => g.name.StartsWith("Tree_"))
                            .Where(g => g.GetComponentInChildren<Renderer>() != null)
                            .ToArray();

            if (old.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Деревьев старого набора в сцене не нашлось.");
                return;
            }

            var random = new System.Random(3);
            var holder = GameObject.Find("BigTrees") ?? new GameObject("BigTrees");

            var taken = new List<Vector3>();
            int placed = 0, skipped = 0;

            foreach (var tree in old)
            {
                Vector3 at = tree.transform.position;
                float angle = tree.transform.eulerAngles.y;

                Object.DestroyImmediate(tree);

                if (TooClose(taken, at)) { skipped++; continue; }

                if (Put(holder.transform, at, angle, random)) { taken.Add(at); placed++; }
            }

            Rebake();

            Debug.Log("[IsoRPG] Деревья заменены: " + placed +
                      ", проредено " + skipped +
                      ". Крупные деревья требуют места: старая частота давала мешанину.");
        }

        /// <summary>
        /// Меняет уже поставленных гигантов на обычные крупные деревья.
        ///
        /// Отдельным пунктом, потому что основная замена ищет деревья
        /// СТАРОГО набора, а гиганты уже наши — она их не найдёт.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Деревья: убрать гигантов", priority = 68)]
        public static void ReplaceGiants()
        {
            if (EditorApplication.isPlaying) return;

            var giants = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude,
                                                              FindObjectsSortMode.None)
                               .Where(g => g.name.Contains("Tree_Giant"))
                               .ToArray();

            if (giants.Length == 0)
            {
                Debug.Log("[IsoRPG] Гигантов в сцене нет.");
                return;
            }

            var random = new System.Random(5);
            int replaced = 0;

            foreach (var giant in giants)
            {
                var parent = giant.transform.parent;
                Vector3 at = giant.transform.position;
                float angle = giant.transform.eulerAngles.y;

                Object.DestroyImmediate(giant);

                if (Put(parent, at, angle, random)) replaced++;
            }

            Rebake();

            Debug.Log("[IsoRPG] Гигантов заменено: " + replaced +
                      ". В их ветвях застревал герой.");
        }

        /// <summary>
        /// Прореживает уже расставленный лес.
        ///
        /// Отдельным пунктом, потому что основная замена ищет деревья СТАРОГО
        /// набора, а в сцене их больше нет — все деревья уже наши. Прежний
        /// зазор в семь метров рассчитан был на кроны, а у этих деревьев
        /// место занимают корни, и лес встал стеной.
        ///
        /// Идём по порядку и оставляем дерево, только если рядом нет уже
        /// оставленного. Порядок обхода задаёт, кто уцелеет, — и это честно:
        /// прореживание не должно выбирать «красивые», иначе рисунок леса
        /// поедет к одному краю.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Деревья: проредить", priority = 63)]
        public static void Thin()
        {
            if (EditorApplication.isPlaying) return;

            var roots = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude,
                                                             FindObjectsSortMode.None)
                              .Where(g => g.name == "BigTrees" || g.name == "WorldBorder")
                              .ToArray();

            if (roots.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Деревьев в сцене не нашлось.");
                return;
            }

            var kept = new List<Vector3>();
            int removed = 0, left = 0;

            foreach (var root in roots)
            {
                // Копию списка детей — удалять во время обхода нельзя.
                var children = new List<Transform>();
                foreach (Transform child in root.transform) children.Add(child);

                foreach (var child in children)
                {
                    // Скалы не трогаем: они и должны стоять плотно, это стена.
                    if (!child.name.Contains("Tree")) { left++; continue; }

                    if (TooClose(kept, child.position))
                    {
                        Object.DestroyImmediate(child.gameObject);
                        removed++;
                        continue;
                    }

                    kept.Add(child.position);
                    left++;
                }
            }

            NavBake.Rebake();

            Debug.Log("[IsoRPG] Лес прорежен: убрано " + removed + ", осталось " + left +
                      ". Зазор " + Spacing + " м — по размаху корней, а не по стволу.");
        }

        // ------------------------------------------------------------------

        private static bool TooClose(List<Vector3> taken, Vector3 at)
        {
            foreach (var other in taken)
            {
                float dx = other.x - at.x;
                float dz = other.z - at.z;

                if (dx * dx + dz * dz < Spacing * Spacing) return true;
            }

            return false;
        }

        private static bool Put(Transform parent, Vector3 at, float angle, System.Random random)
        {
            var pick = Pick(random);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(pick.path);

            if (asset == null)
            {
                Debug.LogWarning("[IsoRPG] Нет дерева " + pick.path);
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);

            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Разнобой по росту: одинаковые деревья выдают копипасту сильнее,
            // чем любая другая мелочь.
            go.transform.localScale = Vector3.one * pick.scale *
                                      (0.85f + (float)random.NextDouble() * 0.35f);

            Sit(go, at.y);

            return true;
        }

        private static (string path, float weight, float scale) Pick(System.Random random)
        {
            float roll = (float)random.NextDouble();
            float sum = 0f;

            foreach (var option in Replacements)
            {
                sum += option.weight;
                if (roll <= sum) return option;
            }

            return Replacements[Replacements.Length - 1];
        }

        /// <summary>Сажает дерево на землю по нарисованным границам.</summary>
        private static void Sit(GameObject go, float groundY)
        {
            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            go.transform.position += new Vector3(0f, groundY - bounds.min.y, 0f);
        }

        private static void Rebake() => NavBake.Rebake();
    }
}
