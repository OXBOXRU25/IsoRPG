using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Пруд: чаша в рельефе, водная гладь, кувшинки.
    ///
    /// <b>Способ автора</b>, снятый с его сцены: вода — это плоскости
    /// SM_Env_Water_Plane_01 с материалами Water_Lake_01 (озеро, одна
    /// плоскость 22x40 м) и Water_River_01 (река — цепочка мелких плоскостей
    /// по 7–20 м, состыкованных вдоль русла).
    ///
    /// <b>Чашу надо выкопать, а не класть воду на склон.</b> Плоскость
    /// горизонтальна; положи её на неровную землю — и края уйдут в грунт, а
    /// с другой стороны повиснут в воздухе. Поэтому сначала лепим впадину с
    /// пологим берегом, потом наливаем.
    ///
    /// Кувшинки — те самые SM_Env_Flowers_Flat_01, которые я по ошибке сеял
    /// по лугу и которые заказчик опознал как водные с первого взгляда.
    /// </summary>
    public static class SyntyWater
    {
        private const string Holder = "Пруд Synty";
        private const string Biome = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest";

        /// <summary>Один водоём: где, какой гладью, какой чашей и глубиной.</summary>
        public readonly struct Pond
        {
            public readonly Vector2 Centre;
            public readonly float Radius;   // водная гладь
            public readonly float Bowl;     // чаша вместе с берегом
            public readonly float Depth;

            public Pond(float x, float z, float radius, float bowl, float depth)
            {
                Centre = new Vector2(x, z); Radius = radius; Bowl = bowl; Depth = depth;
            }
        }

        /// <summary>
        /// Водоёмы мира. Большой сделан впятеро по площади от малого —
        /// радиус растёт как корень, поэтому 13 -> 29, а не 13 -> 65.
        /// </summary>
        /// <summary>Сколько заливов по окружности берега.</summary>
        private const float ShoreLobes = 2.6f;

        /// <summary>
        /// Насколько близко к глади может подойти край чаши, в долях
        /// радиуса глади. Меньше 1.1 нельзя: вода начнёт вылезать на сушу.
        /// </summary>
        private const float ShoreKeep = 1.18f;

        public static readonly Pond[] Ponds =
        {
            new Pond( 20f, -16f, 13f, 22f, 2.6f),
            new Pond(-46f,  34f, 29f, 46f, 4.2f),

            // Ещё два, покрупнее. Разнесены так, чтобы чаши не перекрывали
            // друг друга: сложенные радиусы двух соседей должны быть меньше
            // расстояния между их центрами, иначе две ямы сливаются в одну
            // канаву. Отсюда и неровные координаты — они посчитаны, а не
            // выбраны на глаз.
            new Pond( 60f,  58f, 35f, 52f, 4.6f),
            new Pond(-62f, -66f, 36f, 50f, 4.8f),
        };

        /// <summary>Совместимость со старым кодом: первый пруд.</summary>
        public static Vector2 Centre => Ponds[0].Centre;
        public static float Radius => Ponds[0].Radius;

        /// <summary>Попадает ли точка в любой водоём (с запасом).</summary>
        public static bool Inside(Vector2 world, float margin)
        {
            foreach (var p in Ponds)
                if (Vector2.Distance(world, p.Centre) < p.Radius + margin) return true;

            return false;
        }

        /// <summary>Насколько точка внутри чаши любого водоёма, 0..1.</summary>
        public static bool InBowl(Vector2 world, float margin)
        {
            foreach (var p in Ponds)
                if (Vector2.Distance(world, p.Centre) < p.Bowl + margin) return true;

            return false;
        }

        /// <summary>Насколько уровень воды ниже кромки берега, метров.</summary>
        private const float Below = 1.1f;

        public static void Build()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — пруд копать негде.");
                return;
            }

            Clear();

            var holder = new GameObject(Holder);

            foreach (var pond in Ponds)
            {
                float rim = Carve(terrain, pond);
                float level = rim - Below;

                Surface(holder.transform, pond, level);
                Lilies(holder.transform, terrain, pond, level);

                Debug.Log("[IsoRPG] Водоём: центр (" + pond.Centre.x + ", " + pond.Centre.y +
                          "), гладь " + pond.Radius + " м, чаша " + pond.Bowl +
                          " м, глубина " + pond.Depth + " м, уровень " + level.ToString("0.00") + ".");
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == Holder);

            if (old == null) return;

            Object.DestroyImmediate(old);
            EditorSceneManager.MarkAllScenesDirty();
        }

        // ------------------------------------------------------------------

        /// <summary>Выкопать чашу. Возвращает высоту кромки берега.</summary>
        private static float Carve(Terrain terrain, Pond pond)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            float[,] h = data.GetHeights(0, 0, res, res);

            // Высота кромки — та, что была в центре до копания.
            float rim = terrain.SampleHeight(new Vector3(pond.Centre.x, 0f, pond.Centre.y)) +
                        terrain.transform.position.y;

            float depthNorm = pond.Depth / data.size.y;

            for (int y = 0; y < res; y++)
            {
                float wz = terrain.transform.position.z + (float)y / (res - 1) * data.size.z;

                for (int x = 0; x < res; x++)
                {
                    float wx = terrain.transform.position.x + (float)x / (res - 1) * data.size.x;

                    float d = Vector2.Distance(new Vector2(wx, wz), pond.Centre);
                    if (d > pond.Bowl) continue;

                    // Берег гуляет, а не идёт окружностью.
                    //
                    // Копали по чистому расстоянию от центра — выходил
                    // ровный круг с каймой правильной окружности, пруд
                    // читался как выкопанный экскаватором. В природе (и на
                    // референсе из WoW) у воды рваный край с заливами.
                    //
                    // Гуляет только ВНЕШНИЙ край чаши: у́же глади он не
                    // становится никогда, иначе вода вылезет на сушу —
                    // модель водной глади круглая, её форму не поменять.
                    float ang = Mathf.Atan2(wz - pond.Centre.y, wx - pond.Centre.x);

                    float wob = Mathf.PerlinNoise(
                        Mathf.Cos(ang) * ShoreLobes + pond.Centre.x * 0.07f,
                        Mathf.Sin(ang) * ShoreLobes + pond.Centre.y * 0.07f);

                    float edge = Mathf.Lerp(pond.Radius * ShoreKeep, pond.Bowl, wob);

                    if (d > edge) continue;

                    // Профиль берега: пологий к краю, плоское дно в середине.
                    // Резкий край дал бы стакан с водой, а не пруд.
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / edge));

                    h[y, x] = Mathf.Max(0f, h[y, x] - depthNorm * k * k);
                }
            }

            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            Debug.Log("[IsoRPG] Чаша выкопана: радиус " + pond.Bowl + " м, глубина " +
                      pond.Depth + " м, кромка на " + rim.ToString("0.00") + " м.");

            return rim;
        }

        /// <summary>Водная гладь — плоскость с материалом озера.</summary>
        private static void Surface(Transform parent, Pond pond, float level)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                Biome + "/Prefabs/SM_Env_Water_Plane_01.prefab");

            GameObject water;

            if (asset != null)
            {
                water = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            }
            else
            {
                // Префаба нет — собираем из модели, она в наборе есть точно.
                var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Biome + "/Models/SM_Env_Water_Plane_01.fbx");

                if (mesh == null)
                {
                    Debug.LogError("[IsoRPG] Модели воды в наборе не нашлось.");
                    return;
                }

                water = (GameObject)PrefabUtility.InstantiatePrefab(mesh, parent);
            }

            water.name = "Гладь";
            water.transform.position = new Vector3(pond.Centre.x, level, pond.Centre.y);

            // Подгоняем под нужный радиус по фактическому размеру модели.
            var rs = water.GetComponentsInChildren<Renderer>(true);

            if (rs.Length > 0)
            {
                var box = rs[0].bounds;
                foreach (var r in rs) box.Encapsulate(r.bounds);

                // Тянем по КАЖДОЙ оси отдельно.
                //
                // Модель воды у Synty прямоугольная: 13 на 26 метров. При
                // равномерном масштабе гладь остаётся вдвое длиннее, чем
                // шире, и кувшинки, разложенные по кругу, вылезают на
                // траву с узких боков — заказчик это и увидел.
                var s = water.transform.localScale;

                if (box.size.x > 0.01f) s.x *= (pond.Radius * 2f) / box.size.x;
                if (box.size.z > 0.01f) s.z *= (pond.Radius * 2f) / box.size.z;

                water.transform.localScale = s;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                Biome + "/Materials/Water_Lake_01.mat");

            if (mat != null)
                foreach (var r in water.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;

            // Коллайдер воде не нужен: по ней не ходят, в неё входят.
            foreach (var c in water.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        /// <summary>Кувшинки по глади — те самые плоские цветы.</summary>
        private static void Lilies(Transform parent, Terrain terrain, Pond pond, float level)
        {
            var names = new[] { "SM_Env_Flowers_Flat_01", "SM_Env_Flowers_Flat_02",
                                "SM_Env_Flowers_Flat_03" };

            var prefabs = names
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(
                    Biome + "/Prefabs/" + n + ".prefab"))
                .Where(p => p != null)
                .ToArray();

            if (prefabs.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Кувшинок в наборе не нашлось.");
                return;
            }

            Random.InitState(9101);

            // Не сплошным ковром: кувшинки жмутся к берегу, середина пруда
            // остаётся открытой водой. Сплошь покрытый пруд читается болотом.
            int count = Mathf.RoundToInt(26f * (pond.Radius / 13f));

            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                // 0.80 вместо 0.88: у самой кромки вода в палец глубиной,
                // и кувшинка там читается лежащей на берегу даже когда
                // формально стоит в воде.
                float r = Mathf.Lerp(pond.Radius * 0.30f, pond.Radius * 0.80f, Random.value);

                var at = new Vector3(pond.Centre.x + Mathf.Cos(a) * r, 0f,
                                     pond.Centre.y + Mathf.Sin(a) * r);

                // Кладём ТОЛЬКО туда, где под кувшинкой действительно вода.
                // Иначе они ложатся на берег и висят в воздухе — так и было
                // на первом пруду, заказчик увидел сразу.
                float bed = terrain.SampleHeight(at) + terrain.transform.position.y;
                if (bed > level - 0.35f) { i--; continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(
                    prefabs[Random.Range(0, prefabs.Length)], parent);

                go.transform.position = new Vector3(at.x, level + 0.04f, at.z);

                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(0.7f, 1.5f);

                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
            }

            Debug.Log("[IsoRPG] Кувшинок на пруду радиусом " + pond.Radius + " м: " + count + ".");
        }
    }
}
