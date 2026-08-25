using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Готовит нарисованные панели интерфейса к использованию.
    ///
    /// Unity импортирует PNG как обычную текстуру, а интерфейсу нужен спрайт.
    /// Отдельный пункт меню, а не автоматика при импорте: настройки разные у
    /// разных картинок, и держать их списком в одном месте честнее, чем
    /// угадывать по имени файла в постпроцессоре.
    ///
    /// Главное здесь — границы растяжения. Рамка и кнопка обязаны тянуться
    /// под любой размер, но углы и торцы при этом растягивать нельзя: у
    /// кнопки это металлические наконечники с заклёпками, у рамки — уголки.
    /// Четыре числа задают, сколько пикселей с каждой стороны считать
    /// неприкосновенными.
    /// </summary>
    public static class UiFramesImporter
    {
        // В Resources, а не в Art: интерфейс боя строится кодом в рантайме,
        // и достать спрайт по пути можно только оттуда. Ссылку в сцене здесь
        // не положишь — панели создаются на лету, сцена о них не знает.
        private const string Folder = "Assets/_Game/Resources/UI";

        /// <summary>
        /// Границы растяжения: файл и отступы слева, снизу, справа, сверху.
        ///
        /// Нули означают, что картинка используется целиком и не тянется —
        /// так у портретных рамок и слотов, у них фиксированный размер.
        /// </summary>
        private static readonly (string name, Vector4 border)[] Sliced =
        {
            ("Frame_Window",    new Vector4(110f, 110f, 110f, 110f)),
            ("Frame_Abilities", new Vector4(210f, 90f, 210f, 90f)),
            ("Button_Gold",     new Vector4(130f, 60f, 130f, 60f)),
            ("Button_Plain",    new Vector4(115f, 60f, 115f, 60f)),
            ("Button_Danger",   new Vector4(115f, 60f, 115f, 60f)),
        };

        [MenuItem("Tools/IsoRPG/Настроить панели интерфейса", priority = 16)]
        public static void Setup()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogError("[IsoRPG] Нет папки " + Folder +
                               " — панели интерфейса ещё не положены в проект.");
                return;
            }

            int done = 0;

            foreach (string path in Directory.GetFiles(Folder, "*.png"))
            {
                string asset = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;

                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;

                // Мипмапы интерфейсу вредны: спрайт всегда рисуется в
                // плоскости экрана, а уменьшенные копии его только мылят.
                importer.mipmapEnabled = false;

                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;

                // Панели крупные и рисуются во всю ширину экрана: потолок
                // размера поднимаем, иначе Unity ужмёт их до 2048 и торцы
                // станут мягкими.
                importer.maxTextureSize = 4096;

                string file = Path.GetFileNameWithoutExtension(asset);
                var border = Vector4.zero;

                foreach (var entry in Sliced)
                {
                    if (entry.name != file) continue;

                    border = entry.border;
                    break;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
                done++;
            }

            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Панели интерфейса настроены: " + done + " шт. " +
                      "Растягиваемых с границами: " + Sliced.Length + ".");
        }
    }
}
