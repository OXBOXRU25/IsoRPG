using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Настройки лаунчера из файла рядом с ним.
    ///
    /// Файл, а не константы в коде: адрес обновлений меняется при переезде
    /// хостинга, и ради этого не должно требоваться пересобирать программу.
    /// Файла может не быть — тогда лаунчер просто не проверяет обновления.
    /// </summary>
    internal sealed class LauncherConfig
    {
        /// <summary>Адрес, где лежит свежий CHANGELOG.md. Пусто — не проверяем.</summary>
        public string UpdateUrl = string.Empty;

        /// <summary>Адрес страницы истории версий — для кнопки «Подробнее».</summary>
        public string SiteUrl = string.Empty;

        public static LauncherConfig Load()
        {
            var config = new LauncherConfig();

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                           "launcher.json");

                if (!File.Exists(path)) return config;

                string text = File.ReadAllText(path);

                config.UpdateUrl = Field(text, "updateUrl");
                config.SiteUrl = Field(text, "siteUrl");
            }
            catch (Exception error)
            {
                Log.Write("Не прочитались настройки: " + error.Message);
            }

            return config;
        }

        private static string Field(string json, string name)
        {
            string quote = ((char)34).ToString();

            var match = Regex.Match(json,
                quote + name + quote + @"\s*:\s*" +
                quote + "([^" + quote + "]*)" + quote);

            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    /// <summary>
    /// Проверка обновлений по сети.
    ///
    /// Проверяем не «есть ли новый файл», а какой номер версии стоит первым
    /// в свежем CHANGELOG.md. То есть источник тот же самый, что показывает
    /// историю в окне: отдельного файла с номером нет, и рассинхронизироваться
    /// нечему.
    ///
    /// Скачивание патчей сюда пока не входит намеренно. Оно требует места, где
    /// лежат сборки, и подписи файлов — без второго лаунчер, качающий exe с
    /// произвольного адреса, становится дырой в безопасности игрока.
    /// </summary>
    internal static class Updates
    {
        public static async Task<string> LatestVersion(string url)
        {
            try
            {
                string markdown = await Download(url);
                if (string.IsNullOrEmpty(markdown)) return string.Empty;

                var releases = Changelog.Parse(markdown);

                return releases.Count > 0 ? releases[0].Version : string.Empty;
            }
            catch (Exception error)
            {
                // Нет сети — это не ошибка, а обычное состояние. Пишем в файл
                // и молчим: окно с сообщением «нет интернета» при каждом
                // запуске раздражает сильнее, чем польза от него.
                Log.Write("Проверка обновлений не удалась: " + error.Message);
                return string.Empty;
            }
        }

        private static async Task<string> Download(string url)
        {
            // Протокол задаём явно: на Windows 10 по умолчанию может быть
            // выключен TLS 1.2, и тогда любой https отвечает отказом
            // соединения — выглядит как «сайт недоступен».
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("User-Agent", "HighFlyingBird-Launcher/" +
                                                 LauncherWindow.LauncherVersion);

                return await client.DownloadStringTaskAsync(new Uri(url));
            }
        }
    }
}
