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
        /// Картинки, которые рисуются НАМНОГО мельче, чем нарисованы, — им
        /// нужны мипмапы, всем остальным вредны.
        ///
        /// Порог примерно четырёхкратный: до него разница не видна, после
        /// неё картинка без мипмапов рассыпается в шум. Сюда попадает то,
        /// что сходится в точку: гнёзда комбо (495 на экране в 11).
        ///
        /// Список, а не вычисление по размеру файла: настоящий экранный
        /// размер живёт в коде интерфейса, импортёр его не знает и знать не
        /// должен. Добавляя сюда имя, стоит написать рядом, во сколько раз
        /// картинка ужимается, — тогда видно, не устарела ли строка.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> Detailed =
            new System.Collections.Generic.HashSet<string>
        {
            "Combo_Empty",   // 495 -> 11
            "Combo_Full",    // 495 -> 11
        };

        /// <summary>
        /// Границы растяжения: файл и отступы слева, снизу, справа, сверху.
        ///
        /// Нули означают, что картинка используется целиком и не тянется —
        /// так у портретных рамок и слотов, у них фиксированный размер.
        /// </summary>
        private static readonly (string name, Vector4 border)[] Sliced =
        {
            // Порядок в Vector4 — (слева, снизу, справа, сверху).
            //
            // Числа сняты замером, а не на глаз: скрипт ищет, с какого места
            // столбец картинки становится похож на её середину — то есть где
            // кончается неповторимый торец и начинается то, что можно тянуть.
            // Проверены растяжением до реальных размеров окон, прежде чем
            // попасть сюда.
            ("Frame_Window",    new Vector4(263f, 224f, 280f, 248f)),
            ("Frame_Abilities", new Vector4(210f,  90f, 210f,  90f)),

            ("Button_Gold",     new Vector4(130f,  60f, 130f,  60f)),
            ("Button_Plain",    new Vector4(115f,  60f, 115f,  60f)),
            ("Button_Danger",   new Vector4(115f,  60f, 115f,  60f)),

            // Горизонтальные плашки: по вертикали нули — значит картинка
            // тянется по высоте целиком, а торцы слева и справа держатся.
            ("Frame_TitlePlate", new Vector4(291f, 0f, 269f, 0f)),
            ("Frame_ListRow",    new Vector4(151f, 0f, 139f, 0f)),
            ("Frame_Divider",    new Vector4( 44f, 0f,  29f, 0f)),
            ("Slider_Track",     new Vector4(100f, 0f, 100f, 0f)),
            ("Tab_Active",       new Vector4( 79f, 0f,  71f, 0f)),
            ("Tab_Idle",         new Vector4( 20f, 0f,  28f, 0f)),

            // Тянутся в обе стороны.
            ("Frame_Inset",     new Vector4(145f, 127f, 145f,  81f)),
            ("Frame_ListPanel", new Vector4(137f, 110f, 153f, 111f)),

            // Вертикальные: нули по горизонтали.
            ("Scroll_Track",    new Vector4(0f, 133f, 0f, 116f)),
            ("Scroll_Handle",   new Vector4(0f,  57f, 0f,  53f)),
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

            // Вместе с подпапками — иначе портреты остаются обычными
            // текстурами.
            //
            // Так и было: папка Portraets не обходилась, у портретов стоял
            // textureType 0, и Resources.Load<Sprite> честно возвращал пусто.
            // Игра при этом не падала — код брал запасной путь и рисовал
            // рендер модели, — поэтому беда прожила незамеченной с первого
            // дня. Ошибки нет, предупреждения нет, просто вместо лица фигурка.
            foreach (string path in Directory.GetFiles(Folder, "*.png",
                                                       SearchOption.AllDirectories))
            {
                string asset = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;

                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;

                // Мипмапы интерфейсу вредны — но только пока спрайт рисуется
                // примерно в свою величину: тогда уменьшенные копии его
                // действительно мылят. Как только картинку ужимают в разы,
                // правило переворачивается, и без мипмапов она превращается
                // в шум: билинейная фильтрация берёт четыре точки из тысяч,
                // и какие именно — решает случай.
                //
                // Гнездо комбо нарисовано 495 на 495, а на экране это 11
                // пикселей — сжатие в сорок пять раз. Ряд точек под панелью
                // цели из-за этого читался как тёмная крошка, и в игре его
                // просто не видели.
                importer.mipmapEnabled = Detailed.Contains(
                    Path.GetFileNameWithoutExtension(asset));

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
