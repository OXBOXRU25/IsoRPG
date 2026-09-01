using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит картинки интерфейса в спрайты.
    ///
    /// В трёхмерном проекте Unity импортирует PNG как текстуру, и
    /// <c>Resources.Load&lt;Sprite&gt;</c> возвращает пустоту — молча, без
    /// единой ошибки. Именно тот случай, когда «ничего не показывается» и
    /// «всё работает» выглядят одинаково, поэтому в конце — щуп: считаем,
    /// сколько спрайтов реально загрузилось.
    ///
    /// Иконки нарезаны 01.09.2026 из сеток Павлона: 16 подложек пустых
    /// гнёзд и 4 готовых предмета.
    /// </summary>
    public static class SpriteImport
    {
        private static readonly string[] Folders =
        {
            "Assets/_Game/Resources/SlotIcons",
            "Assets/_Game/Resources/ItemIcons",
        };

        public static void Apply()
        {
            int touched = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    string path = file.Replace(Path.DirectorySeparatorChar, (char)47);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    if (importer.textureType == TextureImporterType.Sprite) continue;

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    // Прозрачности в этих картинках нет, но альфа-канал нужен
                    // спрайту, иначе Unity подмешивает непрозрачный фон.
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                    touched++;
                }
            }

            AssetDatabase.Refresh();

            // Щуп: грузим ровно так, как это сделает игра.
            var loaded = Resources.LoadAll<Sprite>("SlotIcons").Select(s => s.name).OrderBy(n => n).ToArray();
            var items = Resources.LoadAll<Sprite>("ItemIcons").Select(s => s.name).OrderBy(n => n).ToArray();

            Debug.Log(
                $"[IsoRPG] Спрайты интерфейса: переимпортировано {touched}.\n" +
                $"  подложки гнёзд ({loaded.Length}): {string.Join(", ", loaded)}\n" +
                $"  иконки предметов ({items.Length}): {string.Join(", ", items)}");
        }
    }
}
