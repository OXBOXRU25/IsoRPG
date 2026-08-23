using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Настраивает импорт палитровых текстур.
    ///
    /// У наборов вроде KayKit текстура — это не картинка, а палитра: сетка
    /// плоских цветовых блоков, куда модель ссылается координатами. Отсюда
    /// 14 КБ при разрешении 1024.
    ///
    /// Настройки Unity по умолчанию для такой текстуры вредны сразу дважды.
    /// Блочное сжатие рассчитано на плавные фотографические переходы и на
    /// резкой границе двух чистых цветов даёт грязь — она и читается как
    /// «пиксельная текстура». А билинейная фильтрация размывает соседние
    /// блоки друг в друга, и на модели появляются цвета, которых в палитре
    /// нет вообще.
    ///
    /// Обе беды снимаются двумя галочками, и платить за это нечем: без
    /// сжатия такая текстура весит меньше, чем сжатая фотография.
    /// </summary>
    public static class TextureSetup
    {
        private static readonly string[] PaletteFolders =
        {
            "Assets/_Game/Art/KayKit",
        };

        [MenuItem("Tools/IsoRPG/Настроить палитровые текстуры", priority = 31)]
        public static void Apply()
        {
            int changed = 0, seen = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", PaletteFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                seen++;

                bool dirty = false;

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    dirty = true;
                }

                // Мипмапы оставляем включёнными: без них палитра начинает
                // рябить на удалении, а у нас персонаж почти всегда далеко.
                if (!importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = true;
                    dirty = true;
                }

                // Апскейл в степень двойки не нужен — текстуры и так 1024.
                if (importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log("[IsoRPG] Палитровые текстуры: просмотрено " + seen +
                      ", перенастроено " + changed + ".");
        }
    }
}
