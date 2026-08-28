using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Разбор собранной сцены автора биома на числа.
    ///
    /// <b>Зачем.</b> Я сеял растительность по выдуманным числам — плотность,
    /// размер, посадка в землю — и промахивался каждый раз. У Synty лежит
    /// готовая сцена Demo_URP, где все эти решения уже приняты художником.
    /// Заказчик сказал прямо: «разве в биомах нет настроек сцены? размерный
    /// ряд, насколько объект утоплен, какой плотностью стоят? уже готовые
    /// собранные сцены?» — и он прав, они есть.
    ///
    /// Печатаем по каждому виду: сколько штук, какой разброс масштаба,
    /// насколько низ объекта утоплен относительно земли под ним, и плотность
    /// на 100 м² по занятой площади.
    /// </summary>
    public static class PnbAnalyze
    {
        private const string Demo =
            "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_URP.unity";

        /// <summary>
        /// Как у автора устроено небо: купол, облака, солнце и что их
        /// оживляет. Смотрим в демо лугового леса — только там автор
        /// поставил облачные кольца.
        /// </summary>
        public static void Sky()
        {
            EditorSceneManager.OpenScene(
                "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity",
                OpenSceneMode.Single);

            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name.ToLowerInvariant();
                bool sky = n.Contains("cloud") || n.Contains("sky") || n.Contains("dome");
                if (!sky) continue;

                var r = t.GetComponent<Renderer>();

                var extra = t.GetComponents<Component>()
                             .Where(c => c != null && !(c is Transform) &&
                                         !(c is Renderer) && !(c is MeshFilter))
                             .Select(c => c.GetType().Name);

                Debug.Log("[IsoRPG] НЕБО-ДЕМО «" + t.name +
                          "»: позиция " + t.position +
                          ", масштаб " + t.lossyScale +
                          ", поворот " + t.eulerAngles +
                          ", материал " + (r == null || r.sharedMaterial == null
                              ? "нет" : r.sharedMaterial.name) +
                          ", компоненты: " +
                          (extra.Any() ? string.Join(", ", extra) : "нет"));
            }

            foreach (var l in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Debug.Log("[IsoRPG] НЕБО-ДЕМО свет «" + l.name + "»: тип " + l.type +
                          ", поворот " + l.transform.eulerAngles +
                          ", цвет " + l.color + ", яркость " + l.intensity +
                          ", тени " + l.shadows);
            }

            Debug.Log("[IsoRPG] НЕБО-ДЕМО настройки: skybox " +
                      (RenderSettings.skybox == null ? "нет" : RenderSettings.skybox.name) +
                      ", рассеянный " + RenderSettings.ambientMode +
                      ", туман " + RenderSettings.fog +
                      (RenderSettings.fog ? " цвет " + RenderSettings.fogColor +
                       " режим " + RenderSettings.fogMode : ""));
        }

        /// <summary>
        /// Разбор демо лугового леса: там трава, цветы и грибы — то, чего
        /// нет в заколдованном лесу.
        /// </summary>
        public static void Meadow()
        {
            Analyze("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity");
        }

        /// <summary>
        /// Рельеф террейна числами: перепад высот, крутизна, размер форм.
        ///
        /// «Холмистый» и «ровный» — не числа. Числа это: какую долю от
        /// стороны участка занимает перепад высот, какая средняя и
        /// максимальная крутизна склона, и какого размера сами формы —
        /// пологие валы или частая рябь. По ним рельеф воспроизводится, а
        /// на глаз — нет.
        /// </summary>
        private static void Relief(Terrain terrain)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            float[,] h = data.GetHeights(0, 0, res, res);

            float min = 1f, max = 0f;
            double sum = 0;

            foreach (float v in h)
            {
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }

            float range = (max - min) * data.size.y;

            // Крутизна: берём сеткой 64x64, чтобы не считать миллион точек.
            float slopeSum = 0f, slopeMax = 0f;
            int taken = 0;

            for (int i = 1; i < 64; i++)
            {
                for (int j = 1; j < 64; j++)
                {
                    float sx = i / 64f, sy = j / 64f;
                    float s = data.GetSteepness(sx, sy);

                    slopeSum += s;
                    if (s > slopeMax) slopeMax = s;
                    taken++;
                }
            }

            // Размер форм: считаем, сколько раз профиль по середине меняет
            // направление. Много смен — мелкая рябь, мало — пологие валы.
            int turns = 0;
            float prev = h[res / 2, 1] - h[res / 2, 0];

            for (int i = 2; i < res; i++)
            {
                float d = h[res / 2, i] - h[res / 2, i - 1];
                if (Mathf.Sign(d) != Mathf.Sign(prev) && Mathf.Abs(d) > 1e-5f) turns++;
                prev = d;
            }

            float featureSize = turns > 0 ? data.size.x / turns : data.size.x;

            Debug.Log("[IsoRPG] РЕЛЬЕФ: участок " + data.size.x.ToString("0") + " м, " +
                      "высота карты " + data.size.y.ToString("0") + " м; " +
                      "перепад " + range.ToString("0.0") + " м = " +
                      (range / data.size.x * 100f).ToString("0.0") + "% от стороны; " +
                      "крутизна средняя " + (slopeSum / taken).ToString("0.0") +
                      "°, наибольшая " + slopeMax.ToString("0.0") + "°; " +
                      "размер форм ~" + featureSize.ToString("0.0") + " м; " +
                      "разрешение карты высот " + res + ".");
        }

        /// <summary>
        /// Слои земли и какую долю участка каждый занимает.
        ///
        /// Тропинки у Synty — не объекты, а слой террейна. Значит вопрос
        /// «как автор делает тропы» сводится к числам: какой слой, какую
        /// долю карты он покрывает, какой шириной идёт.
        /// </summary>
        private static void Layers(Terrain terrain)
        {
            var data = terrain.terrainData;
            var layers = data.terrainLayers;

            if (layers == null || layers.Length == 0)
            {
                Debug.Log("[IsoRPG] ДЕМО: слоёв земли нет.");
                return;
            }

            int res = data.alphamapResolution;
            float[,,] map = data.GetAlphamaps(0, 0, res, res);

            for (int i = 0; i < layers.Length; i++)
            {
                double sum = 0;
                int strong = 0;

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float w = map[y, x, i];
                        sum += w;
                        if (w > 0.5f) strong++;
                    }
                }

                float cells = res * (float)res;

                Debug.Log("[IsoRPG] ДЕМО слой земли " + i + ": «" + layers[i].name +
                          "», доля по весу " + (sum / cells * 100.0).ToString("0.0") +
                          "%, преобладает на " + (strong / cells * 100f).ToString("0.0") +
                          "% участка, плитка " + layers[i].tileSize.x.ToString("0.0") + " м.");
            }
        }

        /// <summary>Вода в сцене автора: где, какого размера, каким материалом.</summary>
        private static void Water()
        {
            int found = 0;

            foreach (var r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                string mn = m == null ? "" : m.name.ToLowerInvariant();
                string on = r.name.ToLowerInvariant();

                if (!mn.Contains("water") && !on.Contains("water") &&
                    !mn.Contains("river") && !mn.Contains("lake")) continue;

                Debug.Log("[IsoRPG] ДЕМО вода «" + r.name +
                          "»: позиция " + r.transform.position +
                          ", масштаб " + r.transform.lossyScale +
                          ", материал " + (m == null ? "нет" : m.name) +
                          ", размер " + r.bounds.size.ToString("0.0"));
                found++;
            }

            if (found == 0) Debug.Log("[IsoRPG] ДЕМО: воды в сцене нет.");
        }

        /// <summary>
        /// Трава террейна: какие виды посеяны и с какой плотностью.
        ///
        /// Приземную траву Synty кладёт НЕ объектами, а слоем подлеска
        /// террейна — картой плотности. Поэтому в списке объектов её нет
        /// вовсе, и без этого замера кажется, что травы у автора не было.
        /// </summary>
        private static void Details(Terrain terrain)
        {
            var data = terrain.terrainData;
            var protos = data.detailPrototypes;

            if (protos == null || protos.Length == 0)
            {
                Debug.Log("[IsoRPG] ДЕМО: слоёв подлеска у террейна нет.");
                return;
            }

            int res = data.detailResolution;

            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];

                string what = p.prototype != null
                    ? "префаб «" + p.prototype.name + "»"
                    : (p.prototypeTexture != null
                        ? "текстура «" + p.prototypeTexture.name + "»" : "пусто");

                var layer = data.GetDetailLayer(0, 0, res, res, i);

                long sum = 0;
                int cells = 0;

                foreach (int v in layer) { sum += v; cells++; }

                Debug.Log("[IsoRPG] ДЕМО подлесок " + i + ": " + what +
                          ", вид рендера " + p.renderMode +
                          ", размер " + p.minWidth.ToString("0.0") + "-" +
                          p.maxWidth.ToString("0.0") + " x " +
                          p.minHeight.ToString("0.0") + "-" +
                          p.maxHeight.ToString("0.0") +
                          ", средняя плотность " + ((float)sum / cells).ToString("0.00") +
                          " на клетку, клеток " + cells + ".");
            }

            Debug.Log("[IsoRPG] ДЕМО: карта подлеска " + res + "x" + res +
                      ", плотность отрисовки " + terrain.detailObjectDensity +
                      ", дальность " + terrain.detailObjectDistance + " м.");
        }

        public static void Run() { Analyze(Demo); }

        private static void Analyze(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] В демо нет террейна.");
                return;
            }

            var size = terrain.terrainData.size;
            Debug.Log("[IsoRPG] ДЕМО: террейн " + size.x.ToString("0") + " x " +
                      size.z.ToString("0") + " м = " +
                      (size.x * size.z).ToString("0") + " м².");

            Relief(terrain);
            Layers(terrain);
            Water();
            Details(terrain);

            // Считаем только КОРНИ префабов: у каждого внутри LOD-узлы, и
            // считать их — значит считать одно растение по три-четыре раза.
            var roots = new Dictionary<string, List<Transform>>();

            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;

                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (src == null) continue;

                string key = src.name;
                if (!roots.TryGetValue(key, out var list))
                    roots[key] = list = new List<Transform>();

                list.Add(t);
            }

            float area = size.x * size.z / 100f;   // сотни квадратных метров

            foreach (var pair in roots.OrderByDescending(p => p.Value.Count).Take(25))
            {
                var items = pair.Value;

                float minS = items.Min(t => t.lossyScale.y);
                float maxS = items.Max(t => t.lossyScale.y);

                // Насколько низ объекта ниже земли под ним.
                var sunk = new List<float>();

                foreach (var t in items)
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;

                    var box = rs[0].bounds;
                    foreach (var r in rs) box.Encapsulate(r.bounds);

                    float ground = terrain.SampleHeight(t.position) +
                                   terrain.transform.position.y;

                    sunk.Add(ground - box.min.y);
                }

                string sunkText = sunk.Count == 0 ? "—" :
                    (sunk.Average().ToString("0.00") + " м (от " +
                     sunk.Min().ToString("0.00") + " до " + sunk.Max().ToString("0.00") + ")");

                Debug.Log("[IsoRPG] ДЕМО " + pair.Key +
                          ": штук " + items.Count +
                          ", плотность " + (items.Count / area).ToString("0.00") + " на 100 м²" +
                          ", масштаб " + minS.ToString("0.00") + "–" + maxS.ToString("0.00") +
                          ", утоплен " + sunkText);
            }

            Debug.Log("[IsoRPG] ДЕМО: всего видов префабов " + roots.Count +
                      ", экземпляров " + roots.Sum(p => p.Value.Count) + ".");
        }
    }
}
