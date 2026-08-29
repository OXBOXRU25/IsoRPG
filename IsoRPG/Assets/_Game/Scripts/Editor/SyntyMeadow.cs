using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Луг по числам автора: трава, цветы, подсолнухи, камни, фруктовые деревья.
    ///
    /// <b>Все числа сняты со сцены Demo лугового биома Synty</b> щупом
    /// PnbAnalyze, ничего не выдумано. Террейн у автора 400x400 м; плотности
    /// пересчитываются на нашу площадку.
    ///
    /// <b>Важная поправка к моему прежнему предположению:</b> у автора
    /// слоёв подлеска на террейне НЕТ ВООБЩЕ — вся трава стоит префабами.
    /// Я собирался сеять её картой плотности террейна и был неправ.
    ///
    /// Каждый вид несёт свой размерный ряд и свою посадку в землю — они у
    /// автора разные и это не случайность: высокая трава утоплена на метр с
    /// лишним, цветы почти не утоплены, плоские цветы наоборот приподняты
    /// над землёй.
    /// </summary>
    public static class SyntyMeadow
    {
        private const string Holder = "Луг Synty";
        private const string Biome = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs";

        /// <summary>Игровая площадка, метров. Та же, по которой печётся навигация.</summary>
        private const float Field = 160f;

        /// <summary>Вид растения с авторскими числами.</summary>
        private readonly struct Kind
        {
            public readonly string Name;
            public readonly float Per100;    // штук на 100 м²
            public readonly float MinScale, MaxScale;
            public readonly float Sink;      // насколько низ уходит под землю, м

            public Kind(string name, float per100, float min, float max, float sink)
            {
                Name = name; Per100 = per100; MinScale = min; MaxScale = max; Sink = sink;
            }
        }

        // Таблица снята с Demo лугового биома. Порядок — по убыванию плотности.
        private static readonly Kind[] Table =
        {
            new Kind("SM_Env_Grass_Tall_Clump_04",  1.21f, 0.60f, 2.92f, 0.58f),
            new Kind("SM_Env_Grass_Tall_Clump_05",  1.10f, 0.63f, 1.47f, 1.27f),
            new Kind("SM_Env_Grass_Med_Clump_02",   0.74f, 0.61f, 1.32f, 0.22f),
            new Kind("SM_Env_Grass_Med_Clump_03",   0.73f, 0.60f, 1.34f, 0.25f),
            new Kind("SM_Env_Grass_Short_Clump_03", 0.65f, 0.76f, 1.28f, 0.27f),
            new Kind("SM_Env_Grass_Tall_Clump_03",  0.33f, 0.61f, 3.16f, 0.41f),
            new Kind("SM_Env_Grass_Tall_Clump_02",  0.25f, 0.72f, 2.24f, 0.39f),
            new Kind("SM_Env_Wildflowers_03",       0.22f, 0.58f, 1.30f, 0.10f),
            new Kind("SM_Env_Wildflowers_02",       0.19f, 0.61f, 1.28f, 0.11f),
            new Kind("SM_Env_Tree_Fruit_01",        0.24f, 0.65f, 2.22f, 0.96f),
            new Kind("SM_Env_Tree_Fruit_02",        0.14f, 0.70f, 1.41f, 1.00f),
            new Kind("SM_Env_Tree_Fruit_03",        0.13f, 0.67f, 1.61f, 0.68f),
            new Kind("SM_Env_Sunflower_01",         0.09f, 0.61f, 1.26f, 0.06f),
            new Kind("SM_Env_Wildflowers_01",       0.08f, 0.70f, 1.05f, 0.12f),
            new Kind("SM_Env_Grass_Bush_01",        0.08f, 0.80f, 1.70f, 0.58f),
            new Kind("SM_Env_Rock_02",              0.07f, 0.15f, 1.22f, 0.89f),
            new Kind("SM_Env_Rock_01",              0.06f, 0.15f, 1.59f, 0.71f),
            new Kind("SM_Env_Rock_03",              0.06f, 0.10f, 1.28f, 1.02f),
            // SM_Env_Flowers_Flat_01 убран из посева: без стеблей на траве
            // читается кувшинками, а не цветами — так и увидел заказчик.
            // У автора они приподняты НАД землёй (утоплены на -0.36), что
            // подтверждает: это для воды. Вернуть, когда появится пруд.
        };

        /// <summary>
        /// Земля лугового биома с тропами — по долям автора.
        ///
        /// <b>Правило, снятое с его сцены:</b> одна трава занимает 80% участка,
        /// всё остальное — акценты долями процента. Не «поровну между
        /// слоями»: равные доли дают пёстрый камуфляж, а не луг.
        ///
        /// Доли автора: трава 80.5, палая листва 6.5, земля 2.1, галька 2.0,
        /// трава с листвой 1.0, тропа 0.2 процента.
        ///
        /// <b>Плитка 2–4 метра, а не по умолчанию.</b> У нас на мху стояла
        /// стандартная, и земля читалась ковром с мелким узором — это было
        /// видно на голом поле сразу.
        /// </summary>
        public static void Ground()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — красить нечего.");
                return;
            }

            const string T = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Terrain/";

            // Имя слоя, размер плитки в метрах, целевая доля участка.
            var wanted = new (string name, float tile, float share)[]
            {
                ("Terrain_Meadow_Grass_01",                    4f, 0.805f),
                ("Terrain_Meadow_Dirt_Cracked_Leaves_Heavy_01", 2f, 0.065f),
                ("Terrain_Meadow_Dirt_01",                     4f, 0.021f),
                ("Terrain_Meadow_Dirt_Cracked_Pebbles_01",     2f, 0.020f),
                ("Terrain_Meadow_Grass_Leaves_01",             2f, 0.010f),
                ("Terrain_Meadow_Footpath_Tile_01",            3f, 0.002f),
            };

            var layers = new List<TerrainLayer>();

            foreach (var w in wanted)
            {
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(T + w.name + ".terrainlayer");

                if (l == null) { Debug.LogWarning("[IsoRPG] Нет слоя " + w.name); continue; }

                l.tileSize = new Vector2(w.tile, w.tile);
                EditorUtility.SetDirty(l);
                layers.Add(l);
            }

            if (layers.Count < 2)
            {
                Debug.LogError("[IsoRPG] Слоёв лугового биома не нашлось.");
                return;
            }

            var data = terrain.terrainData;
            data.terrainLayers = layers.ToArray();

            int res = data.alphamapResolution;
            int n = layers.Count;
            var map = new float[res, res, n];

            // Тропы: несколько извилистых линий через площадку.
            var paths = Paths(terrain);
            int pathIndex = layers.Count - 1;   // тропа последняя в списке

            float mPerCell = data.size.x / res;
            float halfWidth = 1.25f / mPerCell;  // тропа шириной 2.5 м
            float fade = 0.9f / mPerCell;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // Акценты шумом разного шага: крупные проплешины,
                    // средние пятна, мелкая галька.
                    float a = Mathf.PerlinNoise(x * 0.010f + 11f, y * 0.010f + 23f);
                    float b = Mathf.PerlinNoise(x * 0.035f + 71f, y * 0.035f + 37f);
                    float c = Mathf.PerlinNoise(x * 0.070f + 53f, y * 0.070f + 91f);

                    var w = new float[n];
                    w[0] = 1f;

                    // Пороги подобраны так, чтобы доли сошлись с авторскими:
                    // чем реже слой, тем выше порог отсечки.
                    if (n > 1) w[1] = Mathf.Max(0f, a - 0.62f) * 6f;
                    if (n > 2) w[2] = Mathf.Max(0f, b - 0.74f) * 5f;
                    if (n > 3) w[3] = Mathf.Max(0f, c - 0.76f) * 5f;
                    if (n > 4) w[4] = Mathf.Max(0f, b - 0.80f) * 4f;

                    // Тропа перебивает всё: по ней ходят, и трава на ней
                    // не растёт. Мягкий край — чтобы не было ножевого среза.
                    float d = Dist(paths, x, y);

                    if (d < halfWidth + fade)
                    {
                        float k = d <= halfWidth ? 1f
                            : 1f - Mathf.SmoothStep(0f, 1f, (d - halfWidth) / fade);

                        for (int i = 0; i < n; i++) w[i] *= (1f - k);
                        w[pathIndex] += k * 4f;
                    }

                    float sum = 0f;
                    for (int i = 0; i < n; i++) sum += w[i];
                    for (int i = 0; i < n; i++) map[y, x, i] = w[i] / sum;
                }
            }

            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);

            Debug.Log("[IsoRPG] Земля лугового биома: слоёв " + n +
                      ", плитка 2–4 м, троп " + paths.Count +
                      ". Доли по автору: трава 80%, листва 6.5%, остальное акцентами.");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Лежит ли точка на тропе (с запасом).
        ///
        /// По дороге ходят — на ней не растёт трава. Без этой проверки
        /// кустики садились прямо на камни мостовой, и дорога переставала
        /// читаться дорогой: заказчик увидел это первым же кадром.
        /// </summary>
        public static bool OnPath(Terrain terrain, Vector2 world, float margin)
        {
            var data = terrain.terrainData;
            int res = data.alphamapResolution;

            float u = (world.x - terrain.transform.position.x) / data.size.x * res;
            float v = (world.y - terrain.transform.position.z) / data.size.z * res;

            float mPerCell = data.size.x / res;
            float halfWidth = 1.25f / mPerCell;

            float d = Dist(Paths(terrain), Mathf.RoundToInt(u), Mathf.RoundToInt(v));

            return d < halfWidth + margin / mPerCell;
        }

        /// <summary>Извилистые тропы в координатах карты текстур.</summary>
        private static List<Vector2[]> Paths(Terrain terrain)
        {
            var data = terrain.terrainData;
            int res = data.alphamapResolution;
            var list = new List<Vector2[]>();

            // Три тропы: две через всю площадку и одна поперечная. Столько же
            // порядок величины, что у автора — тропа занимает доли процента.
            var seeds = new (float ax, float ay, float bx, float by, float wob)[]
            {
                (0.05f, 0.30f, 0.95f, 0.55f, 0.10f),
                (0.40f, 0.02f, 0.62f, 0.98f, 0.13f),
                (0.10f, 0.85f, 0.70f, 0.20f, 0.08f),
            };

            foreach (var s in seeds)
            {
                var pts = new Vector2[120];

                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)(pts.Length - 1);

                    float x = Mathf.Lerp(s.ax, s.bx, t);
                    float y = Mathf.Lerp(s.ay, s.by, t);

                    // Виляние: тропа не бывает прямой, её протаптывают в обход
                    // кустов и по низинам.
                    float wob = (Mathf.PerlinNoise(t * 4f + s.ax * 17f, s.ay * 13f) - 0.5f) * s.wob;

                    // Тропа огибает водоёмы: дорога, ныряющая в пруд, —
                    // это не брод, а недосмотр. Точку, попавшую в воду,
                    // выталкиваем наружу по радиусу от центра водоёма.
                    var world = new Vector2(
                        terrain.transform.position.x + (x + wob) * data.size.x,
                        terrain.transform.position.z + (y - wob) * data.size.z);

                    foreach (var pond in SyntyWater.Ponds)
                    {
                        float need = pond.Radius + 4f;
                        var away = world - pond.Centre;

                        if (away.magnitude >= need) continue;

                        if (away.sqrMagnitude < 0.01f) away = Vector2.right;
                        world = pond.Centre + away.normalized * need;
                    }

                    float ux = (world.x - terrain.transform.position.x) / data.size.x;
                    float uy = (world.y - terrain.transform.position.z) / data.size.z;

                    pts[i] = new Vector2(ux * res, uy * res);
                }

                list.Add(pts);
            }

            return list;
        }

        /// <summary>Расстояние от клетки до ближайшей тропы, в клетках.</summary>
        private static float Dist(List<Vector2[]> paths, int x, int y)
        {
            var p = new Vector2(x, y);
            float best = float.MaxValue;

            foreach (var path in paths)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    float d = DistToSegment(p, path[i - 1], path[i]);
                    if (d < best) best = d;
                }
            }

            return best;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;

            if (len < 1e-6f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            return Vector2.Distance(p, a + ab * t);
        }

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
            float hundreds = Field * Field / 100f;

            Random.InitState(4207);

            int total = 0, kinds = 0;

            foreach (var k in Table)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Biome + "/" + k.Name + ".prefab");

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет префаба " + k.Name);
                    continue;
                }

                int count = Mathf.RoundToInt(k.Per100 * hundreds);
                int placed = 0;

                for (int i = 0; i < count; i++)
                {
                    float x = Random.Range(-Field * 0.5f, Field * 0.5f);
                    float z = Random.Range(-Field * 0.5f, Field * 0.5f);

                    // Пятачок у начала координат оставляем чистым: там стоит
                    // герой, и куст в лицо на старте — не композиция.
                    if (new Vector2(x, z).magnitude < 6f) { i--; continue; }

                    // Водоёмы обходим: трава посреди воды и деревья в пруду —
                    // верный признак, что сеятель про воду не знает.
                    if (SyntyWater.Inside(new Vector2(x, z), 2f)) { i--; continue; }

                    // Дорогу обходим: на утоптанной тропе трава не растёт.
                    // Запас 3 м от ОСИ тропы, а не от её края. Тропа шириной
                    // 2.5 м плюс мягкий край в 0.9 — куст, отодвинутый на
                    // метр, всё равно стоит на камнях. Заказчик увидел
                    // именно это: куст посреди дороги.
                    // Запас от тропы РАЗНЫЙ для крупного и для мелкого.
                    //
                    // Одно число на всех даёт либо куст посреди дороги, либо
                    // голую полосу метров в восемь: заказчик увидел сначала
                    // первое, потом второе. У живой тропы трава подходит к
                    // камням вплотную, а кусты и деревья держатся поодаль.
                    bool nearPath = k.Name.Contains("Bush") || k.Name.Contains("Tree")
                                    || k.Name.Contains("Rock") || k.Name.Contains("Tall");

                    if (OnPath(terrain, new Vector2(x, z), nearPath ? 2.5f : 0.4f))
                    { i--; continue; }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);

                    float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) +
                                   terrain.transform.position.y;

                    go.transform.position = new Vector3(x, ground, z);
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    go.transform.localScale = Vector3.one * Random.Range(k.MinScale, k.MaxScale);

                    Seat(go, ground, k.Sink, terrain);
                    Strip(go);

                    placed++;
                }

                total += placed;
                kinds++;

                Debug.Log("[IsoRPG] Луг: " + k.Name + " — " + placed + " шт.");
            }

            int objects = holder.GetComponentsInChildren<Transform>(true).Length;

            Debug.Log("[IsoRPG] Луг Synty посеян: видов " + kinds +
                      ", растений " + total +
                      ", объектов в сцене " + objects +
                      " (у префабов свои LOD-узлы).");

            EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == Holder);

            if (old == null) return;

            Object.DestroyImmediate(old);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Прежний луг снят, сцена помечена грязной.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Посадить по нижней грани нарисованного и утопить на авторскую
        /// величину. По точке отсчёта префаба сажать нельзя: у покупных
        /// наборов она где угодно, и один папоротник у меня уже висел в небе.
        /// </summary>
        private static void Seat(GameObject go, float ground, float sink, Terrain terrain)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;

            var box = rs[0].bounds;
            foreach (var r in rs) box.Encapsulate(r.bounds);

            // Поправка на склон.
            //
            // Нижняя грань коробки горизонтальна, а земля под ней наклонена.
            // Посади объект по высоте в его ЦЕНТРЕ — и половина, смотрящая
            // вниз по склону, повиснет в воздухе. Чем шире объект и круче
            // место, тем больше зазор: он равен половине ширины на тангенс
            // угла. Утапливаем ровно на эту величину.
            var p = go.transform.position;
            var data = terrain.terrainData;

            float u = (p.x - terrain.transform.position.x) / data.size.x;
            float v = (p.z - terrain.transform.position.z) / data.size.z;

            float steep = data.GetSteepness(Mathf.Clamp01(u), Mathf.Clamp01(v));
            float radius = Mathf.Max(box.size.x, box.size.z) * 0.5f;
            float slopeSink = radius * Mathf.Tan(steep * Mathf.Deg2Rad);

            // Предел поправки — РАЗНЫЙ для камня и для травы.
            //
            // Одна цифра на всех не годится, и это стоило двух поломок
            // подряд. Щедрый предел закопал камни: они шире, чем выше, и
            // над землёй оставался мшистый верх — «это камень?». Строгий
            // предел в четверть высоты подвесил траву на крутых склонах:
            // ей нужно уходить глубже, потому что куст широкий и лёгкий.
            //
            // Камню и дереву хватает малого: у них есть ствол, который сам
            // входит в грунт. Траве даём уйти хоть наполовину — снизу её
            // всё равно не видно.
            string kind = go.name.ToLowerInvariant();
            bool solid = kind.Contains("rock") || kind.Contains("tree");

            slopeSink = Mathf.Min(slopeSink, box.size.y * (solid ? 0.15f : 0.55f));

            // Утопление ПРОПОРЦИОНАЛЬНО размеру, а не в абсолютных метрах.
            //
            // Авторские числа сняты с его экземпляров, у которых свой
            // масштаб. Применённые как есть, они хоронят мелкие: у высокой
            // травы утопление 1.27 м, а куст масштаба 0.63 сам ростом с
            // метр — от него остаются торчащие из земли кончики. Заказчик
            // это и увидел: «трава провалилась».
            //
            // Считаем долю от высоты объекта и ограничиваем половиной:
            // глубже половины уходить нечему.
            float ownSink = Mathf.Min(sink * (box.size.y / 2.4f), box.size.y * 0.5f);

            go.transform.position += new Vector3(0f, ground - box.min.y - ownSink - slopeSink, 0f);
        }

        /// <summary>
        /// Снять коллайдеры с травы и цветов.
        ///
        /// По траве ходят. Коллайдер на каждом кустике — это полторы тысячи
        /// препятствий, о которые спотыкается и герой, и монстры, и при этом
        /// на навигационной сетке их не видно. Камням и деревьям коллайдеры
        /// оставляем: в них упираться и надо.
        /// </summary>
        private static void Strip(GameObject go)
        {
            string n = go.name.ToLowerInvariant();

            bool solid = n.Contains("rock") || n.Contains("tree");
            if (solid) return;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }
    }
}
