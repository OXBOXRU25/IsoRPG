using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IsoRPG.Player;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп сцены: выписывает в файл всё, что может давать жёлтое пятно и
    /// мерцание — крупные объекты, источники света, камеры, прозрачные и
    /// светящиеся материалы.
    ///
    /// Заведён после двух неверных догадок подряд. Жёлтый блок объяснялся
    /// сначала недоимпортом, потом потерянной прозрачностью; оба объяснения
    /// звучали убедительно и оба оказались мимо. Пока причина ищется
    /// рассуждением, каждая проверка стоит круга: собери, запусти, посмотри,
    /// опиши. Файл со списком стоит одного нажатия и отвечает на вопрос
    /// «что это вообще такое» напрямую.
    ///
    /// Пишет в shots/scene-probe.txt — папка не версионируется.
    /// </summary>
    public static class SceneProbe
    {
        /// <summary>С чего считаем объект крупным.</summary>
        private const float Big = 15f;

        [MenuItem("Tools/IsoRPG/Диагностика: что в сцене крупного и светящегося", priority = 48)]
        public static void Probe()
        {
            var report = new StringBuilder();

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                                                               FindObjectsSortMode.None);

            report.AppendLine("СЦЕНА: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            report.AppendLine("Рендереров всего: " + renderers.Length);
            report.AppendLine();

            // ---- крупные объекты -----------------------------------------
            report.AppendLine("=== КРУПНЕЕ " + Big + " м ===");

            var big = renderers
                .Where(r => r.bounds.size.magnitude > Big)
                .OrderByDescending(r => r.bounds.size.magnitude)
                .Take(40);

            foreach (var r in big)
            {
                var material = r.sharedMaterial;

                report.AppendLine(HierarchyPath(r.transform));
                report.AppendLine("    размер " + Round(r.bounds.size) +
                                  "   в точке " + Round(r.bounds.center));
                report.AppendLine("    материал " + (material == null ? "НЕТ" : material.name) +
                                  "   шейдер " + (material == null || material.shader == null
                                                  ? "НЕТ" : material.shader.name));

                if (material != null)
                {
                    report.AppendLine("    цвет " + Colour(material) +
                                      "   очередь " + material.renderQueue +
                                      "   текстура " + TextureName(material));
                }

                report.AppendLine("    тени " + r.shadowCastingMode);
                report.AppendLine();
            }

            // ---- что вокруг героя ----------------------------------------
            //
            // Главный раздел отчёта, и заведён он последним. Причину жёлтого
            // пятна я три раза выводил рассуждением и три раза мимо, хотя
            // ответ можно просто спросить: встать в него и перечислить, что
            // рядом. Нажимать надо в режиме Play, стоя внутри того, что
            // непонятно.
            var router = Object.FindFirstObjectByType<PlayerInputRouter>();

            report.AppendLine("=== ВОКРУГ ГЕРОЯ (25 м) ===");

            if (router == null)
            {
                report.AppendLine("Героя в сцене нет — запусти игру и нажми ещё раз.");
            }
            else
            {
                Vector3 at = router.transform.position;
                report.AppendLine("Герой стоит в " + Round(at));
                report.AppendLine();

                var near = renderers
                    .Where(r => Vector3.Distance(r.bounds.center, at) < 25f)
                    .OrderByDescending(r => r.bounds.size.magnitude)
                    .Take(25);

                foreach (var r in near)
                {
                    var m = r.sharedMaterial;

                    report.AppendLine(HierarchyPath(r.transform));
                    report.AppendLine("    размер " + Round(r.bounds.size) +
                                      "   в " + Round(r.bounds.center) +
                                      "   за " + Vector3.Distance(r.bounds.center, at).ToString("0.0") + " м");
                    report.AppendLine("    материал " + (m == null ? "НЕТ" : m.name) +
                                      "   шейдер " + (m == null || m.shader == null ? "НЕТ" : m.shader.name) +
                                      "   цвет " + (m == null ? "нет" : Colour(m)) +
                                      "   текстура " + (m == null ? "нет" : TextureName(m)));
                }
            }

            report.AppendLine();

            // ---- жёлтое ---------------------------------------------------
            report.AppendLine("=== ЖЁЛТЫЕ И СВЕТЯЩИЕСЯ МАТЕРИАЛЫ В КАДРЕ ===");

            var seen = new HashSet<string>();

            foreach (var r in renderers)
            {
                foreach (var material in r.sharedMaterials)
                {
                    if (material == null) continue;
                    if (!seen.Add(material.name + r.gameObject.name)) continue;

                    if (!Yellowish(material)) continue;

                    report.AppendLine(HierarchyPath(r.transform) + "   [" + material.name + "]");
                    report.AppendLine("    шейдер " + (material.shader == null ? "НЕТ" : material.shader.name) +
                                      "   цвет " + Colour(material) +
                                      "   очередь " + material.renderQueue +
                                      "   размер " + Round(r.bounds.size));
                }
            }

            report.AppendLine();

            // ---- свет и камеры --------------------------------------------
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude,
                                                         FindObjectsSortMode.None);

            report.AppendLine("=== ИСТОЧНИКИ СВЕТА: " + lights.Length + " ===");

            foreach (var light in lights.Take(30))
                report.AppendLine(HierarchyPath(light.transform) + "   " + light.type +
                                  "   сила " + light.intensity.ToString("0.00") +
                                  "   тени " + light.shadows +
                                  "   дальность " + light.range.ToString("0.0"));

            report.AppendLine();

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                                                           FindObjectsSortMode.None);

            report.AppendLine("=== КАМЕРЫ: " + cameras.Length + " ===");

            foreach (var camera in cameras)
                report.AppendLine(HierarchyPath(camera.transform) +
                                  "   глубина " + camera.depth +
                                  "   очистка " + camera.clearFlags +
                                  "   дальность " + camera.farClipPlane.ToString("0") +
                                  "   цель " + (camera.targetTexture == null ? "экран" : camera.targetTexture.name));

            report.AppendLine();

            // ---- частицы ---------------------------------------------------
            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

            report.AppendLine("=== СИСТЕМЫ ЧАСТИЦ: " + particles.Length + " ===");

            foreach (var system in particles.Take(20))
                report.AppendLine(HierarchyPath(system.transform) + "   частиц " + system.main.maxParticles);

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "shots");
            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, "scene-probe.txt");
            File.WriteAllText(file, report.ToString());

            Debug.Log("[IsoRPG] Щуп записал отчёт: " + file + "\n" +
                      "Рендереров " + renderers.Length + ", светов " + lights.Length +
                      ", камер " + cameras.Length + ", систем частиц " + particles.Length);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Жёлтое или светящееся: базовый цвет с высокими красным и зелёным
        /// при низком синем, либо включённое свечение.
        ///
        /// Порог мягкий намеренно: задача не отобрать ровно жёлтые, а не
        /// пропустить виновника. Лишняя строка в отчёте стоит секунды
        /// чтения, пропущенная — ещё одного круга с заказчиком.
        /// </summary>
        private static bool Yellowish(Material material)
        {
            if (material.IsKeywordEnabled("_EMISSION")) return true;

            Color c = Colour(material, out bool has);
            if (!has) return false;

            return c.r > 0.55f && c.g > 0.45f && c.b < 0.4f;
        }

        private static Color Colour(Material material, out bool has)
        {
            has = true;

            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");

            has = false;
            return Color.white;
        }

        private static string Colour(Material material)
        {
            Color c = Colour(material, out bool has);

            if (!has) return "нет";

            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        private static string TextureName(Material material)
        {
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                return material.GetTexture("_BaseMap").name;

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
                return material.GetTexture("_MainTex").name;

            return "НЕТ";
        }

        private static string Round(Vector3 v) =>
            "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";

        /// <summary>Путь объекта в иерархии — по нему его сразу найти.</summary>
        private static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();

            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
