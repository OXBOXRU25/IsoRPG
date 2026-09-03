using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит игре свой указатель мыши.
    ///
    /// Импорт настраивается ЗДЕСЬ, а не руками: текстура курсора обязана быть
    /// без сжатия и без мипмап. Сжатие рвёт край прозрачности блочными
    /// артефактами — на картинке в полэкрана это незаметно, а на сорока восьми
    /// пикселях по краю пальца идёт грязь. Мипмапы курсору не нужны вовсе: он
    /// всегда рисуется в одном размере.
    ///
    /// Тот же класс ошибки, что с иконками кнопок 03.09.2026: неправильно
    /// размеченная текстура не находится молча, без единого предупреждения.
    /// Поэтому и здесь — настроить, потом загрузить, и вслух сказать, что
    /// нашлось.
    /// </summary>
    public static class CursorKit
    {
        private const string Folder = "Assets/_Game/Art/UI/Cursors";

        public static void Apply()
        {
            var plain = Prepare(Folder + "/Cursor_Hand.png");
            var lit = Prepare(Folder + "/Cursor_Hand_Glow.png");

            if (plain == null)
            {
                Debug.LogError("[IsoRPG] Нет картинки указателя: " + Folder + "/Cursor_Hand.png");
                return;
            }

            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            var cursor = player.GetComponent<IsoRPG.UI.MouseCursor>();
            if (cursor == null) cursor = player.AddComponent<IsoRPG.UI.MouseCursor>();

            cursor.Setup(plain, lit);
            EditorUtility.SetDirty(cursor);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[IsoRPG] Указатель мыши поставлен: обычный {plain.width}x{plain.height}, " +
                      $"свечение {(lit != null ? lit.width + "x" + lit.height : "НЕТ")}.");
        }

        /// <summary>
        /// Разметить PNG под курсор и вернуть готовую текстуру.
        ///
        /// Тип Cursor, а не Sprite: Unity отдаёт такую текстуру драйверу как
        /// есть, без атласа и без обрезки прозрачных полей — а обрезка сдвинула
        /// бы горячую точку, и указатель начал бы промахиваться.
        /// </summary>
        private static Texture2D Prepare(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Cursor)
            {
                importer.textureType = TextureImporterType.Cursor;
                dirty = true;
            }

            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (importer.maxTextureSize != 64) { importer.maxTextureSize = 64; dirty = true; }

            if (dirty) importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
