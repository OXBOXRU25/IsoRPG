using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HighFlyingBird.Launcher
{
    /// <summary>Один файл игры в списке на сервере.</summary>
    internal sealed class RemoteFile
    {
        public string Path = string.Empty;
        public string Sha256 = string.Empty;
        public long Size;
    }

    /// <summary>Что сервер знает про состав текущей версии.</summary>
    internal sealed class FileList
    {
        public string Version = string.Empty;
        public string Base = string.Empty;
        public readonly List<RemoteFile> Files = new List<RemoteFile>();

        public bool IsValid
        {
            get { return Version.Length > 0 && Base.Length > 0 && Files.Count > 0; }
        }
    }

    /// <summary>
    /// Обновление по одному файлу вместо целого архива.
    ///
    /// Между соседними версиями меняется около десятка файлов из двух сотен:
    /// движок весом в тридцать семь мегабайт не трогается месяцами, а игрок
    /// скачивал его при каждом обновлении. Сверив суммы, берём только то, что
    /// действительно отличается, — обычно это меньше десятой части.
    ///
    /// Считать суммы всех файлов на диске небесплатно, но это чтение с диска
    /// против скачивания по сети: двести мегабайт с быстрого диска читаются
    /// за пару секунд, а качаются минуту.
    /// </summary>
    internal static class FileUpdater
    {
        /// <summary>Читает список файлов текущей версии.</summary>
        public static async Task<FileList> Fetch(string url)
        {
            var list = new FileList();

            try
            {
                string json = await Download(url);
                if (string.IsNullOrEmpty(json)) return list;

                list.Version = Field(json, "version");
                list.Base = Field(json, "base");

                // Разбираем записи одним выражением: в каждой ровно три поля,
                // и порядок их задаём мы сами при выкладке.
                string q = ((char)34).ToString();

                var matches = Regex.Matches(json,
                    q + "path" + q + @"\s*:\s*" + q + "([^" + q + "]+)" + q +
                    @"[^}]*?" +
                    q + "size" + q + @"\s*:\s*(\d+)" +
                    @"[^}]*?" +
                    q + "sha256" + q + @"\s*:\s*" + q + "([^" + q + "]+)" + q,
                    RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    long size;
                    long.TryParse(match.Groups[2].Value, out size);

                    list.Files.Add(new RemoteFile
                    {
                        Path = match.Groups[1].Value,
                        Size = size,
                        Sha256 = match.Groups[3].Value,
                    });
                }
            }
            catch (Exception error)
            {
                Log.Write("Не прочитался список файлов: " + error.Message);
            }

            return list;
        }

        /// <summary>
        /// Обновляет игру, скачивая только изменившееся.
        ///
        /// Возвращает false, если что-то не вышло, — тогда вызывающий может
        /// откатиться на скачивание целого архива.
        /// </summary>
        public static async Task<bool> Install(FileList list, string gameFolder,
                                               Action<string, double> progress)
        {
            try
            {
                progress("Сверяю файлы", 0);

                var needed = new List<RemoteFile>();
                long neededBytes = 0;

                for (int i = 0; i < list.Files.Count; i++)
                {
                    var file = list.Files[i];
                    string local = Path.Combine(gameFolder, file.Path.Replace('/', Path.DirectorySeparatorChar));

                    if (!Same(local, file))
                    {
                        needed.Add(file);
                        neededBytes += file.Size;
                    }

                    // Сверка идёт заметное время, и молчащая полоса читается
                    // как зависшая программа.
                    if (i % 16 == 0)
                    {
                        progress("Сверяю файлы  " + (i + 1) + " из " + list.Files.Count,
                                 (double)i / list.Files.Count * 0.15);
                    }
                }

                if (needed.Count == 0)
                {
                    progress("Всё уже на месте", 1);
                    return true;
                }

                Log.Write("К обновлению " + needed.Count + " файлов, " +
                          (neededBytes / 1024 / 1024) + " МБ");

                long done = 0;

                foreach (var file in needed)
                {
                    string target = Path.Combine(gameFolder,
                        file.Path.Replace('/', Path.DirectorySeparatorChar));

                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    // Качаем рядом и переставляем на место готовым: если связь
                    // оборвётся, у игрока останется прежний рабочий файл, а не
                    // половина нового.
                    string temp = target + ".part";

                    await DownloadFile(list.Base + Uri.EscapeUriString(file.Path), temp,
                        (received, total) =>
                        {
                            double share = neededBytes > 0
                                ? (double)(done + received) / neededBytes : 0;

                            progress("Скачиваю  " + ((done + received) / 1024 / 1024) +
                                     " из " + (neededBytes / 1024 / 1024) + " МБ",
                                     0.15 + share * 0.8);
                        });

                    if (!Matches(temp, file.Sha256))
                    {
                        File.Delete(temp);
                        Log.Write("Сумма не сошлась у " + file.Path);

                        progress("Файл повреждён при скачивании: " + file.Path, 0);
                        return false;
                    }

                    // Замена готовым файлом. Delete перед Move обязателен:
                    // Move не перезаписывает существующий файл.
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(temp, target);

                    done += file.Size;
                }

                progress("Готово", 1);
                return true;
            }
            catch (Exception error)
            {
                Log.Write("Пофайловое обновление не удалось: " + error);
                progress("Не удалось обновить: " + error.Message, 0);
                return false;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Совпадает ли файл на диске с описанным.
        ///
        /// Сначала сверяем размер: он берётся из записи каталога мгновенно,
        /// и у изменившегося файла почти всегда отличается. Считать сумму
        /// двухсот файлов, когда девять десятых отсеиваются размером, — пустая
        /// работа.
        /// </summary>
        private static bool Same(string path, RemoteFile file)
        {
            try
            {
                if (!File.Exists(path)) return false;

                var info = new FileInfo(path);
                if (info.Length != file.Size) return false;

                return Matches(path, file.Sha256);
            }
            catch
            {
                return false;
            }
        }

        private static bool Matches(string path, string expected)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    byte[] bytes = sha.ComputeHash(stream);

                    var text = new StringBuilder(bytes.Length * 2);
                    foreach (byte b in bytes) text.Append(b.ToString("x2"));

                    return string.Equals(text.ToString(), expected,
                                         StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
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

        private static void Secure()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
        }

        private static string Field(string json, string name)
        {
            string q = ((char)34).ToString();

            var match = Regex.Match(json,
                q + name + q + @"\s*:\s*" + q + "([^" + q + "]*)" + q);

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
