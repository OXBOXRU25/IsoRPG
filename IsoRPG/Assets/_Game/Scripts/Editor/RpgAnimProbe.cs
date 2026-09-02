using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп нового набора анимаций (RPG Animations Pack, папка `DoubleL`).
    ///
    /// Отвечает на три вопроса, от которых зависит, годится ли набор вообще,
    /// и все три надо знать ДО того, как строить на нём контроллер:
    ///
    ///   1. **тип рига.** Если клипы помечены Generic, на нашего героя они не
    ///      лягут никак: ретаргет умеет только гуманоид. Это первое, что надо
    ///      проверять у любого чужого набора анимаций;
    ///   2. **что там есть по одноручному** — кинжальной категории в наборе
    ///      нет, есть «оружие опущено» и «оружие поднято», и надо увидеть
    ///      имена, а не догадываться по названию папки;
    ///   3. **несёт ли клип корневое движение.** Наш герой ходит агентом и
    ///      капсулой; клип, который везёт сам, уедет от них — и это ровно то,
    ///      на чём мы уже стояли со зверями.
    ///
    /// Печатает выборку, а не всё: клипов там тысячи, и полный список утопит
    /// журнал ровно там, где его читают.
    /// </summary>
    public static class RpgAnimProbe
    {
        private const string Root = "Assets/DoubleL";

        [MenuItem("Tools/IsoRPG/Щуп: новый набор анимаций", priority = 48)]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogError("[IsoRPG] Набор не найден: " + Root);
                return;
            }

            var report = new StringBuilder();

            // --- разделы и объём ---------------------------------------------
            var byFolder = new Dictionary<string, int>();

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var parts = path.Split('/');

                string folder = parts.Length > 3 ? parts[3] : "(корень)";
                byFolder.TryGetValue(folder, out int n);
                byFolder[folder] = n + 1;
            }

            report.Append("\n  Моделей и клипов в наборе: ").Append(guids.Length);

            foreach (var pair in byFolder.OrderByDescending(p => p.Value))
                report.Append("\n    ").Append(pair.Key.PadRight(26)).Append(pair.Value);

            // --- тип рига ------------------------------------------------------
            var rigs = new Dictionary<ModelImporterAnimationType, int>();
            var noAvatar = new List<string>();

            foreach (var guid in guids.Take(400))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                rigs.TryGetValue(importer.animationType, out int n);
                rigs[importer.animationType] = n + 1;

                if (importer.animationType == ModelImporterAnimationType.Human &&
                    importer.sourceAvatar == null &&
                    importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
                {
                    noAvatar.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }

            report.Append("\n\n  Тип рига (по первым 400 файлам):");

            foreach (var pair in rigs)
                report.Append("\n    ").Append(pair.Key).Append(" — ").Append(pair.Value);

            if (rigs.ContainsKey(ModelImporterAnimationType.Human))
                report.Append("\n    → гуманоид есть, ретаргет на нашего героя возможен");
            else
                report.Append("\n    → ГУМАНОИДА НЕТ: на нашего героя набор не ляжет без переимпорта");

            if (noAvatar.Count > 0)
                report.Append("\n    без аватара: ").Append(noAvatar.Count)
                      .Append(" (у Generic-скелета без аватара анимации молчат)");

            // --- одноручное: что за клипы --------------------------------------
            foreach (string section in new[] { "One Hand Base", "One Hand Up", "Base Move" })
            {
                string folder = Root + "/FBX_Animations/" + section;

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    report.Append("\n\n  Раздела «").Append(section).Append("» нет.");
                    continue;
                }

                var names = AssetDatabase.FindAssets("t:Model", new[] { folder })
                    .Select(g => System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GUIDToAssetPath(g)))
                    .OrderBy(n => n)
                    .ToArray();

                report.Append("\n\n  «").Append(section).Append("» — ").Append(names.Length)
                      .Append(" клипов. Удары и выпады:");

                var hits = names.Where(n =>
                        n.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("stab", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("slash", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("combo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(30)
                    .ToArray();

                report.Append("\n    ").Append(hits.Length == 0
                    ? "по словам attack/stab/slash/combo не нашлось — вот первые двадцать имён: " +
                      string.Join(", ", names.Take(20))
                    : string.Join(", ", hits));
            }

            Debug.Log("[IsoRPG] Щуп нового набора анимаций:" + report);
        }
    }
}
