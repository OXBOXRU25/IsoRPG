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

            // Номер берём из CHANGELOG.md, а не из настроек проекта: иначе
            // версия и описание изменений живут порознь и расходятся.
            string version = GameVersion.ApplyToPlayerSettings();

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

            CleanBuildFolder(folder);

            // Версия в имени файла обязательна: у игрока на диске лежат три
            // архива с одинаковым именем, и какой из них новее — не узнать.
            // Файл версии пишем ДО упаковки, иначе он остаётся только в
            // папке, а в архив не попадает — и лаунчер у того, кто получил
            // архив, показывает игру без версии.
            WriteVersionFile(folder, version);

            string archive = Path.Combine(BuildRoot, ExecutableName + "-" + version + ".zip");
            Zip(folder, archive);

            double megabytes = summary.totalSize / 1024.0 / 1024.0;

            Debug.Log("[IsoRPG] Готово, версия " + version + ". Папка: " + folder + nl +
                      "Архив: " + archive + nl +
                      "Размер сборки: " + megabytes.ToString("0.0") + " МБ, " +
                      "время " + summary.totalTime.TotalSeconds.ToString("0") + " с.");

            EditorUtility.RevealInFinder(archive);
        }

        /// <summary>
        /// Выкидывает из готовой сборки то, что игроку не нужно.
        ///
        /// Unity кладёт рядом с игрой отладочные данные компилятора Burst и
        /// прямо пишет в имени папки «DoNotShip». Работе они не мешают, но
        /// уезжать к игрокам им незачем.
        /// </summary>
        private static void CleanBuildFolder(string folder)
        {
            foreach (string path in Directory.GetDirectories(folder))
            {
                if (!path.EndsWith("_DoNotShip", StringComparison.OrdinalIgnoreCase)) continue;

                Directory.Delete(path, true);
                Debug.Log("[IsoRPG] Из сборки убрано: " + Path.GetFileName(path));
            }
        }

        /// <summary>
        /// Кладёт рядом с игрой файл с её версией.
        ///
        /// Нужен лаунчеру: чтобы понять, надо ли обновляться, он должен
        /// узнать версию установленной игры, не запуская её. Читать номер
        /// из свойств exe можно, но там он в формате Windows (четыре числа),
        /// и наша «0.2.0» туда не ложится без потерь.
        /// </summary>
        private static void WriteVersionFile(string folder, string version)
        {
            var (_, date) = GameVersion.Read();

            string json = "{" + nl +
                          "  \u0022version\u0022: \u0022" + version + "\u0022," + nl +
                          "  \u0022date\u0022: \u0022" + date + "\u0022," + nl +
                          "  \u0022executable\u0022: \u0022" + ExecutableName + ".exe\u0022" + nl +
                          "}" + nl;

            File.WriteAllText(Path.Combine(folder, "version.json"), json);
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
