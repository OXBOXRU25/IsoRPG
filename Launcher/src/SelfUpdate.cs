using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HighFlyingBird.Launcher
{
    /// <summary>Что сервер знает про свежую версию самого лаунчера.</summary>
    internal sealed class SelfUpdateInfo
    {
        public string Version = string.Empty;
        public string Url = string.Empty;
        public string Sha256 = string.Empty;

        public bool IsValid
        {
            get { return Version.Length > 0 && Url.Length > 0; }
        }
    }

    /// <summary>
    /// Лаунчер обновляет сам себя.
    ///
    /// Обойти это нельзя: игру он обновляет по сети, а сам доезжал до игрока
    /// только копированием файлов вручную. То есть любая правка в самом
    /// лаунчере — включая исправление того, как он обновляет игру, — не
    /// доходила ни до кого, кроме того, кто сидит за этой машиной.
    ///
    /// Работающая программа не может заменить собственный файл: Windows
    /// держит его открытым, пока процесс жив. Поэтому замену делает НОВАЯ
    /// версия, а не старая: старая скачивает её во временную папку и
    /// запускает с ключом «поставь себя вон туда». Новая ждёт, пока старая
    /// закроется, переносит файлы на место и запускает уже установленную
    /// копию. Отдельный помощник для этого не нужен — новая версия и есть
    /// помощник.
    /// </summary>
    internal static class SelfUpdate
    {
        /// <summary>Ключ командной строки, включающий режим установки.</summary>
        public const string ApplyFlag = "--apply-update";

        /// <summary>Читает описание свежей версии лаунчера.</summary>
        public static async Task<SelfUpdateInfo> Check(string url)
        {
            var info = new SelfUpdateInfo();

            try
            {
                if (string.IsNullOrEmpty(url)) return info;

                string json = await Net.DownloadString(url);
                if (string.IsNullOrEmpty(json)) return info;

                info.Version = Field(json, "version");
                info.Url = Field(json, "url");
                info.Sha256 = Field(json, "sha256");
            }
            catch (Exception error)
            {
                Log.Write("Не прочиталось описание обновления лаунчера: " + error.Message);
            }

            return info;
        }

        /// <summary>
        /// Скачивает новую версию и передаёт ей установку.
        ///
        /// Возвращает true, только если запустила новую версию — тогда
        /// вызывающему остаётся закрыть окно: дальше работает уже она.
        /// </summary>
        public static async Task<bool> Launch(SelfUpdateInfo info,
                                              Action<string, double> progress)
        {
            try
            {
                string home = AppDomain.CurrentDomain.BaseDirectory;

                string staging = Path.Combine(Path.GetTempPath(),
                                              "HighFlyingBird-launcher-" + info.Version);

                // Чистим прошлую попытку: недокачанный остаток от прерванного
                // обновления хуже, чем его отсутствие.
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);

                string archive = Path.Combine(staging, "launcher.zip");

                progress("Скачиваю лаунчер " + info.Version, 0.05);

                await Net.DownloadFile(info.Url, archive, (received, total) =>
                {
                    double share = total > 0 ? (double)received / total : 0;
                    progress("Скачиваю лаунчер " + info.Version, 0.05 + share * 0.7);
                });

                if (info.Sha256.Length > 0 && !Matches(archive, info.Sha256))
                {
                    Log.Write("Сумма архива лаунчера не сошлась");
                    progress("Лаунчер скачался повреждённым", 0);
                    return false;
                }

                progress("Ставлю новую версию", 0.8);

                string unpacked = Path.Combine(staging, "new");
                Directory.CreateDirectory(unpacked);
                ZipFile.ExtractToDirectory(archive, unpacked);

                // В архиве может быть как сам лаунчер, так и папка с ним.
                string exe = FindExecutable(unpacked);

                if (exe == null)
                {
                    Log.Write("В архиве лаунчера нет исполняемого файла");
                    progress("В обновлении нет программы — пропускаю", 0);
                    return false;
                }

                var start = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = ApplyFlag + " " + Quote(home) + " " +
                                Process.GetCurrentProcess().Id,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                };

                Process.Start(start);

                Log.Write("Передал установку версии " + info.Version + " файлу " + exe);
                return true;
            }
            catch (Exception error)
            {
                Log.Write("Самообновление не удалось: " + error);
                progress("Не удалось обновить лаунчер: " + error.Message, 0);
                return false;
            }
        }

        /// <summary>
        /// Второй режим работы программы: поставить себя в указанную папку.
        ///
        /// Сюда попадает уже НОВАЯ версия, запущенная старой из временной
        /// папки. Окна не показываем вовсе: всё, что нужно человеку, он
        /// увидит через секунду в перезапущенном лаунчере.
        /// </summary>
        public static void Apply(string target, int waitFor)
        {
            try
            {
                Log.Write("Ставлю себя в " + target + ", жду процесс " + waitFor);

                WaitForExit(waitFor);

                string source = AppDomain.CurrentDomain.BaseDirectory;

                CopyTree(source, target);

                RemoveFormerNames(target);

                string exe = Path.Combine(target,
                    Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));

                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = false,
                        WorkingDirectory = target,
                    });
                }
                else
                {
                    Log.Write("После установки не нашёлся " + exe);
                }
            }
            catch (Exception error)
            {
                Log.Write("Установка новой версии не удалась: " + error);
            }
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// Убирает лаунчер, оставшийся от прежнего имени файла.
        ///
        /// Программа переименовывалась, а обновление кладёт файлы рядом, а не
        /// вместо: в папке оказались бы две программы, обе рабочие и с разными
        /// версиями. Человек запускает ту, на которую у него ярлык, и получает
        /// старую — при том, что обновление честно установилось.
        ///
        /// Ярлык всё равно придётся обновить установщиком, но хотя бы не будет
        /// двух программ в одной папке.
        /// </summary>
        private static void RemoveFormerNames(string target)
        {
            string[] former = { "Приключения разбойника Жени.exe" };

            foreach (string name in former)
            {
                try
                {
                    string path = Path.Combine(target, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception error)
                {
                    Log.Write("Не удалось убрать прежний файл " + name + ": " + error.Message);
                }
            }
        }

        /// <summary>
        /// Ждёт, пока старая версия закроется.
        ///
        /// Пока её процесс жив, её файл занят и заменить его нельзя. Ждём с
        /// потолком: если старая по какой-то причине не закрылась, лучше
        /// попробовать и записать неудачу в журнал, чем висеть вечно.
        /// </summary>
        private static void WaitForExit(int processId)
        {
            if (processId <= 0) return;

            try
            {
                var old = Process.GetProcessById(processId);
                if (!old.WaitForExit(15000)) Log.Write("Старая версия не закрылась за 15 с");
            }
            catch (ArgumentException)
            {
                // Процесса уже нет — именно этого мы и ждали.
            }
        }

        /// <summary>
        /// Копирует папку целиком, повторяя попытки по занятым файлам.
        ///
        /// Антивирус или сама система могут держать только что закрытый файл
        /// ещё долю секунды. Одна неудачная попытка тут означала бы битую
        /// установку, поэтому пробуем несколько раз с паузой.
        /// </summary>
        private static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(source, target));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = file.Replace(source, target);

                for (int attempt = 0; attempt < 12; attempt++)
                {
                    try
                    {
                        File.Copy(file, destination, true);
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(400);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Thread.Sleep(400);
                    }
                }
            }
        }

        private static string FindExecutable(string folder)
        {
            var files = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);

            // Служебные программы установщика в архив попадать не должны, но
            // если попадут — берём не их.
            foreach (string file in files)
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                if (name.StartsWith("unins")) continue;

                return file;
            }

            return null;
        }

        private static string Quote(string value)
        {
            string q = ((char)34).ToString();
            return q + value.TrimEnd(Path.DirectorySeparatorChar) + q;
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

        private static string Field(string json, string name)
        {
            string q = ((char)34).ToString();

            var match = Regex.Match(json,
                q + name + q + @"\s*:\s*" + q + "([^" + q + "]*)" + q);

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    /// <summary>
    /// Скачивание, общее для всех обновлений.
    ///
    /// Отдельно, потому что тем же занимаются обновление игры и чтение
    /// списков: три копии одной настройки TLS — это три места, где можно
    /// забыть её поправить.
    /// </summary>
    internal static class Net
    {
        public static async Task<string> DownloadString(string url)
        {
            Secure();

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("User-Agent", "HighFlyingBird-Launcher/" + BuildInfo.Version);

                return await client.DownloadStringTaskAsync(new Uri(url));
            }
        }

        public static async Task DownloadFile(string url, string path,
                                              Action<long, long> onProgress)
        {
            Secure();

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "HighFlyingBird-Launcher/" + BuildInfo.Version);

                client.DownloadProgressChanged += (sender, args) =>
                    onProgress(args.BytesReceived, args.TotalBytesToReceive);

                await client.DownloadFileTaskAsync(new Uri(url), path);
            }
        }

        public static void Secure()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
        }
    }
}
