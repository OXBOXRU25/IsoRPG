using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Настраивает импорт спрайтов интерфейса, взятых из чужих наборов.
    ///
    /// Зачем заданием, а не руками. Картинки лежат в НАШЕЙ папке
    /// (`_Game/Resources/UI`), а не в наборе: набор в `.gitignore`, и всё, что
    /// мы из него берём, надо унести к себе, иначе на второй машине окно
    /// останется плашкой. Настройки импорта в git тоже попадают только для
    /// наших файлов — поэтому задание переписывает их у нас, а не в наборе.
    ///
    /// Границы 9-slice — АВТОРСКИЕ, снятые щупом `ui-norms` с 249 префабов
    /// набора, а не подобранные.
    ///
    /// Сначала я поставил рамке 60 вместо авторских 200: рассудил, что при
    /// 200 угол займёт 39% окна. Павлон 01.09.2026: «опять что-то сам
    /// выдумываешь, поизучай, наверняка есть стандарты». Оказалось, Synty
    /// использует эту самую рамку 17 раз и всегда с границами 200 — а угол
    /// не разрастается потому, что рядом стоит множитель ×3, которого я не
    /// знал. Ужимается вся картинка целиком, пропорции держатся сами.
    ///
    /// У горизонтальных полосок вертикальная граница у автора **ноль**
    /// (`Bar_Horizontal05`, границы 50/0): полоска режется только по
    /// горизонтали, а по вертикали картинка тянется целиком. Мои 30 сверху и
    /// снизу и дали «гантели» в полоске высотой 25 точек.
    /// </summary>
    public static class UiSprites
    {
        private struct Entry
        {
            public string Path;
            public Vector4 Border;
            public string Note;
        }

        private static readonly Entry[] Sprites =
        {
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Frame_Synty05.png",
                Border = new Vector4(200, 200, 200, 200),
                Note = "рамка окон, Synty Frame_Box05",
            },
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Bar_Socket.png",
                Border = new Vector4(46, 0, 46, 0),
                Note = "подложка под полоски",
            },
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Bar_Fill_Health.png",
                Border = new Vector4(20, 0, 20, 0),
                Note = "заливка здоровья",
            },
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Bar_Fill_Stamina.png",
                Border = new Vector4(20, 0, 20, 0),
                Note = "заливка выносливости",
            },
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Bar_Fill_Mana.png",
                Border = new Vector4(20, 0, 20, 0),
                Note = "заливка маны",
            },

            // Рамка портрета и слота. Границы по каменной кромке: она в
            // картинке около 26 точек при холсте 188, дальше идёт пустая
            // середина, которую и надо тянуть.
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Frame_Portrait.png",
                Border = new Vector4(26, 26, 26, 26),
                Note = "рамка портрета и слотов",
            },
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Slot_Backing.png",
                Border = new Vector4(20, 20, 20, 20),
                Note = "подложка слота",
            },

            // Крестик закрытия. Целиком, без растяжения: кнопка везде одного
            // размера, тянуть нечего.
            new Entry
            {
                Path = "Assets/_Game/Resources/UI/Button_CloseStone.png",
                Border = Vector4.zero,
                Note = "кнопка закрытия окна",
            },
        };

        [MenuItem("Tools/IsoRPG/Интерфейс: настроить спрайты", priority = 42)]
        public static void Apply()
        {
            var text = new StringBuilder("[IsoRPG] Спрайты интерфейса:\n");

            foreach (var entry in Sprites)
            {
                var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;

                if (importer == null)
                {
                    Debug.LogError("[IsoRPG] Нет файла " + entry.Path);
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = entry.Border;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                importer.SaveAndReimport();

                text.Append("  ").Append(System.IO.Path.GetFileName(entry.Path).PadRight(24))
                    .Append("границы ").Append(entry.Border.x).Append('/').Append(entry.Border.y)
                    .Append(", ").Append(entry.Note).Append('\n');
            }

            AssetDatabase.Refresh();

            // Щуп: перечитываем с диска, а не верим отчёту о правке.
            foreach (var entry in Sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Path);

                text.Append("  проверка ")
                    .Append(System.IO.Path.GetFileName(entry.Path).PadRight(24))
                    .Append(sprite == null
                                ? "СПРАЙТА НЕТ"
                                : $"{sprite.rect.width}×{sprite.rect.height}, границы {sprite.border}")
                    .Append('\n');
            }

            Debug.Log(text.ToString());
        }
    }
}
