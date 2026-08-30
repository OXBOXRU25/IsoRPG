using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Чем автор освещает свою сцену: свет, постобработка, лучи.
    ///
    /// При переносе мира я выбросил его светильники — иначе в сцене два
    /// солнца и пересвет. Но вместе с ними ушли и его настройки: цвет,
    /// наклон, сила, туман, тени. Разница в цвете картинки идёт отсюда, и
    /// прежде чем что-то подбирать, надо прочитать его числа.
    /// </summary>
    public static class AuthorLight
    {
        private const string Demo =
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

        /// <summary>
        /// Поставить в копию арены СВЕТ АВТОРА вместо нашего.
        ///
        /// При переносе мира я выбросил его светильники, чтобы не получить
        /// два солнца, — и оставил наше. Это неверно: переносим его мир,
        /// значит и свет должен быть его, а гасить надо было наш.
        ///
        /// Настройки окружения (рассеянный свет, туман, небо) живут В СЦЕНЕ,
        /// а не в объектах, поэтому вместе со сценой они не переехали. Их
        /// читаем из демо и переносим числами.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Мир автора: поставить его свет", priority = 32)]
        public static void Adopt()
        {
            const string arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

            if (!File.Exists(Demo) || !File.Exists(arena))
            {
                Debug.LogError("[IsoRPG] Нет демо-сцены или копии арены.");
                return;
            }

            // --- ЧИТАЕМ у автора -------------------------------------------
            EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single);

            var sun = Object.FindObjectsByType<Light>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                      .Where(l => l.type == LightType.Directional)
                      .OrderByDescending(l => l.intensity)
                      .FirstOrDefault();

            if (sun == null)
            {
                Debug.LogError("[IsoRPG] У автора не нашлось направленного света.");
                return;
            }

            var sunColor = sun.color;
            float sunPower = sun.intensity;
            var sunAngles = sun.transform.rotation.eulerAngles;
            var sunShadows = sun.shadows;
            float sunShadowStrength = sun.shadowStrength;
            float sunBounce = sun.bounceIntensity;

            var ambMode = RenderSettings.ambientMode;
            var ambSky = RenderSettings.ambientSkyColor;
            var ambEq = RenderSettings.ambientEquatorColor;
            var ambGround = RenderSettings.ambientGroundColor;
            float ambPower = RenderSettings.ambientIntensity;
            var ambLight = RenderSettings.ambientLight;

            bool fog = RenderSettings.fog;
            var fogColor = RenderSettings.fogColor;
            var fogMode = RenderSettings.fogMode;
            float fogDensity = RenderSettings.fogDensity;
            float fogStart = RenderSettings.fogStartDistance;
            float fogEnd = RenderSettings.fogEndDistance;

            var skybox = RenderSettings.skybox;

            Debug.Log("[IsoRPG] Свет автора прочитан: цвет #" +
                      ColorUtility.ToHtmlStringRGB(sunColor) +
                      ", сила " + sunPower.ToString("0.00") +
                      ", наклон " + (sunAngles.x > 180f ? sunAngles.x - 360f : sunAngles.x)
                          .ToString("0") +
                      "°, поворот " + sunAngles.y.ToString("0") +
                      "°, тени " + sunShadows +
                      ". Туман " + (fog ? "есть" : "нет") + ".");

            // --- СТАВИМ в копию арены --------------------------------------
            var scene = EditorSceneManager.OpenScene(arena, OpenSceneMode.Single);

            int ours = 0;

            foreach (var l in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;

                l.gameObject.SetActive(false);
                ours++;
            }

            var go = new GameObject("Солнце автора");
            var made = go.AddComponent<Light>();

            made.type = LightType.Directional;
            made.color = sunColor;
            made.intensity = sunPower;
            made.shadows = sunShadows;
            made.shadowStrength = sunShadowStrength;
            made.bounceIntensity = sunBounce;
            go.transform.rotation = Quaternion.Euler(sunAngles);

            RenderSettings.ambientMode = ambMode;
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;
            RenderSettings.ambientIntensity = ambPower;
            RenderSettings.ambientLight = ambLight;

            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (skybox != null) RenderSettings.skybox = skybox;

            RenderSettings.sun = made;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[IsoRPG] Свет автора поставлен: наших солнц погашено " + ours +
                      ", перенесены рассеянный свет, туман и небо. Небо в сцене: " +
                      (skybox != null ? skybox.name : "как было") + ".");
        }

        [MenuItem("Tools/IsoRPG/Щуп: свет автора", priority = 57)]
        public static void Report()
        {
            if (!File.Exists(Demo))
            {
                Debug.LogError("[IsoRPG] Демо-сцены нет: " + Demo);
                return;
            }

            EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single);

            Debug.Log("[IsoRPG] === СВЕТ АВТОРА ===");

            foreach (var l in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var e = l.transform.rotation.eulerAngles;

                Debug.Log("[IsoRPG] «" + l.gameObject.name + "»: тип " + l.type +
                          ", цвет " + ColorUtility.ToHtmlStringRGB(l.color) +
                          ", сила " + l.intensity.ToString("0.00") +
                          ", тени " + l.shadows +
                          ", сила тени " + l.shadowStrength.ToString("0.00") +
                          ", наклон над горизонтом " + (e.x > 180f ? e.x - 360f : e.x).ToString("0") +
                          "°, поворот " + e.y.ToString("0") + "°" +
                          (l.gameObject.activeInHierarchy ? "" : "  (ВЫКЛЮЧЕН)"));
            }

            Debug.Log("[IsoRPG] Рассеянный свет: режим " + RenderSettings.ambientMode +
                      ", цвет неба " + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor) +
                      ", горизонта " + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor) +
                      ", земли " + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor) +
                      ", сила " + RenderSettings.ambientIntensity.ToString("0.00"));

            Debug.Log("[IsoRPG] Туман: " + (RenderSettings.fog ? "ЕСТЬ" : "нет") +
                      (RenderSettings.fog
                          ? ", цвет " + ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor) +
                            ", режим " + RenderSettings.fogMode +
                            ", плотность " + RenderSettings.fogDensity.ToString("0.0000") +
                            ", от " + RenderSettings.fogStartDistance.ToString("0") +
                            " до " + RenderSettings.fogEndDistance.ToString("0") + " м"
                          : ""));

            // Постобработка: свечение, тонирование, виньетка. Именно она чаще
            // всего и даёт «тот самый цвет», которого не добиться светом.
            var volumes = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (volumes.Length == 0)
                Debug.Log("[IsoRPG] Постобработки в сцене автора НЕТ.");

            foreach (var v in volumes)
            {
                string profile = v.sharedProfile != null ? v.sharedProfile.name : "без набора";

                Debug.Log("[IsoRPG] Постобработка «" + v.gameObject.name + "»: набор " + profile +
                          ", на всю сцену " + v.isGlobal + ", вес " + v.weight.ToString("0.00"));

                if (v.sharedProfile == null) continue;

                foreach (var comp in v.sharedProfile.components)
                    Debug.Log("[IsoRPG]     эффект: " + comp.GetType().Name +
                              (comp.active ? "" : " (выключен)"));
            }

            // Лучи и блики — отдельными объектами: у Synty это плоскости с
            // прозрачной текстурой, а не эффект конвейера.
            var flares = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                         .Where(g => g.name.IndexOf("flare", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     g.name.IndexOf("ray", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     g.name.IndexOf("shaft", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     g.name.IndexOf("sun", System.StringComparison.OrdinalIgnoreCase) >= 0)
                         .ToArray();

            Debug.Log("[IsoRPG] Лучей, бликов и солнц отдельными объектами: " + flares.Length);

            foreach (var f in flares.Take(10))
                Debug.Log("[IsoRPG]     «" + f.name + "» в точке " + f.transform.position);
        }
    }
}
