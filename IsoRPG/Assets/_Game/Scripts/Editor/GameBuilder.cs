using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает игру в папку и складывает её в архив.
    ///
    /// Настоящего установщика не делаем, и это не упрощение: инди-игры так и
    /// раздают — папкой с исполняемым файлом. Установщик требует отдельной
    /// программы (NSIS, Inno Setup), подписи и обновлений, а даёт ровно одно —
    /// ярлык в меню «Пуск». Архив распаковывается куда угодно и удаляется
    /// целиком, ничего не оставляя в системе.
    ///
    /// Архив собираем через .NET напрямую, а не Compress-Archive: тот пишет в
    /// пути обратные слеши, и на macOS такой архив разворачивается кашей из
    /// файлов с именами вида «папка\файл».
    /// </summary>
    public static class GameBuilder
    {
        private const string ProductName = "Птица высокого полёта";
        private const string ExecutableName = "HighFlyingBird";

        /// <summary>Куда кладём сборку. Рядом с проектом, а не внутри него.</summary>
        private static string BuildRoot =>
            Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "Build");

        [MenuItem("Tools/IsoRPG/Собрать игру (Windows)", priority = 2)]
        public static void BuildGame()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "Сборка требует остановленной игры.", "Понятно");
                return;
            }

            var scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Нет сцен",
                    "В настройках сборки пусто. Сначала собери главное меню — " +
                    "оно само добавит обе сцены.", "Понятно");
                return;
            }

            // Меню первым: с какой сцены начинается игра, решает порядок в
            // списке, а не имя файла.
            if (!scenes[0].Contains("MainMenu"))
                Debug.LogWarning("[IsoRPG] Первой в сборке идёт не MainMenu — " +
                                 "игра запустится сразу в песочнице.");

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = "OXBOX";

            // Полноэкранное окно, а не эксклюзивный полный экран: у второго
            // ломается переключение по Alt+Tab, а выигрыша на нашей картинке
            // никакого.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;

            string folder = Path.Combine(BuildRoot, ExecutableName);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExecutableName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[IsoRPG] Сборка не удалась: " + summary.result +
                               ", ошибок " + summary.totalErrors);
                return;
            }

            string archive = Path.Combine(BuildRoot, ExecutableName + ".zip");
            Zip(folder, archive);

            double megabytes = summary.totalSize / 1024.0 / 1024.0;

            Debug.Log("[IsoRPG] Готово. Папка: " + folder + nl +
                      "Архив: " + archive + nl +
                      "Размер сборки: " + megabytes.ToString("0.0") + " МБ, " +
                      "время " + summary.totalTime.TotalSeconds.ToString("0") + " с.");

            EditorUtility.RevealInFinder(archive);
        }

        private static string nl => Environment.NewLine;

        private static string[] CollectScenes()
        {
            var list = new System.Collections.Generic.List<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                if (!File.Exists(scene.path)) continue;

                list.Add(scene.path);
            }

            return list.ToArray();
        }

        /// <summary>
        /// Архив с прямыми слешами в путях. Разворачивается одинаково и на
        /// Windows, и на macOS.
        /// </summary>
        private static void Zip(string sourceFolder, string archivePath)
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);

            using (var stream = new FileStream(archivePath, FileMode.Create))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                string root = Path.GetFullPath(sourceFolder);
                char separator = Path.DirectorySeparatorChar;

                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetFullPath(file).Substring(root.Length + 1);
                    string entry = ExecutableName + "/" + relative.Replace(separator, '/');

                    // Имя пишем полностью: у Unity есть свой CompressionLevel,
                    // и короткая форма становится неоднозначной.
                    zip.CreateEntryFromFile(file, entry,
                                            System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }
    }
}
