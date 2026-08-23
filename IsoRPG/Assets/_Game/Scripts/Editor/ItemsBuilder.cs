using System;
using UnityEditor;
using UnityEngine;
using IsoRPG.Items;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Создаёт стартовый набор предметов и таблицы добычи.
    ///
    /// Всё числами в ассетах: Павлон может править урон, броню и шансы прямо
    /// в инспекторе, не трогая код. Ради этого предметы и заводились данными.
    /// </summary>
    public static class ItemsBuilder
    {
        private const string ItemsFolder = "Assets/_Game/Data/Items";
        private const string LootFolder = "Assets/_Game/Data/Loot";

        [MenuItem("Tools/IsoRPG/Создать предметы и добычу", priority = 32)]
        public static void Build()
        {
            EnsureFolder(ItemsFolder);
            EnsureFolder(LootFolder);

            // --- Оружие ---

            var rustyDagger = CreateItem("I_RustyDagger", item =>
            {
                item.displayName = "Ржавый кинжал";
                item.description = "Видал лучшие дни. Но колет.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0x9A, 0x8A, 0x76, 0xFF);
                item.slot = EquipSlot.MainHand;
                item.weaponDamage = 8;
                item.attackInterval = 1.3f;
                item.vendorPrice = 4;
            });

            var banditDagger = CreateItem("I_BanditDagger", item =>
            {
                item.displayName = "Кинжал бандита";
                item.description = "Лёгкий и злой. Кто-то точил его каждый вечер.";
                item.rarity = ItemRarity.Uncommon;
                item.iconColor = new Color32(0x8A, 0xB8, 0x6A, 0xFF);
                item.slot = EquipSlot.MainHand;
                item.weaponDamage = 14;
                item.attackInterval = 1.3f;
                item.agility = 3;
                item.vendorPrice = 25;
            });

            // --- Броня ---

            var leatherChest = CreateItem("I_LeatherChest", item =>
            {
                item.displayName = "Кожаный нагрудник";
                item.description = "Потёртая кожа, но швы крепкие.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0x8A, 0x6A, 0x4A, 0xFF);
                item.slot = EquipSlot.Chest;
                item.armor = 30;
                item.stamina = 2;
                item.vendorPrice = 12;
            });

            var swiftRing = CreateItem("I_SwiftRing", item =>
            {
                item.displayName = "Кольцо ловкача";
                item.description = "Тонкая работа. Пальцы сами тянутся к кинжалу.";
                item.rarity = ItemRarity.Rare;
                item.iconColor = new Color32(0x50, 0x90, 0xE0, 0xFF);
                item.slot = EquipSlot.Ring;
                item.agility = 6;
                item.requiredLevel = 2;
                item.vendorPrice = 90;
            });

            // --- Хлам на продажу ---

            var pelt = CreateItem("I_Pelt", item =>
            {
                item.displayName = "Грубая шкура";
                item.description = "Пахнет. Торговцы берут.";
                item.rarity = ItemRarity.Junk;
                item.iconColor = new Color32(0x7A, 0x6A, 0x5A, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = true;
                item.maxStack = 20;
                item.vendorPrice = 2;
            });

            var buckle = CreateItem("I_Buckle", item =>
            {
                item.displayName = "Погнутая пряжка";
                item.description = "Ничего не стоит, но кто-то же её купит.";
                item.rarity = ItemRarity.Junk;
                item.iconColor = new Color32(0x8A, 0x82, 0x70, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = true;
                item.maxStack = 20;
                item.vendorPrice = 1;
            });

            // --- Таблицы добычи ---

            CreateLoot("LT_Bandit", table =>
            {
                table.minGold = 1;
                table.maxGold = 18;
                table.goldChance = 1f;
                table.entries = new[]
                {
                    Entry(pelt, 0.55f, 1, 2),
                    Entry(buckle, 0.3f, 1, 1),
                    Entry(rustyDagger, 0.12f, 1, 1),
                    Entry(banditDagger, 0.05f, 1, 1),
                };
            });

            CreateLoot("LT_Thug", table =>
            {
                table.minGold = 1;
                table.maxGold = 45;
                table.goldChance = 1f;
                table.entries = new[]
                {
                    Entry(pelt, 0.5f, 1, 3),
                    Entry(leatherChest, 0.28f, 1, 1),
                    Entry(banditDagger, 0.15f, 1, 1),
                    Entry(swiftRing, 0.04f, 1, 1),
                };
            });

            CreateLoot("LT_Drifter", table =>
            {
                table.minGold = 1;
                table.maxGold = 28;
                table.goldChance = 1f;
                table.entries = new[]
                {
                    Entry(pelt, 0.5f, 1, 2),
                    Entry(buckle, 0.35f, 1, 2),
                    Entry(leatherChest, 0.1f, 1, 1),
                };
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IsoRPG] Предметы и таблицы добычи созданы.");
        }

        private static LootEntry Entry(ItemDefinition item, float chance, int min, int max) =>
            new LootEntry { item = item, chance = chance, minCount = min, maxCount = max };

        public static LootTable LoadTable(string name) =>
            AssetDatabase.LoadAssetAtPath<LootTable>(LootFolder + "/" + name + ".asset");

        public static ItemDefinition LoadItem(string name) =>
            AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "/" + name + ".asset");

        // ------------------------------------------------------------------

        private static ItemDefinition CreateItem(string fileName, Action<ItemDefinition> configure)
        {
            string path = ItemsFolder + "/" + fileName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (existing != null) return existing;   // правки Павлона не затираем

            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            configure(item);
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        private static void CreateLoot(string fileName, Action<LootTable> configure)
        {
            string path = LootFolder + "/" + fileName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (existing != null) return;

            var table = ScriptableObject.CreateInstance<LootTable>();
            configure(table);
            AssetDatabase.CreateAsset(table, path);
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
