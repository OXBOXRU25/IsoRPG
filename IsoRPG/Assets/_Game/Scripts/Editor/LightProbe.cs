using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп освещения: почему темно и куда делись тени.
    ///
    /// Жалоба «свет исчез, теней нет» неразрешима на глаз — на картинке
    /// видно следствие, а причин у него шесть штук, и они в разных местах:
    /// направленный свет, его тени, рассеянный свет, небо, туман, настройки
    /// конвейера. Печатаем все шесть сразу, и дальше спорить не о чем.
    ///
    /// Написан после того, как я одной правкой погасил сцену: велел брать
    /// рассеянный свет от неба, а небо к тому моменту рисовалось чёрным.
    /// Причина была в двух шагах от следствия, и найти её глядя на экран
    /// нельзя.
    /// </summary>
    public static class LightProbe
    {
        [MenuItem("Tools/IsoRPG/Свет: что со сценой", priority = 73)]
        public static void Probe()
        {
            var report = new StringBuilder();

            report.AppendLine("СВЕТ В СЦЕНЕ");
            report.AppendLine();

            // ---- источники ------------------------------------------------
            var lights = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            report.AppendLine("Источников: " + lights.Length);

            foreach (var light in lights.Where(l => l.type == LightType.Directional))
            {
                report.AppendLine("  НАПРАВЛЕННЫЙ  " + light.name);
                report.AppendLine("      объект включён: " + light.gameObject.activeInHierarchy);
                report.AppendLine("      компонент включён: " + light.enabled);
                report.AppendLine("      яркость: " + light.intensity);
                report.AppendLine("      цвет: " + light.color);
                report.AppendLine("      тени: " + light.shadows +
                                  "   сила " + light.shadowStrength);
                report.AppendLine("      угол: " + light.transform.eulerAngles);
            }

            int points = lights.Count(l => l.type != LightType.Directional);
            report.AppendLine("  прочих источников: " + points);
            report.AppendLine();

            // ---- окружение ------------------------------------------------
            report.AppendLine("РАССЕЯННЫЙ СВЕТ");
            report.AppendLine("  режим: " + RenderSettings.ambientMode);
            report.AppendLine("  плоский цвет: " + RenderSettings.ambientLight);
            report.AppendLine("  верх/середина/низ: " + RenderSettings.ambientSkyColor +
                              " / " + RenderSettings.ambientEquatorColor +
                              " / " + RenderSettings.ambientGroundColor);
            report.AppendLine("  сила: " + RenderSettings.ambientIntensity);
            report.AppendLine();

            var sky = RenderSettings.skybox;

            report.AppendLine("НЕБО");
            report.AppendLine("  материал: " + (sky == null ? "НЕТ" : sky.name));
            report.AppendLine("  шейдер: " + (sky == null || sky.shader == null
                                              ? "нет"
                                              : sky.shader.name));
            // Проверка по имени шейдера, и она нарочно нестрогая.
            //
            // Первая версия требовала ровно `Skybox/...` — и объявила
            // негодным небо COZY, у которого шейдер зовётся
            // `Distant Lands/Cozy/URP/Stylized Sky Desktop`. Это как раз
            // настоящее небо. Слово «Sky» в имени — признак слабый, но
            // здесь он честнее строгого правила: щуп, который врёт, хуже
            // отсутствующего.
            bool looksLikeSky = sky != null && sky.shader != null &&
                                (sky.shader.name.StartsWith("Skybox/") ||
                                 sky.shader.name.Contains("Sky"));

            report.AppendLine("  похоже на небо: " + (looksLikeSky ? "да" : "НЕТ"));

            var camera = Camera.main;

            report.AppendLine("  камера чистит: " +
                              (camera == null ? "камеры нет" : camera.clearFlags.ToString()));
            report.AppendLine();

            report.AppendLine("ТУМАН");
            report.AppendLine("  включён: " + RenderSettings.fog +
                              "   режим " + RenderSettings.fogMode);
            report.AppendLine("  цвет: " + RenderSettings.fogColor);
            report.AppendLine("  от/до: " + RenderSettings.fogStartDistance +
                              " / " + RenderSettings.fogEndDistance);
            report.AppendLine();

            // ---- конвейер --------------------------------------------------
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            report.AppendLine("КОНВЕЙЕР");

            if (pipeline == null)
            {
                report.AppendLine("  URP-ассет не назначен — это само по себе беда.");
            }
            else
            {
                report.AppendLine("  ассет: " + pipeline.name);
                report.AppendLine("  тени направленного света: " + pipeline.supportsMainLightShadows);
                report.AppendLine("  дальность теней: " + pipeline.shadowDistance);
                report.AppendLine("  каскадов: " + pipeline.shadowCascadeCount);
                report.AppendLine("  доли каскадов: " + pipeline.cascade2Split + " | " +
                                  pipeline.cascade3Split + " | " + pipeline.cascade4Split);
                report.AppendLine("  масштаб отрисовки: " + pipeline.renderScale);
                report.AppendLine("  сглаживание: " + pipeline.msaaSampleCount + "x");
            }

            report.AppendLine();
            report.AppendLine("ЧТО РИСУЕТ ТЕНИ");
            report.AppendLine("  Тень на земле требует ТРЁХ вещей сразу: у света");
            report.AppendLine("  shadows не None, у конвейера supportsMainLightShadows,");
            report.AppendLine("  и объект в пределах дальности теней. Пропала хоть одна —");
            report.AppendLine("  теней нет вовсе, и по картинке не понять, какая именно.");

            Debug.Log("[IsoRPG]\n" + report);
        }
    }
}
