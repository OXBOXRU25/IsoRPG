using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Точка входа для запуска редактора без окна.
    ///
    /// Пока Unity открыт у человека, он держит проект монопольно и снаружи
    /// им не поуправлять. Но если редактор закрыт, ассистент запускает его
    /// сам:
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;проект&gt;
    ///             -executeMethod IsoRPG.EditorTools.BatchRunner.Run
    ///             -logFile &lt;лог&gt;
    ///
    /// и выполняет очередь заданий из `_pending-tasks.txt` — те же самые,
    /// что и пункты меню. Человек в это время свободен: собирается сцена,
    /// пекутся замеры, вытаскиваются префабы.
    ///
    /// Сцену открываем сами и явно. В пакетном режиме открытой сцены нет
    /// вовсе, а почти каждое задание работает со сценой — без этого они
    /// молча ничего бы не находили.
    /// </summary>
    public static class BatchRunner
    {
        private const string Old = "Assets/_Game/Scenes/Sandbox.unity";

        /// <summary>
        /// Какую сцену открывать. Арену, как только она появилась.
        ///
        /// Старая песочница остаётся на диске нетронутой — она наш откат и
        /// заодно склад того, что придётся перенести. Но работать по
        /// умолчанию надо в новой: задание, отработавшее не в той сцене,
        /// выглядит успешным и не меняет ничего.
        /// </summary>
        private static string Scene =>
            System.IO.File.Exists(ArenaBuilder.ScenePath) ? ArenaBuilder.ScenePath : Old;

        public static void Run()
        {
            Debug.Log("[IsoRPG] Пакетный запуск: начинаю.");

            try
            {
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                Debug.Log("[IsoRPG] Открыта сцена " + Scene);
            }
            catch (Exception e)
            {
                Debug.LogError("[IsoRPG] Не открылась сцена: " + e.Message);
            }

            PendingTasks.RunNow();

            // Сохраняем обязательно: в пакетном режиме никто не спросит
            // «сохранить изменения?», и вся работа осталась бы в памяти
            // процесса, который сейчас закроется.
            try
            {
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                Debug.Log("[IsoRPG] Сцена и ассеты сохранены.");
            }
            catch (Exception e)
            {
                Debug.LogError("[IsoRPG] Не сохранилось: " + e.Message);
            }

            Debug.Log("[IsoRPG] Пакетный запуск: готово.");
        }
    }
}
