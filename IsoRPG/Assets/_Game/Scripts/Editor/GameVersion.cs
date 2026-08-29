using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Номер версии игры. Читается из CHANGELOG.md в корне репозитория.
    ///
    /// Источник правды один намеренно. Версия нужна в четырёх местах — в самой
    /// игре, в имени архива, в лаунчере и на странице истории, — и если хранить
    /// её в каждом отдельно, они разойдутся. Разойдутся тихо: игра покажет одно,
    /// лаунчер другое, и понять, какая сборка у игрока на руках, станет нельзя.
    ///
    /// Выбран именно CHANGELOG, а не отдельный файл с числом, потому что версия
    /// без описания бесполезна. Раз описание всё равно пишется, пусть номер
    /// живёт рядом с ним: тогда невозможно поднять версию и забыть сказать, что
    /// в ней изменилось.
    /// </summary>
    public static class GameVersion
    {
        /// <summary>
        /// Что ставим, если файл не нашёлся или разобрать не удалось.
        /// Не «0.0.0»: ноль выглядит как настоящая версия и уедет в сборку
        /// незамеченным. Слово в номере сразу видно и в игре, и в имени файла.
        /// </summary>
        public const string Unknown = "0.0.0-unknown";

        /// <summary>Корень репозитория — на уровень выше папки проекта Unity.</summary>
        public static string RepositoryRoot =>
            Directory.GetParent(Application.dataPath).Parent.FullName;

        public static string ChangelogPath => Path.Combine(RepositoryRoot, "CHANGELOG.md");

        /// <summary>Текущий номер, например «0.2.0».</summary>
        public static string Current => Read().version;

        /// <summary>Дата текущей версии как записана в файле, например «24 августа 2026».</summary>
        public static string CurrentDate => Read().date;

        /// <summary>
        /// Разбирает верхний заголовок вида «## 0.2.0 — 24 августа 2026».
        ///
        /// Берётся именно первый: записи идут от новых к старым, и новая
        /// версия — это всегда та, что дописана сверху.
        /// </summary>
        public static (string version, string date) Read()
        {
            string path = ChangelogPath;

            if (!File.Exists(path))
            {
                Debug.LogWarning("[IsoRPG] Не найден CHANGELOG.md по пути " + path +
                                 ". Версия будет " + Unknown + ".");
                return (Unknown, string.Empty);
            }

            string text = File.ReadAllText(path);

            // Тире в заголовке — длинное, но короткое тоже принимаем: рука сама
            // ставит дефис, и ронять из-за этого сборку глупо.
            var match = Regex.Match(text, @"^##\s+(\d+\.\d+\.\d+)\s*[—–-]?\s*(.*)$",
                                    RegexOptions.Multiline);

            if (!match.Success)
            {
                Debug.LogWarning("[IsoRPG] В CHANGELOG.md нет заголовка вида " +
                                 "«## 0.2.0 — дата». Версия будет " + Unknown + ".");
                return (Unknown, string.Empty);
            }

            return (match.Groups[1].Value, match.Groups[2].Value.Trim());
        }

        /// <summary>
        /// Переносит номер в настройки проекта.
        ///
        /// Зовётся из сборщика перед каждой сборкой, а не руками: настройка,
        /// которую надо не забыть поменять, рано или поздно забывается.
        /// </summary>
        public static string ApplyToPlayerSettings()
        {
            var (version, _) = Read();

            if (PlayerSettings.bundleVersion != version)
            {
                PlayerSettings.bundleVersion = version;
                Debug.Log("[IsoRPG] Версия проекта обновлена до " + version + ".");
            }

            return version;
        }

        [MenuItem("Tools/IsoRPG/Показать версию", priority = 1)]
        private static void ShowVersion()
        {
            var (version, date) = Read();

            EditorUtility.DisplayDialog(
                "Версия игры",
                "Сейчас: " + version + Environment.NewLine +
                (string.IsNullOrEmpty(date) ? string.Empty : "Дата: " + date + Environment.NewLine) +
                Environment.NewLine +
                "Меняется правкой CHANGELOG.md в корне репозитория.",
                "Понятно");
        }
    }
}
