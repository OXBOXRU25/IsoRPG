using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HighFlyingBird.Launcher
{
    /// <summary>Одна запись истории версий.</summary>
    internal sealed class Release
    {
        public string Version = string.Empty;
        public string Date = string.Empty;

        /// <summary>Первый абзац после заголовка — короткая суть версии.</summary>
        public string Summary = string.Empty;

        public readonly List<Section> Sections = new List<Section>();

        /// <summary>Все пункты всех разделов подряд — для короткой карточки.</summary>
        public IEnumerable<string> AllItems
        {
            get
            {
                foreach (var section in Sections)
                    foreach (var item in section.Items)
                        yield return item;
            }
        }

        public int ItemCount
        {
            get
            {
                int total = 0;
                foreach (var section in Sections) total += section.Items.Count;
                return total;
            }
        }

        internal sealed class Section
        {
            public string Title = string.Empty;
            public readonly List<string> Items = new List<string>();
        }
    }

    /// <summary>
    /// Разбор CHANGELOG.md.
    ///
    /// Лаунчер читает тот же файл, что и сборщик игры, и тот же, из которого
    /// делается страница истории в вебе. Формат выбран не ради красоты: пока
    /// источник один, версия в игре, в лаунчере и на сайте не может разойтись.
    /// Стоит завести второй список — и они разойдутся в первый же занятый день.
    ///
    /// Парсер намеренно грубый. Markdown разбирать целиком незачем: нам нужны
    /// заголовки версий, подзаголовки разделов и пункты списка.
    /// </summary>
    internal static class Changelog
    {
        public static List<Release> Parse(string markdown)
        {
            var releases = new List<Release>();
            if (string.IsNullOrEmpty(markdown)) return releases;

            Release current = null;
            Release.Section section = null;

            foreach (string raw in markdown.Split('\n'))
            {
                string line = raw.TrimEnd('\r').Trim();

                // «## 0.2.0 — 24 августа 2026»
                var head = Regex.Match(line, @"^##\s+(\d+\.\d+\.\d+)\s*[—–-]?\s*(.*)$");
                if (head.Success)
                {
                    current = new Release
                    {
                        Version = head.Groups[1].Value,
                        Date = head.Groups[2].Value.Trim(),
                    };

                    releases.Add(current);
                    section = null;
                    continue;
                }

                // Любой другой заголовок второго уровня закрывает запись:
                // так хвост файла вроде «## До 0.1.0» не приписывается
                // к последней настоящей версии.
                if (line.StartsWith("## ")) { current = null; section = null; continue; }

                if (current == null) continue;

                // «### Добавлено»
                if (line.StartsWith("### "))
                {
                    section = new Release.Section { Title = line.Substring(4).Trim() };
                    current.Sections.Add(section);
                    continue;
                }

                // «* пункт» или «- пункт»
                if (line.StartsWith("* ") || line.StartsWith("- "))
                {
                    if (section == null)
                    {
                        section = new Release.Section { Title = string.Empty };
                        current.Sections.Add(section);
                    }

                    section.Items.Add(Clean(line.Substring(2)));
                    continue;
                }

                // Продолжение пункта с переносом строки: в файле длинные пункты
                // разбиты по ширине, а в окне они должны склеиться обратно.
                if (line.Length > 0 && section != null && section.Items.Count > 0
                    && !line.StartsWith("---"))
                {
                    int last = section.Items.Count - 1;
                    section.Items[last] = section.Items[last] + " " + Clean(line);
                    continue;
                }

                // Абзац сразу под заголовком версии — её краткая суть.
                if (line.Length > 0 && section == null && !line.StartsWith("---")
                    && string.IsNullOrEmpty(current.Summary))
                {
                    current.Summary = Clean(line);
                }
            }

            return releases;
        }

        public static List<Release> Load(string path)
        {
            try
            {
                return File.Exists(path)
                    ? Parse(File.ReadAllText(path))
                    : new List<Release>();
            }
            catch (Exception error)
            {
                Log.Write("Не прочитался CHANGELOG: " + error.Message);
                return new List<Release>();
            }
        }

        /// <summary>Снимает разметку выделения — в окне её показывать нечем.</summary>
        private static string Clean(string text)
        {
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
            text = Regex.Replace(text, @"`(.+?)`", "$1");
            return text.Trim();
        }

        /// <summary>
        /// Сравнение номеров версий по числам, а не по алфавиту.
        ///
        /// Строковое сравнение здесь врёт ровно один раз и очень некстати:
        /// «0.10.0» меньше «0.9.0», если сравнивать посимвольно. Игрок с
        /// десятой версией получил бы предложение обновиться до девятой.
        /// </summary>
        public static int Compare(string left, string right)
        {
            var a = Numbers(left);
            var b = Numbers(right);

            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;

                if (x != y) return x.CompareTo(y);
            }

            return 0;
        }

        private static int[] Numbers(string version)
        {
            if (string.IsNullOrEmpty(version)) return new int[0];

            var match = Regex.Match(version, @"(\d+)\.(\d+)\.(\d+)");
            if (!match.Success) return new int[0];

            return new[]
            {
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
            };
        }
    }
}
