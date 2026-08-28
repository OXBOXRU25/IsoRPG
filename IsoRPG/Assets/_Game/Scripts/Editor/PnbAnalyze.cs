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
