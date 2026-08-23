using UnityEditor;
using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Раздаёт иконки предметам и способностям по именам файлов.
    ///
    /// По именам, а не вручную в инспекторе: иконок будет столько же, сколько
    /// предметов, и перетаскивать их мышью придётся каждый раз, когда
    /// добавился новый. Правило «файл называется как ассет» переживает любое
    /// пополнение и не требует ничего помнить.
    /// </summary>
    public static class IconBinder
    {
        private const string ItemIcons = "Assets/_Game/Art/UI/Icons/Items";
        private const string AbilityIcons = "Assets/_Game/Art/UI/Icons/Abilities";
        private const string ButtonIcons = "Assets/_Game/Art/UI/Icons/Buttons";
        private const string SlotIcons = "Assets/_Game/Art/UI/Icons/Slots";
        private const string PortraitIcons = "Assets/_Game/Art/UI/Icons/Portraits";
        private const string ItemsFolder = "Assets/_Game/Data/Items";
        private const string AbilitiesFolder = "Assets/_Game/Data/Abilities";

        [MenuItem("Tools/IsoRPG/Раздать иконки", priority = 17)]
        public static void Bind()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            PrepareSprites(ItemIcons);
            PrepareSprites(AbilityIcons);
            PrepareSprites(ButtonIcons);
            PrepareSprites(SlotIcons);
            PrepareSprites(PortraitIcons);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int items = BindItems();
            int abilities = BindAbilities();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Иконки розданы: предметам " + items +
                      ", способностям " + abilities + ".");
        }

        // ------------------------------------------------------------------

        private static int BindItems()
        {
            int bound = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ItemIcons + "/" + item.name + ".png");
                if (sprite == null || item.icon == sprite) continue;

                item.icon = sprite;
                EditorUtility.SetDirty(item);
                bound++;
            }

            return bound;
        }

        private static int BindAbilities()
        {
            int bound = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:AbilityDefinition", new[] { AbilitiesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (ability == null) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AbilityIcons + "/" + ability.name + ".png");
                if (sprite == null || ability.icon == sprite) continue;

                ability.icon = sprite;
                EditorUtility.SetDirty(ability);
                bound++;
            }

            return bound;
        }

        /// <summary>
        /// Настраивает импорт: PNG должен стать спрайтом с прозрачностью.
        ///
        /// Без этого иконка лежит в проекте, выглядит правильно в папке — и
        /// просто не появляется на экране. Ошибка тихая: ни предупреждения,
        /// ни ошибки, только пустое место.
        /// </summary>
        public static void PrepareSprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[IsoRPG] Нет папки " + folder);
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                // Одиночный спрайт, а не атлас. При режиме Multiple Unity
                // ждёт нарезку на подспрайты, и запрос спрайта по пути
                // возвращает пусто — иконка молча не находится.
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }

                // Без сжатия: иконка мелкая, а блочные артефакты по краю
                // прозрачности заметны даже на ней.
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (importer.maxTextureSize > 256)
                {
                    // В интерфейсе иконка не больше 64 пикселей даже на
                    // экране двойной плотности. 256 — запас вчетверо.
                    importer.maxTextureSize = 256;
                    dirty = true;
                }

                if (dirty) importer.SaveAndReimport();
            }
        }
    }
}
