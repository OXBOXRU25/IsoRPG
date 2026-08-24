using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Где лежит игра, какой она версии и как её запустить.
    ///
    /// Путь не спрашивается у игрока и нигде не прописан жёстко: лаунчер ищет
    /// игру сам, начиная с папки, в которой лежит. Спрашивать путь — значит
    /// требовать от человека знать устройство собственной установки; жёсткий
    /// путь ломается от первого же переноса папки на другой диск.
    /// </summary>
    internal sealed class GameFinder
    {
        private const string ExecutableName = "HighFlyingBird.exe";

        /// <summary>Полный путь к exe игры или пусто, если не нашли.</summary>
        public string ExecutablePath { get; private set; }

        /// <summary>Версия установленной игры из version.json рядом с ней.</summary>
        public string InstalledVersion { get; private set; }

        public bool Found
        {
            get { return !string.IsNullOrEmpty(ExecutablePath); }
        }

        public GameFinder()
        {
            Refresh();
        }

        /// <summary>
        /// Перечитывает состояние с диска.
        ///
        /// Нужно после установки обновления: версия читается из файла рядом
        /// с игрой, и в памяти лаунчера она остаётся прежней, пока её не
        /// перечитали. Показывать обещанную сервером версию вместо реальной
        /// нельзя — обновление могло встать наполовину.
        /// </summary>
        public void Refresh()
        {
            ExecutablePath = Locate();
            InstalledVersion = ReadVersion();
        }

        /// <summary>
        /// Порядок поиска: рядом с лаунчером, потом в подпапке игры, потом
        /// на уровень выше. Этим тремя случаями закрываются все раскладки,
        /// которые получаются при обычной установке и при распаковке архива.
        /// </summary>
        private static string Locate()
        {
            string home = AppDomain.CurrentDomain.BaseDirectory;

            var candidates = new[]
            {
                Path.Combine(home, ExecutableName),
                Path.Combine(home, "Game", ExecutableName),
                Path.Combine(home, "HighFlyingBird", ExecutableName),
                Path.Combine(Path.GetFullPath(Path.Combine(home, "..")), ExecutableName),
                Path.Combine(Path.GetFullPath(Path.Combine(home, "..")),
                             "HighFlyingBird", ExecutableName),
            };

            foreach (string candidate in candidates)
            {
                try { if (File.Exists(candidate)) return candidate; }
                catch { /* недоступный путь — просто не наш случай */ }
            }

            return string.Empty;
        }

        /// <summary>
        /// Версия из version.json, который кладёт сборщик игры.
        ///
        /// Файл читается регулярным выражением, а не разборщиком JSON: полей
        /// три, а тянуть ради них зависимость — значит увеличить лаунчер в
        /// несколько раз.
        /// </summary>
        private string ReadVersion()
        {
            if (!Found) return string.Empty;

            try
            {
                string path = Path.Combine(Path.GetDirectoryName(ExecutablePath), "version.json");
                if (!File.Exists(path)) return string.Empty;

                // Кавычку собираем из кода символа, а не пишем в тексте
                // выражения. Экранированные кавычки внутри регулярки внутри
                // строки читаются отвратительно и ломаются при первой правке.
                string quote = ((char)34).ToString();

                var match = Regex.Match(
                    File.ReadAllText(path),
                    quote + "version" + quote + @"\s*:\s*" +
                    quote + "([^" + quote + "]+)" + quote);

                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch (Exception error)
            {
                Log.Write("Не прочиталась версия игры: " + error.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Запускает игру. Рабочей папкой ставится папка игры — Unity ищет
        /// рядом с собой папку с данными, и из чужой папки не найдёт.
        /// </summary>
        public bool Launch(out string error)
        {
            error = string.Empty;

            if (!Found)
            {
                error = "Не нашёл " + ExecutableName + " рядом с лаунчером.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                    UseShellExecute = true,
                });

                return true;
            }
            catch (Exception failure)
            {
                error = failure.Message;
                Log.Write("Игра не запустилась: " + failure);
                return false;
            }
        }

        /// <summary>Папка сохранений — та же, куда пишет сама игра.</summary>
        public static string SaveFolder
        {
            get
            {
                string appData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

                // Unity складывает сохранения в LocalLow, а не в Local.
                string low = Path.Combine(Path.GetDirectoryName(appData), "LocalLow");

                return Path.Combine(low, "OXBOX", "Птица высокого полёта");
            }
        }
    }

    /// <summary>
    /// Запись в файл рядом с лаунчером.
    ///
    /// Нужна ровно для одного случая: когда у игрока лаунчер не запускается
    /// или не находит игру, а показать ему окно с ошибкой уже нельзя. Тогда
    /// он присылает этот файл, и причина видна сразу.
    /// </summary>
    internal static class Log
    {
        private static readonly object Lock = new object();

        private static string Path_
        {
            get
            {
                return System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "launcher.log");
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(Path_,
                        DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "  " +
                        message + Environment.NewLine);
                }
            }
            catch { /* не смогли записать — не повод падать */ }
        }
    }
}
