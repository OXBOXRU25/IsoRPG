using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Печатает, что реально лежит в наборе KayKit: клипы анимаций, кости,
    /// тип рига.
    ///
    /// Нужен потому, что имена клипов угадывать нельзя — контроллер строится
    /// по ним, и опечатка проявится не ошибкой, а неподвижным персонажем.
    /// Дешевле один раз посмотреть, чем трижды пересобирать.
    /// </summary>
    public static class KayKitProbe
    {
        private const string AnimationsFolder = "Assets/_Game/Art/KayKit/Animations";
        private const string CharactersFolder = "Assets/_Game/Art/KayKit/Characters";

        /// <summary>
        /// Печатает скелет персонажа целиком.
        ///
        /// Нужно, чтобы вложить оружие в руку: у наборов вроде KayKit для
        /// этого обычно заведены отдельные кости-держатели, и угадывать их
        /// имена нельзя — промах даст меч, растущий из бедра.
        /// </summary>
        /// <summary>
        /// Что внутри модели с проёмом: отдельная ли створка.
        ///
        /// От этого зависит, можно ли двери открывать. Если створка — отдельный
        /// объект внутри модели, её достаточно повернуть. Если она вварена в
        /// общий меш, придётся искать другую модель или резать геометрию, а это
        /// уже работа в редакторе моделей.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Показать устройство двери", priority = 34)]
        public static void ProbeDoorway()
        {
            var lines = new List<string>();

            foreach (var name in new[] { "wall_doorway", "wall_gated", "wall_doorway_sides" })
            {
                string path = "Assets/_Game/Art/KayKit/Dungeon/" + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model == null)
                {
                    lines.Add(name + ": НЕ НАЙДЕНА");
                    continue;
                }

                lines.Add("");
                lines.Add("=== " + name + " ===");

                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    var renderer = t.GetComponent<Renderer>();
                    string size = renderer != null
                        ? "  размер " + renderer.bounds.size.ToString("0.00")
                        : "";

                    lines.Add("    " + t.name + size);
                }
            }

            Debug.Log(string.Join(System.Environment.NewLine, lines));
        }

        [MenuItem("Tools/IsoRPG/Показать кости персонажа", priority = 33)]
        public static void ProbeBones()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharactersFolder + "/Rogue_Hooded.fbx");

            if (model == null)
            {
                Debug.LogError("[IsoRPG] Не найдена модель Rogue_Hooded.");
                return;
            }

            var lines = new List<string> { "=== КОСТИ Rogue_Hooded ===" };
            Walk(model.transform, 0, lines);

            var slots = model.GetComponentsInChildren<Transform>(true)
                .Where(b => b.name.ToLower().Contains("hand") ||
                            b.name.ToLower().Contains("slot") ||
                            b.name.ToLower().Contains("weapon"))
                .Select(b => "    " + b.name)
                .ToList();

            lines.Add("");
            lines.Add("=== ПОХОЖИЕ НА ДЕРЖАТЕЛИ ===");
            lines.AddRange(slots.Count > 0 ? slots : new List<string> { "    ничего не нашлось" });

            Debug.Log(string.Join(System.Environment.NewLine, lines));
        }

        private static void Walk(Transform node, int depth, List<string> into)
        {
            into.Add(new string(' ', depth * 2) + node.name);

            // Глубже пятого уровня начинаются пальцы и мелочь — они не нужны,
            // а список раздувают втрое.
            if (depth >= 5) return;

            foreach (Transform child in node) Walk(child, depth + 1, into);
        }

        [MenuItem("Tools/IsoRPG/Показать содержимое KayKit", priority = 32)]
        public static void Probe()
        {
            var report = new List<string>();

            report.Add("=== АНИМАЦИИ ===");

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview"))
                    .OrderBy(c => c.name)
                    .ToList();

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;

                report.Add("");
                report.Add(System.IO.Path.GetFileName(path) + "  (клипов: " + clips.Count +
                           ", риг: " + (importer != null ? importer.animationType.ToString() : "?") + ")");

                foreach (var clip in clips)
                    report.Add("    " + clip.name + "  " + clip.length.ToString("0.00") + " с" +
                               (clip.isLooping ? ", зациклен" : ""));
            }

            report.Add("");
            report.Add("=== ПЕРСОНАЖИ ===");

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                int bones = go != null
                    ? go.GetComponentsInChildren<Transform>(true).Length
                    : 0;

                report.Add("    " + System.IO.Path.GetFileName(path) +
                           "  риг: " + (importer != null ? importer.animationType.ToString() : "?") +
                           ", узлов: " + bones +
                           ", масштаб: " + (importer != null ? importer.globalScale.ToString("0.###") : "?"));
            }

            Debug.Log(string.Join("\n", report));
        }
    }
}
