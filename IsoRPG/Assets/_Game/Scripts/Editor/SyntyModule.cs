using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Замер модуля набора: габариты ключевых деталей и положение их точки
    /// отсчёта.
    ///
    /// Без этого новый словарь не написать. Модульная сборка держится на
    /// двух числах у каждой детали: сколько она занимает и где у неё ноль.
    /// Стена с пивотом в центре и стена с пивотом в углу ставятся по разным
    /// формулам, и разница вылезает не сразу, а щелью в полметра на десятой
    /// клетке.
    ///
    /// Прикидывать эти числа нельзя: у KayKit клетка вышла 4 метра, у Synty
    /// она своя, и «наверное, тоже четыре» — самый дорогой способ узнать,
    /// что нет.
    ///
    /// Пишет отчёт в shots/synty-module.txt.
    /// </summary>
    public static class SyntyModule
    {
        private const string Root = "Assets/PolygonDungeon/Prefabs/Environments";

        /// <summary>
        /// Что меряем. По одной детали каждого рода — этого хватает, чтобы
        /// понять сетку; остальные того же рода повторяют модуль.
        /// </summary>
        private static readonly (string group, string path)[] Probes =
        {
            ("стена",           "Walls/SM_Env_Wall_01.prefab"),
            ("стена вариант",   "Walls/SM_Env_Wall_02.prefab"),
            ("стена двусторонняя", "Walls/SM_Env_Wall_01_DoubleSided.prefab"),
            ("стена торец",     "Walls/SM_Env_Wall_Culled_01.prefab"),
            ("стена битая",     "Walls/SM_Env_Wall_Broken_Edge_01.prefab"),
            ("проём",           "Walls/SM_Env_Wall_DoorFrame_01.prefab"),
            ("проём двойной",   "Walls/SM_Env_Wall_DoorFrame_Double_01.prefab"),
            ("проём арочный",   "Walls/SM_Env_Wall_Archway_01.prefab"),
            ("окно",            "Walls/SM_Env_Wall_Window_01.prefab"),
            ("окно с решёткой", "Walls/SM_Env_Wall_Window_Bars_01.prefab"),
            ("ниша",            "Walls/SM_Env_Wall_Alcove_Round_01.prefab"),
            ("пол",             "Floors/SM_Env_Tiles_01.prefab"),
            ("пол вариант",     "Floors/SM_Env_Tiles_05.prefab"),
            ("потолок",         "Misc/SM_Env_Ceiling_Stone_Curved_01.prefab"),
            ("потолок арка",    "Misc/SM_Env_Ceiling_Arch_01.prefab"),
            ("колонна",         "Pillars/SM_Env_Pillar_01.prefab"),
            ("колонна битая",   "Pillars/SM_Env_Pillar_Broken_01.prefab"),
            ("лестница",        "Misc/SM_Env_Stairs_01.prefab"),
        };

        [MenuItem("Tools/IsoRPG/Замерить модуль Synty", priority = 51)]
        public static void Measure()
        {
            var report = new StringBuilder();

            report.AppendLine("МОДУЛЬ POLYGON DUNGEONS");
            report.AppendLine("Размер — габариты по нарисованным границам.");
            report.AppendLine("Смещение — где центр детали относительно её точки отсчёта:");
            report.AppendLine("нули означают пивот в центре, половина размера — пивот у края.");
            report.AppendLine();

            foreach (var (group, path) in Probes)
            {
                string full = Root + "/" + path;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(full);

                if (asset == null)
                {
                    report.AppendLine(group.PadRight(22) + "НЕТ ФАЙЛА  " + path);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                var renderers = instance.GetComponentsInChildren<Renderer>()
                                        .Where(r => !(r is ParticleSystemRenderer))
                                        .ToArray();

                if (renderers.Length == 0)
                {
                    report.AppendLine(group.PadRight(22) + "без видимой части  " + path);
                    Object.DestroyImmediate(instance);
                    continue;
                }

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                report.AppendLine(group.PadRight(22) + Path.GetFileNameWithoutExtension(path));
                report.AppendLine("    размер     " + V(bounds.size));
                report.AppendLine("    смещение   " + V(bounds.center) +
                                  "   низ " + bounds.min.y.ToString("0.00"));

                // Коллайдеры важны отдельно: по ним пойдёт навигация, и если
                // их нет вовсе, стены не станут стенами.
                var colliders = instance.GetComponentsInChildren<Collider>();
                report.AppendLine("    коллайдеры " + colliders.Length);
                report.AppendLine();

                Object.DestroyImmediate(instance);
            }

            // Отдельно — сколько всего вариантов каждого рода: словарь будет
            // выбирать из них случайно, и знать запас полезно сразу.
            report.AppendLine("=== ЗАПАС ВАРИАНТОВ ===");

            foreach (var (label, mask) in new[]
                     {
                         ("стены",   "SM_Env_Wall_0"),
                         ("торцы",   "SM_Env_Wall_Culled"),
                         ("проёмы",  "SM_Env_Wall_DoorFrame"),
                         ("окна",    "SM_Env_Wall_Window"),
                         ("полы",    "SM_Env_Tiles_"),
                         ("потолки", "SM_Env_Ceiling"),
                         ("колонны", "SM_Env_Pillar"),
                     })
            {
                int n = AssetDatabase.FindAssets("t:Prefab " + mask, new[] { Root }).Length;
                report.AppendLine(label.PadRight(10) + n);
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "shots");
            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, "synty-module.txt");
            File.WriteAllText(file, report.ToString());

            Debug.Log("[IsoRPG] Модуль замерен: " + file);
        }

        private static string V(Vector3 v) =>
            "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
    }
}
