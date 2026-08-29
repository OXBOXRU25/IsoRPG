using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Разведка анимаций: что у нас есть и на кого это можно надеть.
    ///
    /// Задача «раздай нормальные анимации — монстрам монстровое, людям
    /// человеческое» упирается в один факт, который надо проверить, а не
    /// предположить: клипы переносятся с чужого скелета только через
    /// **Humanoid**. У Generic-рига клип намертво привязан к своим костям, и
    /// надеть его на другую модель нельзя никак.
    ///
    /// Что мы знаем до проверки:
    ///
    /// * Единственный набор передвижения Synty у нас — гоблинский. Он
    ///   приехал в двух видах (Polygon и Sidekick), но это один и тот же
    ///   сгорбленный шаг, разложенный на два скелета. Отсюда «все ходят как
    ///   гориллы»: людям достаётся гоблинская походка.
    /// * У KayKit походка человеческая — рыцарь, разбойник, маг ходят прямо.
    ///   Но их риг размечен как Generic, и клипы никуда не переносятся.
    ///
    /// Отсюда план: перевести риг KayKit в Humanoid. Тогда человеческие
    /// клипы становятся переносимыми, и людям Synty достаётся человеческий
    /// шаг, а монстрам — гоблинский. Оба набора уже куплены, доплачивать не
    /// надо.
    ///
    /// Перевод не бесплатен: Unity сама сопоставляет кости со своей схемой, и
    /// сопоставление может не сойтись. Поэтому здесь не «сделали и пошли
    /// дальше», а «сделали и напечатали, у кого скелет собрался, а у кого
    /// нет».
    /// </summary>
    public static class AnimAudit
    {
        private const string Anim = "Assets/_Game/Art/KayKit/Animations";
        private const string Chars = "Assets/_Game/Art/KayKit/Characters";

        [MenuItem("Tools/IsoRPG/Анимации: разведка и перевод в Humanoid", priority = 70)]
        public static void Run()
        {
            var report = new StringBuilder();

            report.AppendLine("РАЗВЕДКА АНИМАЦИЙ");
            report.AppendLine();

            int converted = 0, failed = 0;

            report.AppendLine("--- KayKit: наборы клипов ---");

            foreach (var path in Files(Anim))
            {
                converted += Convert(path, report, ref failed) ? 1 : 0;
                Clips(path, report);
                report.AppendLine();
            }

            report.AppendLine("--- KayKit: персонажи ---");

            foreach (var path in Files(Chars))
                converted += Convert(path, report, ref failed) ? 1 : 0;

            report.AppendLine();
            report.AppendLine("Переведено в Humanoid: " + converted +
                              ", не собрался скелет: " + failed);

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG]\n" + report);
        }

        private static string[] Files(string folder)
        {
            if (!Directory.Exists(folder)) return new string[0];

            return Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly)
                            .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                            .OrderBy(p => p)
                            .ToArray();
        }

        /// <summary>
        /// Переводит модель в Humanoid и проверяет, собрался ли скелет.
        ///
        /// Проверка обязательна и именно здесь. Unity на неудачное
        /// сопоставление не ругается ошибкой — она просто отдаёт аватар,
        /// который «не человек», и все переносы с него молча дают позу
        /// буквой «Т». Это тот случай, когда без печати факта ошибку находят
        /// уже в игре.
        /// </summary>
        private static bool Convert(string path, StringBuilder report, ref int failed)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                report.AppendLine("    " + Path.GetFileName(path) + "   не модель");
                return false;
            }

            bool already = importer.animationType == ModelImporterAnimationType.Human;

            if (!already)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path)
                                      .OfType<Avatar>()
                                      .FirstOrDefault();

            bool good = avatar != null && avatar.isValid && avatar.isHuman;

            report.AppendLine("    " + Path.GetFileName(path).PadRight(36) +
                              (already ? "уже Humanoid   " : "переведён      ") +
                              (good ? "скелет собрался" : "СКЕЛЕТ НЕ СОБРАЛСЯ"));

            if (!good) failed++;

            return good;
        }

        private static void Clips(string path, StringBuilder report)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                                     .OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__preview"))
                                     .OrderBy(c => c.name)
                                     .ToArray();

            foreach (var clip in clips)
                report.AppendLine("        " + clip.name.PadRight(32) +
                                  clip.length.ToString("0.00") + " с" +
                                  (clip.isLooping ? "   зациклен" : ""));
        }
    }
}
