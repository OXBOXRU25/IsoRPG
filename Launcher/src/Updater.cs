using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HighFlyingBird.Launcher
{
    /// <summary>Что сервер сообщает о доступной версии.</summary>
    internal sealed class UpdateInfo
    {
        public string Version = string.Empty;
        public string Url = string.Empty;
        public string Sha256 = string.Empty;
        public long Size;

        public bool IsValid
        {
            get { return Version.Length > 0 && Url.Length > 0; }
        }
    }

    /// <summary>
    /// Скачивает и ставит обновление игры.
    ///
    /// Обновляется только папка игры. Себя лаунчер обновляет отдельным путём —
    /// см. SelfUpdate: работающая программа не может переписать собственный
    /// файл, поэтому замену там делает уже скачанная новая версия.
    ///
    /// Скачанный архив проверяется по контрольной сумме до распаковки. Это не
    /// формальность: лаунчер кладёт в папку игры исполняемые файлы, и без
    /// проверки любой сбой при передаче — или подмена по дороге — превращается
    /// в запуск неизвестно чего. Полностью риск закрывает только https, но
    /// сумма ловит и повреждение, и грубую подмену.
    /// </summary>
    internal static class Updater
    {
        /// <summary>Читает описание обновления с сервера.</summary>
        public static async Task<UpdateInfo> Check(string url)
        {
            var info = new UpdateInfo();

            try
            {
                string json = await Download(url);
                if (string.IsNullOrEmpty(json)) return info;

                info.Version = Field(json, "version");
                info.Url = Field(json, "url");
                info.Sha256 = Field(json, "sha256");

                long size;
                if (long.TryParse(Field(json, "size"), out size)) info.Size = size;
            }
            catch (Exception error)
            {
                Log.Write("Не прочиталось описание обновления: " + error.Message);
            }

            return info;
        }

        /// <summary>
        /// Качает архив, проверяет сумму и распаковывает поверх игры.
        ///
        /// Сообщения о ходе идут через progress — окно показывает их человеку,
        /// потому что скачивание семидесяти мегабайт без единого признака
        /// жизни неотличимо от зависшей программы.
        /// </summary>
        public static async Task<bool> Install(UpdateInfo info, string gameFolder,
                                               Action<string, double> progress)
        {
            string temp = Path.Combine(Path.GetTempPath(),
                                       "HighFlyingBird-" + info.Version + ".zip");

            try
            {
                progress("Скачиваю обновление", 0);

                await DownloadFile(info.Url, temp, (received, total) =>
                {
                    double share = total > 0 ? (double)received / total : 0;

                    progress("Скачиваю обновление  " +
                             (received / 1024 / 1024) + " из " + (total / 1024 / 1024) + " МБ",
                             share * 0.85);
                });

                progress("Проверяю целостность", 0.88);

                if (!string.IsNullOrEmpty(info.Sha256))
                {
                    string actual = Hash(temp);

                    if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Write("Сумма не сошлась. Ожидалось " + info.Sha256 +
                                  ", получено " + actual);

                        progress("Файл повреждён при скачивании", 0);
                        return false;
                    }
                }

                progress("Устанавливаю", 0.92);

                Extract(temp, gameFolder);

                progress("Готово", 1);
                return true;
            }
            catch (Exception error)
            {
                Log.Write("Обновление не установилось: " + error);
                progress("Не удалось обновить: " + error.Message, 0);
                return false;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { /* не смогли убрать временный файл — не беда */ }
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Распаковка поверх установленной игры.
        ///
        /// Именно поверх, а не в пустую папку: обновление содержит только
        /// изменившиеся файлы движка и данных, а сохранения и настройки лежат
        /// вне папки игры и не трогаются вовсе.
        /// </summary>
        private static void Extract(string archive, string folder)
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var entry in zip.Entries)
                {
                    // Пустое имя — это папка, её создаст сам путь файла.
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string target = Path.Combine(folder, entry.FullName);

                    // Защита от архива, который пытается писать выше своей
                    // папки именами вида «..». Такой архив не бывает случайным.
                    string full = Path.GetFullPath(target);
                    if (!full.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Write("Пропущен путь за пределами папки: " + entry.FullName);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    entry.ExtractToFile(full, true);
                }
            }
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] bytes = sha.ComputeHash(stream);

                var text = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) text.Append(b.ToString("x2"));

                return text.ToString();
            }
        }

        private static async Task DownloadFile(string url, string path,
                                               Action<long, long> onProgress)
        {
            Secure();

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "HighFlyingBird-Launcher/" +
                                                 LauncherWindow.LauncherVersion);

                client.DownloadProgressChanged += (sender, args) =>
                    onProgress(args.BytesReceived, args.TotalBytesToReceive);

                await client.DownloadFileTaskAsync(new Uri(url), path);
            }
        }

        private static async Task<string> Download(string url)
        {
            Secure();

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("User-Agent", "HighFlyingBird-Launcher/" +
                                                 LauncherWindow.LauncherVersion);

                return await client.DownloadStringTaskAsync(new Uri(url));
            }
        }

        /// <summary>
        /// Разрешает современные протоколы шифрования.
        ///
        /// На Windows 10 значение по умолчанию может не включать TLS 1.2, и
        /// тогда любой https отвечает отказом соединения — со стороны это
        /// выглядит как недоступный сайт, а не как настройка.
        /// </summary>
        private static void Secure()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
        }

        private static string Field(string json, string name)
        {
            string quote = ((char)34).ToString();

            // Числа приходят без кавычек, строки — в кавычках. Одно выражение
            // на оба случая: иначе размер пришлось бы разбирать отдельно.
            var match = Regex.Match(json,
                quote + name + quote + @"\s*:\s*" +
                "(?:" + quote + "([^" + quote + "]*)" + quote + @"|(\d+))");

            if (!match.Success) return string.Empty;

            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }
    }
}
