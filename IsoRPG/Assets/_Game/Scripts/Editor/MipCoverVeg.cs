using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Включает сохранение покрытия альфы у растительности набора PNB.
    ///
    /// Причина мигания цветов, найденная 02.09.2026. У растений отсечение по
    /// альфе с порогом 0.5. Мип-уровни усредняют альфу, тонкий лепесток на
    /// дальнем уровне уходит ниже порога — и цветок пропадает. Камера ближе,
    /// мип-уровень другой — появляется. Отсюда «исчезают и появляются», и
    /// отсюда же — что вблизи всё в порядке, а на дальнем зуме нет.
    ///
    /// Такое у нас уже лечили заданием `mipcover`, но оно правит РОВНО ОДНУ
    /// текстуру из набора TriForge — до PolygonNatureBiomes очередь не дошла
    /// ни разу. Это тот самый случай «список, собранный в двух местах»: одна
    /// и та же беда чинилась для одного набора и осталась для другого.
    ///
    /// Отбираем по ФАКТУ наличия альфы в исходнике, а не по имени файла:
    /// имя — подпись художника, а не свойство текстуры. Карты нормалей и
    /// маски не трогаем: там альфа значит другое.
    ///
    /// **Прогонять после каждого переимпорта набора** — папка в `.gitignore`,
    /// и настройки импорта уезжают вместе с ней.
    /// </summary>
    public static class MipCoverVeg
    {
        private static readonly string[] Folders =
        {
            "Assets/PolygonNatureBiomes",
        };

        [MenuItem("Tools/IsoRPG/Мир: цветам не мигать (покрытие альфы)", priority = 34)]
        public static void Apply()
        {
            int looked = 0, fixedCount = 0, already = 0, noAlpha = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                                     .Where(p => p.EndsWith(".png") || p.EndsWith(".tga") ||
                                                 p.EndsWith(".psd") || p.EndsWith(".jpg"));

                foreach (var raw in files)
                {
                    string path = raw.Replace('\\', '/');

                    // Карты нормалей и маски: альфа там служебная, порога
                    // отсечения у них нет, и трогать её незачем.
                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (name.EndsWith("_normals") || name.EndsWith("_normal") ||
                        name.Contains("mask") || name.Contains("_mra") || name.Contains("_orm"))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    looked++;

                    if (importer.textureType != TextureImporterType.Default) continue;
                    if (!importer.mipmapEnabled) continue;

                    // Главный отбор: есть ли в исходнике альфа вообще.
                    if (!importer.DoesSourceTextureHaveAlpha()) { noAlpha++; continue; }

                    if (importer.mipMapsPreserveCoverage) { already++; continue; }

                    importer.mipMapsPreserveCoverage = true;

                    // Порог берём тот же, что стоит в шейдере растительности.
                    importer.alphaTestReferenceValue = 0.5f;

                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }

            AssetDatabase.Refresh();

            // Щуп: перечитываем с диска. Отчёт о правке подтверждает лишь то,
            // что код дошёл до строки.
            int left = 0;

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                left += Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                                 .Where(p => p.EndsWith(".png") || p.EndsWith(".tga"))
                                 .Select(p => AssetImporter.GetAtPath(p.Replace('\\', '/')) as TextureImporter)
                                 .Count(i => i != null && i.mipmapEnabled &&
                                             i.textureType == TextureImporterType.Default &&
                                             i.DoesSourceTextureHaveAlpha() &&
                                             !i.mipMapsPreserveCoverage);
            }

            Debug.Log($"[IsoRPG] Покрытие альфы: просмотрено {looked}, включено {fixedCount}, " +
                      $"уже было {already}, без альфы {noAlpha}, осталось без покрытия {left}.");
        }
    }
}
