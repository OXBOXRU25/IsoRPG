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

        /// <summary>Центр пруда в мировых координатах.</summary>
        public static readonly Vector2 Centre = new Vector2(20f, -16f);

        /// <summary>Радиус водной глади, метров.</summary>
        public static readonly float Radius = 13f;

        /// <summary>Радиус чаши вместе с берегом, метров.</summary>
        private const float Bowl = 22f;

        /// <summary>Глубина чаши в центре, метров.</summary>
        private const float Depth = 2.6f;

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

            float rim = Carve(terrain);
            float level = rim - Below;

            var holder = new GameObject(Holder);
            holder.transform.position = new Vector3(Centre.x, level, Centre.y);

            Surface(holder.transform, level);
            Lilies(holder.transform, level);

            Debug.Log("[IsoRPG] Пруд готов: центр (" + Centre.x + ", " + Centre.y +
                      "), гладь радиусом " + Radius + " м, уровень " +
                      level.ToString("0.00") + " м, чаша глубиной " + Depth + " м.");

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
        private static float Carve(Terrain terrain)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            float[,] h = data.GetHeights(0, 0, res, res);

            // Высота кромки — та, что была в центре до копания.
            float rim = terrain.SampleHeight(new Vector3(Centre.x, 0f, Centre.y)) +
                        terrain.transform.position.y;

            float depthNorm = Depth / data.size.y;

            for (int y = 0; y < res; y++)
            {
                float wz = terrain.transform.position.z + (float)y / (res - 1) * data.size.z;

                for (int x = 0; x < res; x++)
                {
                    float wx = terrain.transform.position.x + (float)x / (res - 1) * data.size.x;

                    float d = Vector2.Distance(new Vector2(wx, wz), Centre);
                    if (d > Bowl) continue;

                    // Профиль берега: пологий к краю, плоское дно в середине.
                    // Резкий край дал бы стакан с водой, а не пруд.
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / Bowl));

                    h[y, x] = Mathf.Max(0f, h[y, x] - depthNorm * k * k);
                }
            }

            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            Debug.Log("[IsoRPG] Чаша выкопана: радиус " + Bowl + " м, глубина " +
                      Depth + " м, кромка на " + rim.ToString("0.00") + " м.");

            return rim;
        }

        /// <summary>Водная гладь — плоскость с материалом озера.</summary>
        private static void Surface(Transform parent, float level)
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
            water.transform.position = new Vector3(Centre.x, level, Centre.y);

            // Подгоняем под нужный радиус по фактическому размеру модели.
            var rs = water.GetComponentsInChildren<Renderer>(true);

            if (rs.Length > 0)
            {
                var box = rs[0].bounds;
                foreach (var r in rs) box.Encapsulate(r.bounds);

                float own = Mathf.Max(box.size.x, box.size.z);
                if (own > 0.01f)
                    water.transform.localScale *= (Radius * 2f) / own;
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
        private static void Lilies(Transform parent, float level)
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
            int count = 26;

            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Lerp(Radius * 0.45f, Radius * 0.92f, Random.value);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(
                    prefabs[Random.Range(0, prefabs.Length)], parent);

                go.transform.position = new Vector3(
                    Centre.x + Mathf.Cos(a) * r,
                    level + 0.04f,          // чуть выше глади, иначе мерцает
                    Centre.y + Mathf.Sin(a) * r);

                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(0.7f, 1.5f);

                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
            }

            Debug.Log("[IsoRPG] Кувшинок положено: " + count + ", по кольцу у берега.");
        }
    }
}
