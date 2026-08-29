using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит в сцену систему неба и погоды Enviro 3.
    ///
    /// Enviro забирает себе всё, что относится к атмосфере: солнце, луну,
    /// небо, туман, облака, рассеянный свет. Это не дополнение к нашим
    /// настройкам, а замена им — поэтому ручной свет и туман тут не «мешают»,
    /// а просто перестают действовать: система переписывает их каждый кадр.
    ///
    /// Своё направленное солнце гасим сразу. Два направленных света в сцене
    /// дают двойные тени под каждым деревом — беда заметная и совершенно
    /// непонятная на вид, если не знать, откуда она.
    ///
    /// COZY, который лежит у нас пакетом, в сцене не стоит — проверено по
    /// самому файлу сцены. Если он там появится, обе системы будут драться за
    /// одни и те же настройки, и победит та, что обновится последней; выглядит
    /// это как мигающее небо.
    /// </summary>
    public static class EnviroSetup
    {
        private const string PrefabPath =
            "Assets/Enviro 3 - Sky and Weather/Enviro 3.prefab";

        [MenuItem("Tools/IsoRPG/Небо Enviro: поставить в сцену", priority = 22)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var existing = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                                 .FirstOrDefault(g => g.name.StartsWith("Enviro"));

            if (existing != null)
            {
                Debug.Log("[IsoRPG] Enviro уже в сцене: " + existing.name);
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Не найден префаб " + PrefabPath);
                    return;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                Debug.Log("[IsoRPG] Enviro поставлен: " + go.name);
            }

            AssignConfiguration();

            // Старое небо снимаем явно.
            //
            // Панорама Beautiful Sky осталась в RenderSettings и продолжала
            // рисоваться поверх всего — я час доказывал, что вижу облака
            // Enviro, глядя на неё же. Две системы неба в сцене это тот же
            // конфликт, что COZY и Enviro, только тише: скайбокс не спорит,
            // он просто рисуется, а Enviro свой ставит уже в игре.
            //
            // Убрав его, мы получаем честный ответ на кадре: есть небо —
            // значит рисует Enviro, нет неба — значит он не работает.
            if (RenderSettings.skybox != null)
            {
                Debug.Log("[IsoRPG] Снял прежнее небо: " + RenderSettings.skybox.name);
                RenderSettings.skybox = null;
            }

            // Заодно снимаем купол, если он остался от старых заходов.
            foreach (var dome in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                if (dome != null && (dome.name == "SkyDome" || dome.name.StartsWith("SM_Env_Skydome")))
                {
                    Debug.Log("[IsoRPG] Убран купол неба: " + dome.name);
                    Object.DestroyImmediate(dome);
                }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            DynamicGI.UpdateEnvironment();

            // Гасим своё солнце: атмосферу теперь ведёт Enviro, а второй
            // направленный свет даст двойные тени.
            int off = 0;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;

                // Собственное солнце Enviro лежит внутри его же иерархии —
                // его не трогаем, иначе выключим то, что только что поставили.
                bool insideEnviro = light.transform.root != null &&
                                    light.transform.root.name.StartsWith("Enviro");

                if (insideEnviro) continue;

                light.enabled = false;
                off++;
            }

            if (off > 0)
                Debug.Log("[IsoRPG] Выключено своих направленных источников: " + off +
                          " (небом теперь управляет Enviro).");

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Выдаёт менеджеру конфигурацию.
        ///
        /// Без неё система стоит мёртвая: объект в сцене есть, компонент
        /// включён, а небо рисует прежний скайбокс из RenderSettings — и по
        /// картинке это неотличимо от «Enviro не поставили вовсе». Я на этом
        /// уже ошибся и объявил чужое облако объёмным.
        ///
        /// Поле ищем через отражение по имени «configuration»: тип лежит в
        /// своей сборке, и ссылаться на него из нашего кода значит завязать
        /// компиляцию проекта на присутствие Enviro.
        /// </summary>
        private static void AssignConfiguration()
        {
            const string configPath =
                "Assets/Enviro 3 - Sky and Weather/Profiles/Configurations/" +
                "Default Enviro Configuration.asset";

            var manager = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                                .FirstOrDefault(m => m != null &&
                                                     m.GetType().Name == "EnviroManager");

            if (manager == null)
            {
                Debug.LogWarning("[IsoRPG] EnviroManager в сцене не найден.");
                return;
            }

            var field = manager.GetType().GetField("configuration");

            if (field == null)
            {
                Debug.LogWarning("[IsoRPG] У менеджера нет поля configuration.");
                return;
            }

            if (field.GetValue(manager) != null)
            {
                Debug.Log("[IsoRPG] Конфигурация уже назначена.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<Object>(configPath);

            if (config == null)
            {
                Debug.LogWarning("[IsoRPG] Не найдена конфигурация " + configPath);
                return;
            }

            field.SetValue(manager, config);
            EditorUtility.SetDirty(manager);

            // Просим менеджера разобрать конфигурацию сразу: иначе модули
            // подхватятся только при запуске игры, и в редакторе мы снова
            // увидим старое небо и решим, что ничего не сработало.
            var load = manager.GetType().GetMethod("LoadConfiguration");
            if (load != null) load.Invoke(manager, null);

            Debug.Log("[IsoRPG] Конфигурация назначена: " + config.name);
        }

        /// <summary>
        /// Убирает Enviro и возвращает наш ручной свет.
        ///
        /// Нужен обязательно, и не как вежливость: система, которую нельзя
        /// снять одной кнопкой, превращает пробу в необратимое решение.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Небо Enviro: убрать", priority = 23)]
        public static void Remove()
        {
            var found = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .Where(g => g.name.StartsWith("Enviro") &&
                                          g.transform.parent == null)
                              .ToList();

            foreach (var go in found) Object.DestroyImmediate(go);

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.enabled = true;

            DaylightSetup.Apply();

            Debug.Log("[IsoRPG] Enviro убран (" + found.Count +
                      " объектов), вернул наш дневной свет.");
        }
    }
}
