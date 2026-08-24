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
        /// <summary>Адрес описания обновления (update.json). Пусто — не проверяем.</summary>
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

}
