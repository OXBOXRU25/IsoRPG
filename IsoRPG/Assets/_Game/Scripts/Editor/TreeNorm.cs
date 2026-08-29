using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Норматив посадки: насколько автор набора топит дерево в землю.
    ///
    /// Своя посадка «нижней точкой на грунт» дала деревья, стоящие НА земле
    /// корнями наружу, — заказчик увидел это сразу. Причина в том, что у
    /// этих моделей нижняя точка — кончики корней, а корни по замыслу
    /// закопаны. Сколько именно закопать, из геометрии не выводится: это
    /// решение художника.
    ///
    /// А оно у нас есть готовое. В наборах лежат демо-сцены, где те же самые
    /// префабы расставлены руками автора. Щуп открывает их, находит наши
    /// виды и меряет разницу между низом модели и землёй под ней. Отношение
    /// к высоте дерева и есть переносимый норматив.
    /// </summary>
    public static class TreeNorm
    {
        private static readonly string[] Scenes =
        {
            "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_URP.unity",
            "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_01.unity",
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity",
        };

        private static readonly string[] Kinds =
        {
            "SM_Env_Tree_Giant_01", "SM_Env_Tree_Giant_02",
            "SM_Env_Tree_Large_01", "SM_Env_Tree_Large_02",
            "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Birch_01",
        };

        [MenuItem("Tools/IsoRPG/Щуп: норматив посадки деревьев", priority = 53)]
        public static void Measure()
        {
            // Копим по видам через все сцены: одного экземпляра мало, автор
            // сажает по-разному на ровном месте и на склоне.
            var sunk = new Dictionary<string, List<float>>();
            var share = new Dictionary<string, List<float>>();
            var tall = new Dictionary<string, float>();

            foreach (var kind in Kinds)
            {
                sunk[kind] = new List<float>();
                share[kind] = new List<float>();
                tall[kind] = 0f;
            }

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.Log("[IsoRPG] Сцены нет, пропускаю: " + scenePath);
                    continue;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                var terrain = Object.FindObjectsByType<Terrain>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

                int found = 0;

                foreach (var go in Object.FindObjectsByType<GameObject>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    string kind = Kinds.FirstOrDefault(k =>
                        go.name.StartsWith(k, System.StringComparison.OrdinalIgnoreCase));

                    if (kind == null) continue;

                    // Берём только сам корень префаба: у дерева внутри есть
                    // узлы уровней детализации с теми же именами.
                    if (go.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                    if (go.transform.parent != null &&
                        Kinds.Any(k => go.transform.parent.name.StartsWith(
                            k, System.StringComparison.OrdinalIgnoreCase))) continue;

                    var box = Box(go);
                    float ground;

                    if (terrain != null)
                    {
                        ground = terrain.SampleHeight(go.transform.position) +
                                 terrain.transform.position.y;
                    }
                    else
                    {
                        // Земли террейном нет — ищем её лучом сверху вниз.
                        // Само дерево из расчёта выбрасываем: луч обязан
                        // найти грунт, а не ветку.
                        var self = new HashSet<Collider>(
                            go.GetComponentsInChildren<Collider>(true));

                        var hits = Physics.RaycastAll(
                            go.transform.position + Vector3.up * 60f,
                            Vector3.down, 300f);

                        var hit = hits.Where(h => !self.Contains(h.collider))
                                      .OrderBy(h => h.distance)
                                      .ToArray();

                        if (hit.Length == 0) continue;

                        ground = hit[0].point.y;
                    }

                    float deep = ground - box.min.y;      // >0 — низ модели ПОД землёй
                    float height = box.size.y;

                    if (height < 0.5f) continue;

                    sunk[kind].Add(deep);
                    share[kind].Add(deep / height);

                    if (height > tall[kind]) tall[kind] = height;

                    found++;
                }

                Debug.Log("[IsoRPG] " + Path.GetFileName(scenePath) + ": наших деревьев " + found);
            }

            Debug.Log("[IsoRPG] === НОРМАТИВ ПОСАДКИ (автор набора) ===");

            foreach (var kind in Kinds)
            {
                if (sunk[kind].Count == 0)
                {
                    Debug.Log("[IsoRPG] " + kind + ": в демо-сценах не встречается.");
                    continue;
                }

                var s = sunk[kind];
                float avg = s.Average();

                Debug.Log("[IsoRPG] " + kind + ": экземпляров " + s.Count +
                          ", утоплено в среднем " + avg.ToString("0.00") +
                          " м (от " + s.Min().ToString("0.00") +
                          " до " + s.Max().ToString("0.00") +
                          "), это " + (share[kind].Average() * 100f).ToString("0.0") +
                          "% высоты дерева " + tall[kind].ToString("0.0") + " м.");
            }
        }

        private static Bounds Box(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);

            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            var box = rs[0].bounds;
            foreach (var r in rs) box.Encapsulate(r.bounds);

            return box;
        }
    }
}
