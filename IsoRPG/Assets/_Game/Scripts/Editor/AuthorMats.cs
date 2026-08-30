using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Чем покрашена сцена автора: перепись шейдеров по фактам.
    ///
    /// Розовый цвет в URP означает ровно одно — шейдер материала не
    /// поддерживается конвейером. Гадать, какой именно, бессмысленно:
    /// перепись занимает один прогон и называет виновных поимённо, вместе с
    /// числом объектов на каждом шейдере.
    ///
    /// Работает в ОТКРЫТОЙ сцене, поэтому в очередь ставится после
    /// «arena-open-author».
    /// </summary>
    public static class AuthorMats
    {
        [MenuItem("Tools/IsoRPG/Щуп: чем покрашена сцена", priority = 55)]
        public static void Report()
        {
            var byShader = new Dictionary<string, (int count, string sample)>();
            int noMaterial = 0;

            foreach (var r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) { noMaterial++; continue; }

                    string key = m.shader.name;

                    if (byShader.TryGetValue(key, out var had))
                        byShader[key] = (had.count + 1, had.sample);
                    else
                        byShader[key] = (1, r.gameObject.name + " / " + m.name);
                }
            }

            Debug.Log("[IsoRPG] === ЧЕМ ПОКРАШЕНА СЦЕНА ===");

            foreach (var pair in byShader.OrderByDescending(p => p.Value.count))
            {
                bool urp = pair.Key.StartsWith("Universal Render Pipeline") ||
                           pair.Key.StartsWith("Shader Graphs") ||
                           pair.Key.StartsWith("Skybox");

                Debug.Log("[IsoRPG] " + (urp ? "  " : "НЕ URP ") + pair.Key +
                          ": объектов " + pair.Value.count +
                          ", например «" + pair.Value.sample + "»");
            }

            if (noMaterial > 0)
                Debug.LogWarning("[IsoRPG] Без материала или без шейдера: " + noMaterial);

            // Поимённо — то, на что показал заказчик.
            string[] wanted = { "wheel", "mill", "backdrop", "mountain", "sky", "dome", "cloud" };

            Debug.Log("[IsoRPG] === ОБЪЕКТЫ, НА КОТОРЫЕ ПОКАЗАЛИ ===");

            foreach (var r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = r.gameObject.name.ToLowerInvariant();

                if (!wanted.Any(w => n.Contains(w))) continue;

                var box = r.bounds;

                Debug.Log("[IsoRPG] «" + r.gameObject.name + "»: шейдер " +
                          (r.sharedMaterial != null && r.sharedMaterial.shader != null
                              ? r.sharedMaterial.shader.name : "НЕТ") +
                          ", материал " +
                          (r.sharedMaterial != null ? r.sharedMaterial.name : "НЕТ") +
                          ", размер " + box.size.x.ToString("0") + " x " +
                          box.size.y.ToString("0") + " x " + box.size.z.ToString("0") +
                          " м, в точке " + r.transform.position);
            }

            // --- Цветы: со стеблями или плоские --------------------------
            //
            // Вопрос заказчика по кадру: у цветов не видно стеблей. В наборе
            // есть оба рода — «Flowers_Flat» это лепестки, лежащие на земле,
            // а «Wildflowers» стоят на стеблях. Считаем, чего и сколько
            // поставил автор, и меряем высоту: стебель виден в числе.
            var flowers = new Dictionary<string, (int count, float tallest)>();

            foreach (var r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;

                if (n.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("Bloom", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Узлы уровней детализации не считаем дважды.
                if (n.Contains("_LOD") && !n.EndsWith("_LOD0")) continue;

                string kind = n.Split('(')[0].Replace("_LOD0", "").Trim();
                float h = r.bounds.size.y;

                if (flowers.TryGetValue(kind, out var had))
                    flowers[kind] = (had.count + 1, Mathf.Max(had.tallest, h));
                else
                    flowers[kind] = (1, h);
            }

            Debug.Log("[IsoRPG] === ЦВЕТЫ: СТЕБЛИ ЕСТЬ ИЛИ НЕТ ===");

            foreach (var pair in flowers.OrderByDescending(p => p.Value.count))
                Debug.Log("[IsoRPG] " + pair.Key + ": штук " + pair.Value.count +
                          ", высота " + pair.Value.tallest.ToString("0.00") +
                          " м — " + (pair.Value.tallest < 0.25f
                              ? "ПЛОСКИЙ, лежит на земле"
                              : "на стебле"));

            Debug.Log("[IsoRPG] Небо в настройках сцены: " +
                      (RenderSettings.skybox != null
                          ? RenderSettings.skybox.name + " на шейдере " +
                            RenderSettings.skybox.shader.name
                          : "НЕТ"));
        }
    }
}
