using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Запускает в COZY цикл суток и задаёт его темп.
    ///
    /// Система приезжает со стандартным профилем времени, и время в нём идёт
    /// «как в жизни»: одна игровая минута за одну реальную секунду, то есть
    /// сутки за двадцать четыре минуты. Звучит близко к нашему, но проверять
    /// это вслепую нельзя — профиль общий для всего набора и мог быть
    /// поставлен на паузу.
    ///
    /// Темп считаем от желаемой длины суток, а не подбираем число.
    /// В сутках 1440 игровых минут; если хотим сутки за N реальных минут, то
    /// скорость = 1440 / (N * 60) игровых минут в реальную секунду.
    /// При N = 20 это 1.2 — и такое число уже нельзя случайно перепутать с
    /// «поставил единицу, вроде идёт».
    ///
    /// Стартовый час ставим на утро: игра начинается днём, и ночь должна
    /// приходить как событие, а не встречать игрока на первом же кадре.
    /// </summary>
    public static class CozyDayCycle
    {
        /// <summary>Сколько реальных минут длятся игровые сутки.</summary>
        private const float MinutesPerDay = 20f;

        /// <summary>Час, с которого начинается игра.</summary>
        private const int StartHour = 9;

        [MenuItem("Tools/IsoRPG/Небо COZY: запустить сутки", priority = 26)]
        public static void Apply()
        {
            // Модуль времени ищем отражением: типы COZY лежат в своей сборке,
            // и прямая ссылка привязала бы компиляцию проекта к набору.
            var module = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                               .FirstOrDefault(m => m != null &&
                                                    m.GetType().Name == "CozyTimeModule");

            if (module == null)
            {
                Debug.LogWarning("[IsoRPG] Модуль времени COZY в сцене не найден — " +
                                 "сначала включи COZY.");
                return;
            }

            var profileField = module.GetType().GetField("perennialProfile");
            var profile = profileField?.GetValue(module) as ScriptableObject;

            if (profile == null)
            {
                Debug.LogWarning("[IsoRPG] У модуля времени нет профиля.");
                return;
            }

            float speed = 1440f / (MinutesPerDay * 60f);

            Set(profile, "pauseTime", false);
            Set(profile, "timeMovementSpeed", speed);
            Set(profile, "progressDay", true);
            Set(profile, "resetTimeOnStart", true);

            // Час старта хранится своим типом MeridiemTime — собираем его
            // отражением, чтобы не тянуть ссылку на сборку COZY.
            var timeField = profile.GetType().GetField("startTime");

            if (timeField != null)
            {
                var t = System.Activator.CreateInstance(timeField.FieldType,
                                                        new object[] { StartHour, 0 });
                timeField.SetValue(profile, t);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[IsoRPG] Сутки запущены: " + MinutesPerDay +
                      " реальных минут на игровые сутки (скорость " +
                      speed.ToString("0.00") + " игровых минут в секунду), старт в " +
                      StartHour + ":00. Профиль: " + profile.name);
        }

        /// <summary>Ставит поле профиля, если оно есть. Молчит, если нет.</summary>
        private static void Set(ScriptableObject profile, string field, object value)
        {
            var f = profile.GetType().GetField(field);

            if (f == null)
            {
                Debug.LogWarning("[IsoRPG] В профиле нет поля " + field);
                return;
            }

            f.SetValue(profile, value);
        }
    }
}
