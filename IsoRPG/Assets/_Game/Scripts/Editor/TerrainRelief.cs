using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Рельеф террейна по стандартам Synty.
    ///
    /// <b>Числа сняты с их собранных сцен</b>, а не подобраны:
    /// перепад высот держится в 10–17% от стороны участка, средняя крутизна
    /// 13–15°, размер форм 8–14 метров. Обрывы у автора делаются НЕ рельефом,
    /// а моделями (SM_Env_Dirt_Cliff_01..12) — поэтому здесь мы лепим только
    /// пологие валы, без стен.
    ///
    /// Берём середину вилки: перепад 12.5% от игровой площадки (160 м) —
    /// это 20 метров, формы по 12 метров.
    ///
    /// <b>Три слоя шума, а не один.</b> Один слой даёт правильную по числам,
    /// но неживую волну — одинаковые холмы через равные промежутки. Крупный
    /// слой задаёт общий наклон местности, средний лепит холмы, мелкий
    /// снимает «пластилиновость» с их склонов.
    /// </summary>
    public static class TerrainRelief
    {
        /// <summary>Перепад высот, метров. 12.5% от игровой площадки в 160 м.</summary>
        private const float Range = 20f;

        /// <summary>Размер основных форм, метров — от гребня до гребня.</summary>
        private const float Feature = 17f;

        /// <summary>Радиус ровной площадки вокруг начала координат, метров.</summary>
        private const float FlatRadius = 14f;

        /// <summary>Ширина сглаживания на краю ровной площадки, метров.</summary>
        private const float FlatFade = 10f;

        public static void Build()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — лепить не на чем.");
                return;
            }

            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            float side = data.size.x;

            // Частота слоя = сколько раз форма укладывается по стороне.
            float fBig = side / (Feature * 3.5f);   // общий наклон местности
            float fMid = side / Feature;            // сами холмы
            float fFine = side / (Feature * 0.9f);  // шероховатость склонов

            // Смещения, чтобы слои не совпадали гребнями: совпавшие гребни
            // дают правильные по числам, но неправдоподобно ровные валы.
            const float ox = 137.3f, oy = 411.7f;

            var h = new float[res, res];

            float min = float.MaxValue, max = float.MinValue;

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1);

                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);

                    float n =
                        Mathf.PerlinNoise(u * fBig + ox, v * fBig + oy) * 0.74f +
                        Mathf.PerlinNoise(u * fMid + ox * 2f, v * fMid + oy * 2f) * 0.22f +
                        Mathf.PerlinNoise(u * fFine + ox * 3f, v * fFine + oy * 3f) * 0.04f;

                    h[y, x] = n;

                    if (n < min) min = n;
                    if (n > max) max = n;
                }
            }

            // Приводим к заданному перепаду. Высота карты у террейна своя,
            // поэтому считаем долю от неё, а не абсолютные метры.
            float span = Mathf.Max(0.0001f, max - min);
            float amp = Range / data.size.y;

            // Центр площадки в долях карты — там стоит герой.
            float cx = (0f - terrain.transform.position.x) / side;
            float cz = (0f - terrain.transform.position.z) / data.size.z;

            float flatR = FlatRadius / side;
            float fadeR = (FlatRadius + FlatFade) / side;

            // Высота ровной площадки — та, что получилась в её центре.
            int ci = Mathf.Clamp(Mathf.RoundToInt(cz * (res - 1)), 0, res - 1);
            int cj = Mathf.Clamp(Mathf.RoundToInt(cx * (res - 1)), 0, res - 1);
            float centre = (h[ci, cj] - min) / span;

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1);

                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);

                    float value = (h[y, x] - min) / span;

                    // Ровная площадка под героем: постройки и стартовая
                    // сцена на склоне повисают углами, и это видно сразу.
                    float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cz) * (v - cz));

                    if (d < fadeR)
                    {
                        float k = d <= flatR ? 0f
                            : Mathf.SmoothStep(0f, 1f, (d - flatR) / (fadeR - flatR));

                        value = Mathf.Lerp(centre, value, k);
                    }

                    h[y, x] = value * amp;
                }
            }

            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            Snap(terrain);
            Report(terrain);
            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Поднять на поверхность всех, кто стоял на плоской земле.
        ///
        /// Рельеф меняет высоту грунта, а объекты остаются на прежней —
        /// и герой оказывается ПОД землёй. Провалившегося персонажа
        /// заказчик увидел первым же кадром, и это правильный симптом:
        /// «вылепил рельеф» и «мир остался рабочим» — разные утверждения.
        ///
        /// Двигаем всех с навигационным агентом (герой и монстры) и все
        /// корневые объекты, кроме служебных: света, камеры, неба и самого
        /// террейна — им высота грунта безразлична.
        /// </summary>
        private static void Snap(Terrain terrain)
        {
            var scene = EditorSceneManager.GetActiveScene();
            int moved = 0;

            string[] skip = { "sun", "camera", "eventsystem", "небо", "terrain",
                              "ground", "wind", "луг", "лес" };

            foreach (var root in scene.GetRootGameObjects())
            {
                string n = root.name.ToLowerInvariant();
                if (skip.Any(s => n.Contains(s))) continue;

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    bool stands = t.GetComponent<UnityEngine.AI.NavMeshAgent>() != null
                                  || t.parent == null || t.parent == root.transform;

                    if (!stands) continue;

                    var p = t.position;
                    float ground = terrain.SampleHeight(p) + terrain.transform.position.y;

                    // Двигаем только тех, кто оказался НИЖЕ земли или
                    // висит над ней выше полуметра.
                    if (p.y > ground - 0.05f && p.y < ground + 0.5f) continue;

                    t.position = new Vector3(p.x, ground, p.z);
                    EditorUtility.SetDirty(t);
                    moved++;
                }
            }

            Debug.Log("[IsoRPG] Поднято на поверхность объектов: " + moved + ".");
        }

        /// <summary>Плоский лист обратно — если рельеф не понравится.</summary>
        public static void Flatten()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null) return;

            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            data.SetHeights(0, 0, new float[res, res]);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Террейн выровнен в плоский лист.");
        }

        /// <summary>Отчитываемся теми же числами, которыми мерили автора.</summary>
        private static void Report(Terrain terrain)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] h = data.GetHeights(0, 0, res, res);

            float min = 1f, max = 0f;

            foreach (float v in h)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float slopeSum = 0f, slopeMax = 0f;
            int taken = 0;

            for (int i = 1; i < 64; i++)
            {
                for (int j = 1; j < 64; j++)
                {
                    float s = data.GetSteepness(i / 64f, j / 64f);
                    slopeSum += s;
                    if (s > slopeMax) slopeMax = s;
                    taken++;
                }
            }

            Debug.Log("[IsoRPG] Рельеф вылеплен: перепад " +
                      ((max - min) * data.size.y).ToString("0.0") + " м, " +
                      "крутизна средняя " + (slopeSum / taken).ToString("0.0") +
                      "°, наибольшая " + slopeMax.ToString("0.0") +
                      "°. Ориентир Synty: перепад 10–17% стороны, крутизна 13–15°.");
        }
    }
}
