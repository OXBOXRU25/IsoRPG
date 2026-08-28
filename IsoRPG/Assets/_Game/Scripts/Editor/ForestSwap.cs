using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Меняет деревья в открытой сцене на деревья TriForge, не трогая
    /// раскладку.
    ///
    /// Почему заменой, а не новым посевом. Расстановка леса в арене
    /// подбиралась кругами и сама по себе не виновата — виноваты модели:
    /// крона Synty это десяток граней, крашенных палитрой, и рядом с
    /// персонажем она читается как груда валунов на палке. Новый посев
    /// выбросил бы вместе с моделями и композицию, то есть работу, которая
    /// уже сделана.
    ///
    /// Высоту сохраняем масштабом. Наборы расходятся по росту: то, что у
    /// одного «среднее дерево», у другого вдвое выше. Если поставить новый
    /// префаб как есть, лес поедет по размеру, и придётся заново подбирать
    /// плотность, тени и дальность. Поэтому каждый новый экземпляр ужимается
    /// или растягивается ровно до высоты того, кого он заменил.
    ///
    /// Вид дерева выбирается по имени старого, а не наугад: тонкие остаются
    /// тонкими, круглые — круглыми. Иначе на месте аккуратной аллеи вырастет
    /// каша, и это будет видно раньше, чем смена набора.
    /// </summary>
    public static class ForestSwap
    {
        private const string Trees =
            "Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/";

        /// <summary>
        /// Чем заменяем. Ключ — кусок имени старого дерева, значение —
        /// набор кандидатов; из них берётся один, но всегда один и тот же для
        /// одной и той же точки, чтобы пересборка давала тот же лес.
        /// </summary>
        private static readonly (string match, string[] replace)[] Table =
        {
            ("Thin",   new[] { "P_FFE_Birch_1", "P_FFE_Birch_2", "P_FFE_Birch_3" }),
            ("Round",  new[] { "P_FFE_Tree_1", "P_FFE_Tree_2" }),
            ("Large",  new[] { "P_FFE_Tree_1", "P_FFE_Tree_2" }),
            ("Giant",  new[] { "P_FFE_Tree_1", "P_FFE_Tree_2" }),
            ("Medium", new[] { "P_FFE_Tree_1", "P_FFE_Tree_2", "P_FFE_Spruce_B1",
                               "P_FFE_Birch_2" }),
            ("Small",  new[] { "P_FFE_Spruce_A2", "P_FFE_Birch_1" }),
            ("Pine",   new[] { "P_FFE_Spruce_A1", "P_FFE_Spruce_B1" }),
            ("Spruce", new[] { "P_FFE_Spruce_A1", "P_FFE_Spruce_B2" }),
        };

        /// <summary>Запасной вариант, если имя ни на что не похоже.</summary>
        private static readonly string[] Fallback =
            { "P_FFE_Tree_1", "P_FFE_Tree_2", "P_FFE_Spruce_B1" };

        private static readonly Dictionary<string, float> heights =
            new Dictionary<string, float>();

        [MenuItem("Tools/IsoRPG/Лес: заменить деревья на TriForge", priority = 57)]
        public static void Swap()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] Замена в режиме Play не сохранится.");
                return;
            }

            heights.Clear();

            // Собираем список ДО правки сцены: удалять объекты, по которым
            // ещё идёшь, — верный способ пропустить половину.
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .Where(IsTree)
                            .ToList();

            // Вложенные деревья выкидываем: если дерево висит внутри другого
            // дерева, менять его отдельно бессмысленно — оно исчезнет вместе
            // с родителем, а его замена останется висеть в пустоте.
            var roots = new HashSet<GameObject>(old);
            old = old.Where(t => !HasTreeAncestor(t, roots)).ToList();

            if (old.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] Деревьев в сцене не нашлось — менять нечего.");
                return;
            }

            int done = 0, skipped = 0;
            var seed = new System.Random(20260828);

            foreach (var tree in old)
            {
                // Дерево могло погибнуть вместе с родителем на прошлом шаге:
                // в сцене они вложены друг в друга, а DestroyImmediate уносит
                // и детей. Первый заход на этом и упал.
                if (tree == null) { skipped++; continue; }

                var prefab = Pick(tree.name, seed);
                if (prefab == null) { skipped++; continue; }

                float oldHeight = Measure(tree);
                var parent = tree.transform.parent;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(parent, false);
                go.transform.position = tree.transform.position;
                // Берём только рыскание. Полный поворот копировать нельзя:
                // у деревьев Synty он нёс наклон по X и Z (их кроны рисованы
                // симметрично, и завал в пять градусов там не читался), а
                // ствол TriForge с корнями от такого поворота ложится набок.
                // Первая замена дала десяток падающих берёз ровно поэтому.
                go.transform.rotation =
                    Quaternion.Euler(0f, tree.transform.eulerAngles.y, 0f);

                // Масштаб от эталонной высоты префаба, а не от габарита уже
                // поставленного объекта: у поставленного он зависит от того,
                // как повёрнута крона, и лес получился бы разнокалиберным.
                float baseHeight = PrefabHeight(prefab);
                float k = baseHeight > 0.01f && oldHeight > 0.01f
                        ? oldHeight / baseHeight
                        : 1f;

                go.transform.localScale = Vector3.one * k;
                go.name = prefab.name;

                Object.DestroyImmediate(tree);
                done++;
            }

            Debug.Log("[IsoRPG] Лес заменён на TriForge: деревьев " + done +
                      (skipped > 0 ? ", пропущено " + skipped : "") + ".");
        }

        /// <summary>
        /// Выпрямляет уже стоящие деревья TriForge.
        ///
        /// Нужен отдельным ходом, потому что замена необратима: после неё
        /// старых имён в сцене нет, и повторный прогон Swap уже ничего не
        /// найдёт. А наклон, унаследованный первой заменой, надо чем-то
        /// снимать — иначе единственный путь это откат сцены целиком.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Лес: выпрямить деревья TriForge", priority = 58)]
        public static void Straighten()
        {
            int fixedCount = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (!go.name.StartsWith("P_FFE_")) continue;

                // Наклон сидит НЕ на корне дерева, а на дочернем узле внутри
                // префаба: у берёзы автор завалил ствол на 17 градусов, чтобы
                // она росла криво, как настоящая. На своём размере это красиво,
                // а раздутая вчетверо она читается как падающая. Поэтому идём
                // по детям и распрямляем их, оставляя рыскание.
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t == go.transform) continue;

                    var e = t.localEulerAngles;
                    float tiltX = Mathf.Abs(Mathf.DeltaAngle(e.x, 0f));
                    float tiltZ = Mathf.Abs(Mathf.DeltaAngle(e.z, 0f));

                    if (tiltX < 3f && tiltZ < 3f) continue;

                    t.localRotation = Quaternion.Euler(0f, e.y, 0f);
                    fixedCount++;
                }
            }

            MarkDirty();
            Debug.Log("[IsoRPG] Выпрямлено узлов: " + fixedCount + ".");
        }

        /// <summary>
        /// Ставит в сцену контроллер ветра из набора.
        ///
        /// Без него листва стоит намертво, и это не настройка, а пропущенный
        /// объект: шейдер деревьев читает силу, скорость и направление ветра
        /// из ГЛОБАЛЬНЫХ переменных, а выставляет их каждый кадр вот этот
        /// компонент. Нет его в сцене — переменные нули, и весь лес каменный.
        ///
        /// Направление ветра — это ось X объекта, поэтому поворот у него не
        /// украшение: разворот меняет, куда клонит кроны.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Лес: включить ветер", priority = 59)]
        public static void Wind()
        {
            const string path =
                "Assets/TriForge Assets/Fantasy Forest Environment/FFE Wind Controller.prefab";

            var existing = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                                 .FirstOrDefault(g => g.name.StartsWith("FFE Wind Controller"));

            if (existing != null)
            {
                Debug.Log("[IsoRPG] Ветер уже в сцене.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning("[IsoRPG] Не найден контроллер ветра: " + path);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;

            // Ветер поперёк основного направления взгляда камеры: так качание
            // видно, а не уходит в глубину кадра.
            go.transform.rotation = Quaternion.Euler(0f, 140f, 0f);

            MarkDirty();
            Debug.Log("[IsoRPG] Ветер поставлен: листва задвигается.");
        }

        /// <summary>
        /// Дерево ли это. Только по имени: у деревьев в сцене нет ни общего
        /// компонента, ни слоя, а имя набора Synty жёстко размечено —
        /// SM_Env_Tree_*.
        /// </summary>
        private static bool IsTree(GameObject go) =>
            go.name.StartsWith("SM_Env_Tree_") ||
            go.name.StartsWith("SM_Generic_Tree_");

        private static GameObject Pick(string oldName, System.Random random)
        {
            foreach (var (match, replace) in Table)
                if (oldName.Contains(match))
                    return Load(replace[random.Next(replace.Length)]);

            return Load(Fallback[random.Next(Fallback.Length)]);
        }

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Trees + name + ".Prefab")
            ?? AssetDatabase.LoadAssetAtPath<GameObject>(Trees + name + ".prefab");

        /// <summary>
        /// Помечает открытую сцену изменённой.
        ///
        /// Обязательно, и это не перестраховка: в пакетном режиме
        /// SaveOpenScenes сохраняет только грязные сцены, а создание объекта
        /// через PrefabUtility сцену грязной НЕ помечает. Из-за этого
        /// контроллер ветра встал, отчитался в журнал и пропал при
        /// сохранении — журнал говорил «поставлен», а в сцене его не было.
        /// </summary>
        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Есть ли среди предков объекта другое дерево из списка.</summary>
        private static bool HasTreeAncestor(GameObject go, HashSet<GameObject> all)
        {
            for (var t = go.transform.parent; t != null; t = t.parent)
                if (all.Contains(t.gameObject)) return true;

            return false;
        }

        /// <summary>Высота объекта в сцене, как её видит игрок.</summary>
        private static float Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds.size.y;
        }

        /// <summary>
        /// Высота префаба при единичном масштабе. Считается один раз на вид:
        /// иначе на сотне деревьев это сотня инстанцирований впустую.
        /// </summary>
        private static float PrefabHeight(GameObject prefab)
        {
            if (heights.TryGetValue(prefab.name, out float known)) return known;

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            float h = Measure(probe);
            Object.DestroyImmediate(probe);

            heights[prefab.name] = h;
            return h;
        }
    }
}
