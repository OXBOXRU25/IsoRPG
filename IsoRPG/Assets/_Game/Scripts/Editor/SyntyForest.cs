using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Лес из биома Synty: деревья и подлесок префабами.
    ///
    /// <b>Плотность взята с демо-сцены автора, а не выдумана.</b> Замер по
    /// демо Fantasy Forest: деревьев 0.3 на 100 м², кустов 0.7, травы около
    /// 42 суммарно. Переносим соотношение, а не абсолютные числа: у нас
    /// игровая площадка 160x160 = 25 600 м².
    ///
    /// <b>Приземная трава сюда НЕ входит.</b> Её кладём слоем подлеска
    /// террейна — так делает и сама Synty в своей демо-сцене (карта
    /// подлеска 1024 на три вида). Прежние 46 тысяч кустиков TriForge,
    /// разложенных поштучно, стоили 151 МБ в файле сцены и паузу при
    /// каждой загрузке.
    ///
    /// <b>Коллайдеры.</b> У покупных деревьев в префабе стоит MeshCollider
    /// по всей геометрии, включая каждую ветку. Агент ходит по навмешу, а
    /// тело упирается в физику, и обойти нельзя — ветки торчат на метры.
    /// Поэтому меши снимаем, ставим капсулу по стволу.
    /// </summary>
    public static class SyntyForest
    {
        private const string Holder = "Лес Synty";
        private const string Biome = "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Prefabs";

        /// <summary>Игровая площадка, метров. Та же, по которой печётся навигация.</summary>
        private const float Field = 160f;

        // Гиганты остаются — заказчик оставил их прямо: «большие высокие
        // деревья не трогай». Они и держат кадр: в изометрии их стволы
        // уходят за верх экрана и дают лесу масштаб.
        //
        // Убраны древовидные папоротники (Fern_Tree): силуэт у них
        // тропический, читаются пальмами и сказочному лесу не подходят.
        private static readonly string[] Trees =
        {
            "SM_Env_Tree_Giant_01", "SM_Env_Tree_Giant_02",
            "SM_Env_Tree_Large_01", "SM_Env_Tree_Large_02",
            "SM_Env_Tree_Medium_01", "SM_Env_Tree_Medium_02",
        };

        // Fern_Koru убран: у него точка отсчёта смещена вверх, и он висел в
        // воздухе. Остальное сажается по нижней грани, а не по точке
        // отсчёта, — см. Seat().
        private static readonly string[] Under =
        {
            "SM_Env_Fern_01", "SM_Env_Fern_02", "SM_Env_Fern_03",
            "SM_Env_Mushroom_01", "SM_Env_Mushroom_02", "SM_Env_Mushroom_03",
            "SM_Env_Moss_Lumps_01", "SM_Env_Moss_Lumps_02",
            "SM_Env_Log_01", "SM_Env_Roots_Small_01", "SM_Env_Roots_Small_02",
        };

        public static void Sow()
        {
            Clear();

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — сеять не на чем.");
                return;
            }

            var holder = new GameObject(Holder);

            // Числа плотности с демо автора: деревьев 0.3 на 100 м²,
            // подлеска 0.7. На 25 600 м² это 77 и 179.
            int trees = Mathf.RoundToInt(Field * Field / 100f * 0.3f);
            int under = Mathf.RoundToInt(Field * Field / 100f * 0.7f);

            Random.InitState(2308);

            int placedTrees = Scatter(Trees, trees, holder.transform, terrain, 0.85f, 1.25f);
            int placedUnder = Scatter(Under, under, holder.transform, terrain, 0.7f, 1.4f);

            int objects = holder.GetComponentsInChildren<Transform>(true).Length;

            Debug.Log("[IsoRPG] Лес Synty посеян: деревьев " + placedTrees +
                      ", подлеска " + placedUnder +
                      ". Объектов в сцене от него: " + objects +
                      " (у каждого префаба свои LOD-узлы).");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Земля слоями Synty вместо нашей текстуры.
        ///
        /// Своя текстура была одна на весь террейн и повторялась мелким
        /// узором — под травой этого не видно, а на голой земле читается
        /// ковром. У биома лежат готовые слои: мох, земля, грязь и листва в
        /// двух вариантах, все в их художественном языке.
        ///
        /// Красим не однотонно: мох основой, поверх пятна листвы и земли
        /// шумом Перлина. Однотонная заливка любым слоем даёт тот же ковёр,
        /// только другого цвета — узор виден там, где нет разнообразия.
        /// </summary>
        public static void Ground()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — красить нечего.");
                return;
            }

            const string T = "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Terrain/";

            string[] names =
            {
                "Moss_01", "Leaves_Terrain_01", "Dirt_01",
                "Leaves_Multicolour_Terrain_01", "Mud_01",
            };

            var layers = new List<TerrainLayer>();

            foreach (var n in names)
            {
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(T + n + ".terrainlayer");
                if (l != null) layers.Add(l);
                else Debug.LogWarning("[IsoRPG] Нет слоя " + n);
            }

            if (layers.Count == 0)
            {
                Debug.LogError("[IsoRPG] Слои Synty не найдены.");
                return;
            }

            var data = terrain.terrainData;
            data.terrainLayers = layers.ToArray();

            int res = data.alphamapResolution;
            var map = new float[res, res, layers.Count];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // Три шума разного шага: крупные поляны, средние пятна
                    // листвы, мелкие проплешины земли.
                    float big = Mathf.PerlinNoise(x * 0.006f, y * 0.006f);
                    float mid = Mathf.PerlinNoise(x * 0.02f + 100f, y * 0.02f + 100f);
                    float fine = Mathf.PerlinNoise(x * 0.06f + 300f, y * 0.06f + 300f);

                    var w = new float[layers.Count];

                    w[0] = 1f;                                              // мох — основа
                    if (layers.Count > 1) w[1] = Mathf.Max(0f, mid - 0.45f) * 3f;
                    if (layers.Count > 2) w[2] = Mathf.Max(0f, fine - 0.62f) * 4f;
                    if (layers.Count > 3) w[3] = Mathf.Max(0f, big - 0.58f) * 2.5f;
                    if (layers.Count > 4) w[4] = Mathf.Max(0f, fine - 0.78f) * 3f;

                    float sum = w.Sum();
                    for (int i = 0; i < layers.Count; i++) map[y, x, i] = w[i] / sum;
                }
            }

            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);

            Debug.Log("[IsoRPG] Земля перекрашена слоями Synty: " + layers.Count +
                      " слоёв (" + string.Join(", ", layers.Select(l => l.name)) +
                      "), карта " + res + "x" + res + ", пятна шумом Перлина.");

            EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == Holder);

            if (old == null) return;

            Object.DestroyImmediate(old);

            // Пометить сцену грязной обязательно, иначе SaveOpenScenes её
            // пропустит: снос отчитается в журнал, а в файле лес останется.
            // Ровно это и случилось 29.08.2026 — прогон сказал «снят», а в
            // игре грибы стояли на месте.
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Прежний лес Synty снят, сцена помечена грязной.");
        }

        // ------------------------------------------------------------------

        private static int Scatter(string[] names, int count, Transform parent,
                                   Terrain terrain, float minScale, float maxScale)
        {
            var prefabs = new List<GameObject>();

            foreach (var n in names)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Biome + "/" + n + ".prefab");
                if (go != null) prefabs.Add(go);
                else Debug.LogWarning("[IsoRPG] Нет префаба " + n);
            }

            if (prefabs.Count == 0) return 0;

            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(-Field * 0.5f, Field * 0.5f);
                float z = Random.Range(-Field * 0.5f, Field * 0.5f);

                // Пятачок вокруг начала координат оставляем чистым: там
                // стоит герой, и дерево в лицо на старте — не композиция.
                if (new Vector2(x, z).magnitude < 8f) { i--; continue; }

                float y = terrain.SampleHeight(new Vector3(x, 0f, z)) +
                          terrain.transform.position.y;

                var src = prefabs[Random.Range(0, prefabs.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);

                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);

                Seat(go, y);
                Trunk(go);
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Посадить объект на землю по НИЖНЕЙ ГРАНИ, а не по точке отсчёта.
        ///
        /// У покупных префабов точка отсчёта где угодно: у папоротника-улитки
        /// она оказалась выше самого растения, и он висел в воздухе посреди
        /// кадра. Ставить по ней — значит доверять чужой договорённости,
        /// которой нет. Считаем нижнюю грань нарисованного и опускаем объект
        /// на разницу.
        /// </summary>
        private static void Seat(GameObject go, float ground)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);

            float bottom = box.min.y;
            float shift = ground - bottom;

            // Чуть утапливаем: корни и мох должны входить в землю, иначе
            // между объектом и склоном видна щель.
            go.transform.position += new Vector3(0f, shift - 0.05f, 0f);
        }

        /// <summary>Меш-коллайдер по веткам снять, поставить капсулу по стволу.</summary>
        private static void Trunk(GameObject go)
        {
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true))
                Object.DestroyImmediate(mc);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);

            // Радиус ствола — примерно 6% габарита кроны: проверено на
            // прошлом наборе, ветки становятся проходимы, ствол нет.
            float radius = Mathf.Max(box.size.x, box.size.z) * 0.06f;
            if (radius < 0.05f) return;

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = box.size.y;
            col.center = new Vector3(0f, box.size.y * 0.5f, 0f);
        }
    }
}
