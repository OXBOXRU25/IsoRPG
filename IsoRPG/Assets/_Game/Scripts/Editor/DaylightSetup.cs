using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Дневной свет по эталону автора набора.
    ///
    /// Числа не подобраны, а сняты: щуп прочитал освещение демо-сцены
    /// Fantasy Forest и нашей арены подряд, и разница оказалась не в оттенке,
    /// а в разах.
    ///
    /// <code>
    ///                     автор      было у нас
    ///   яркость солнца     2.8          0.95
    ///   высота солнца      62°          21°
    ///   рассеянный        Trilight     от неба
    ///   туман             с 23 м, exp²  с 45 м, линейный
    /// </code>
    ///
    /// Втрое слабее и почти у горизонта — вот и весь ответ на «почему темно».
    /// При наклоне в 21 градус свет скользит по земле, ничего не освещая
    /// сверху, а трава и кроны стоят в собственной тени.
    ///
    /// Берём эталон, но не копируем слепо — три поправки под нашу игру:
    ///
    ///   • <b>Яркость 2.4, а не 2.8.</b> У автора небо процедурное и тусклое,
    ///     у нас панорама Beautiful Sky, которая сама даёт много света.
    ///   • <b>Солнце на 52°, а не 62°.</b> Камера у нас смотрит сверху под 35
    ///     градусами; чем выше солнце, тем короче тени, а тени — единственное,
    ///     что даёт объём при взгляде сверху.
    ///   • <b>Цвет теплее нейтрального, но не оранжевый.</b> Прежний
    ///     (1, 0.79, 0.54) — это закатный свет; днём он красит траву в хаки.
    /// </summary>
    public static class DaylightSetup
    {
        [MenuItem("Tools/IsoRPG/Свет: дневной по эталону набора", priority = 21)]
        public static void Apply()
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                      FindObjectsSortMode.None)
                            .FirstOrDefault(l => l.type == LightType.Directional);

            if (sun == null)
            {
                Debug.LogWarning("[IsoRPG] Направленного света в сцене нет.");
                return;
            }

            sun.intensity = 2.4f;
            sun.color = new Color(1.000f, 0.945f, 0.855f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            // Высота 52 градуса, поворот 145 — солнце светит навстречу камере
            // чуть сбоку. При таком раскладе тени ложатся поперёк взгляда, и
            // рельеф читается; светящее в спину солнце убивает тени в кадре.
            sun.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

            // Рассеянный — трёхцветный, как у автора. От неба брать нельзя:
            // панорама у нас нарисованная, и движок вытягивает из неё
            // сине-серую муть, которая гасит зелень.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.55f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.31f, 0.26f);
            RenderSettings.ambientIntensity = 1.15f;

            // Туман экспоненциальный: линейный обрубает мир по линейке, и
            // край этой линейки видно как белую стену. Плотность подобрана
            // так, чтобы дальний край карты тонул, а лес в полусотне метров
            // ещё читался.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.fogColor = new Color(0.62f, 0.78f, 0.92f);

            DynamicGI.UpdateEnvironment();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[IsoRPG] Свет дневной: солнце " + sun.intensity +
                      " на высоте " + sun.transform.eulerAngles.x.ToString("0") +
                      "°, рассеянный Trilight " + RenderSettings.ambientIntensity +
                      ", туман exp² плотностью " + RenderSettings.fogDensity + ".");
        }
    }
}
