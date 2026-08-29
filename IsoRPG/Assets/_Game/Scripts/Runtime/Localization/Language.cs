using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Localization
{
    /// <summary>Языки, которые игра знает.</summary>
    public enum Language
    {
        Russian,
        English,
        Ukrainian,
    }

    /// <summary>
    /// Перевод текстов интерфейса.
    ///
    /// Ключом служит сама русская строка, а не выдуманный код вроде
    /// «menu.start.button». Причина практическая: в игре уже полтысячи строк,
    /// и переход на коды означал бы переписать каждую, а любая пропущенная
    /// превратилась бы в «menu.start.button» прямо на экране. С русским
    /// ключом непереведённое место показывает осмысленный русский текст —
    /// то есть худший случай выглядит как «этот кусок ещё не перевели»,
    /// а не как поломка.
    ///
    /// Обратная сторона: правка русского текста рвёт связь с переводом.
    /// Поэтому строки, уже попавшие в словарь, меняем вместе со словарём.
    /// </summary>
    public static class Loc
    {
        /// <summary>Язык по умолчанию. Игра сделана по-русски, с него и начинаем.</summary>
        public const Language Default = Language.Russian;

        private const string SettingKey = "IsoRPG.Language";

        public static event Action Changed;

        private static Language current = Default;
        private static bool loaded;

        /// <summary>Словари для языков, кроме русского: он и есть исходник.</summary>
        private static readonly Dictionary<Language, Dictionary<string, string>> tables =
            new Dictionary<Language, Dictionary<string, string>>();

        public static Language Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        /// <summary>Как язык называется на самом себе — так его и показываем.</summary>
        public static string NameOf(Language language)
        {
            switch (language)
            {
                case Language.English: return "English";
                case Language.Ukrainian: return "Українська";
                default: return "Русский";
            }
        }

        public static void Set(Language language)
        {
            EnsureLoaded();

            if (current == language) return;

            current = language;

            PlayerPrefs.SetInt(SettingKey, (int)language);
            PlayerPrefs.Save();

            var handler = Changed;
            if (handler != null) handler();
        }

        /// <summary>
        /// Переводит строку. Нет перевода — возвращает исходную.
        ///
        /// Молча и без предупреждений: непереведённая строка не ошибка, а
        /// нормальное состояние работы, которая идёт постепенно.
        /// </summary>
        public static string T(string russian)
        {
            EnsureLoaded();

            if (current == Language.Russian || string.IsNullOrEmpty(russian)) return russian;

            Dictionary<string, string> table;
            if (!tables.TryGetValue(current, out table)) return russian;

            string translated;
            return table.TryGetValue(russian, out translated) && translated.Length > 0
                ? translated
                : russian;
        }

        /// <summary>
        /// Переводит строку с подстановками: Loc.F("{0} энергии", 5).
        ///
        /// Нужна там, где в текст входит число. Без неё ключом становилась бы
        /// уже собранная строка — «5 энергии», «12 энергии», — и словарь
        /// пришлось бы заполнять на каждое возможное значение. С шаблоном
        /// ключ один, а число подставляется после перевода: в английском оно
        /// может стоять и в другом месте фразы.
        /// </summary>
        public static string F(string russian, params object[] args)
        {
            string pattern = T(russian);

            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                // Кривой перевод не должен ронять игру: в словарь пишет
                // человек, и лишняя фигурная скобка там — вопрос времени.
                return pattern;
            }
        }

        /// <summary>
        /// Возвращает переносы строк, записанные двумя знаками.
        ///
        /// Словарь читается построчно, поэтому настоящий перенос внутри
        /// записи разорвал бы её пополам. А переносы нужны: реплики торговцев
        /// и тексты квестов написаны абзацами, и без них диалог слипается в
        /// одну простыню.
        ///
        /// Знак записан кодом, а не сам собой: обратный слеш в исходнике
        /// имеет привычку исчезать при любой правке файла скриптом.
        /// </summary>
        private static string Unescape(string text)
        {
            string marker = ((char)92).ToString() + "n";

            return text.IndexOf(marker, StringComparison.Ordinal) < 0
                ? text
                : text.Replace(marker, ((char)10).ToString());
        }

        // ------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            current = (Language)PlayerPrefs.GetInt(SettingKey, (int)Default);

            Load(Language.English, "Localization/en");
            Load(Language.Ukrainian, "Localization/uk");
        }

        /// <summary>
        /// Читает словарь из текстового файла в Resources.
        ///
        /// Формат — по строке на запись, русский и перевод через знак равенства.
        /// Не JSON намеренно: переводы правит человек, а не программа, и в
        /// таком виде их можно открыть в блокноте и дописать строку, не рискуя
        /// сломать разметку лишней запятой.
        /// </summary>
        private static void Load(Language language, string path)
        {
            var table = new Dictionary<string, string>();
            tables[language] = table;

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return;

            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] == '#') continue;

                int split = line.IndexOf('=');
                if (split <= 0) continue;

                string key = Unescape(line.Substring(0, split).Trim());
                string value = Unescape(line.Substring(split + 1).Trim());

                if (key.Length == 0 || value.Length == 0) continue;

                // Перевод может содержать знак равенства — берём только первый
                // как разделитель, остальное часть текста.
                table[key] = value;
            }
        }
    }
}
