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

        public static void Run()
        {
            EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single);

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
