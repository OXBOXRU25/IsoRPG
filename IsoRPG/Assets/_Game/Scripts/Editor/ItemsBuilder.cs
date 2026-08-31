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
                item.dualWieldable = true;
                item.vendorPrice = 4;
                item.worldModel = LoadModel("dagger");
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
                item.dualWieldable = true;
                item.agility = 3;
                item.vendorPrice = 25;
                item.worldModel = LoadModel("dagger");
            });

            var skeletonBone = CreateItem("I_SkeletonBone", item =>
            {
                item.displayName = "Кость скелета";
                item.description = "Выбелена временем. Кому-то такие зачем-то нужны.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0xD8, 0xD2, 0xC0, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = true;
                item.maxStack = 20;
                item.vendorPrice = 2;
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

            // Трофей охоты на кабанов. Падает с каждого без исключения:
            // квест считает прогресс по сумке, и случайный дроп превратил бы
            // «убей двенадцать» в «убей сколько-то, как повезёт» — а это
            // разные обещания.
            var boarTusk = CreateItem("I_BoarTusk", item =>
            {
                item.displayName = "Клык кабана";
                item.description = "Жёлтый, щербатый. Доказательство охоты.";
                item.rarity = ItemRarity.Junk;
                item.iconColor = new Color32(0xD8, 0xC8, 0x9A, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = true;
                item.maxStack = 20;
                item.vendorPrice = 3;
            });

            // Награды за охоту Талина Кини. Игрок выбирает ОДНИ из двух:
            // кожаные вчетверо крепче, тканевые смешные — и в этом весь
            // выбор, он про вкус, а не про цифры.
            var leatherBreeches = CreateItem("I_LeatherBreeches", item =>
            {
                item.displayName = "Кожаные бриджи холдея";
                item.description = "Латаные, но крепкие. Держат удар.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0xC8, 0x8A, 0x3A, 0xFF);
                item.slot = EquipSlot.Legs;
                item.armor = 29;
                item.stackable = false;
                item.vendorPrice = 12;
            });

            var clothPantaloons = CreateItem("I_ClothPantaloons", item =>
            {
                item.displayName = "Тканевые панталоны бабушки Талина Кини";
                item.description = "С кружевом и незабудками. Бабушка вязала сама.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0xE8, 0xDE, 0xC0, 0xFF);
                item.slot = EquipSlot.Legs;
                item.armor = 9;
                item.stackable = false;
                item.vendorPrice = 9;
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

            var necklace = CreateItem("I_BoneNecklace", item =>
            {
                item.displayName = "Костяной амулет";
                item.description = "Кость, шнур и чья-то вера, что это помогает.";
                item.rarity = ItemRarity.Uncommon;
                item.iconColor = new Color32(0xC8, 0xBE, 0xA0, 0xFF);
                item.slot = EquipSlot.Necklace;
                item.stamina = 4;
                item.vendorPrice = 18;
            });

            var cloak = CreateItem("I_WornCloak", item =>
            {
                item.displayName = "Потёртый плащ";
                item.description = "Греет хуже, чем прячет.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0x5A, 0x4A, 0x62, 0xFF);
                item.slot = EquipSlot.Cloak;
                item.armor = 4;
                item.agility = 2;
                item.vendorPrice = 12;
            });

            var signet = CreateItem("I_ThiefSignet", item =>
            {
                item.displayName = "Воровская печатка";
                item.description = "Печать цела, а вот дом, где ей пользовались, — нет.";
                item.rarity = ItemRarity.Uncommon;
                item.iconColor = new Color32(0xC8, 0xA8, 0x5A, 0xFF);
                item.slot = EquipSlot.Ring;
                item.agility = 3;
                item.vendorPrice = 22;
            });

            var dart = CreateItem("I_Dart", item =>
            {
                item.displayName = "Метательный дротик";
                item.description = "Летит недалеко, зато тихо.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0x8A, 0x8A, 0x94, 0xFF);
                item.slot = EquipSlot.Ranged;
                item.agility = 1;
                item.vendorPrice = 6;
            });

            var cryptKey = CreateItem("I_CryptKey", item =>
            {
                item.displayName = "Ключ от склепа";
                item.description = "Тяжелее, чем должен быть. Череп на головке смотрит вбок.";
                item.rarity = ItemRarity.Rare;
                item.iconColor = new Color32(0x8A, 0x84, 0x70, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = false;
                item.vendorPrice = 0;
            });

            // Награда за босса. Единственная эпическая вещь, которую нельзя
            // выбить удачей: она падает раз и только с него.
            var bossRing = CreateItem("I_RingOfTheBoneLord", item =>
            {
                item.displayName = "Печать Костяного владыки";
                item.description = "Камень до сих пор тёплый. Это не к добру.";
                item.rarity = ItemRarity.Epic;
                item.iconColor = new Color32(0x9A, 0x4C, 0xC8, 0xFF);
                item.slot = EquipSlot.Ring;
                item.requiredLevel = 3;
                item.agility = 6;
                item.stamina = 5;
                item.armor = 3;
                item.vendorPrice = 240;
            });

            var apple = CreateItem("I_Apple", item =>
            {
                item.displayName = "Спелое красное яблоко";
                item.description = "Кто-то нёс его домой и не дошёл.";
                item.rarity = ItemRarity.Common;
                item.iconColor = new Color32(0xC8, 0x3A, 0x32, 0xFF);
                item.slot = EquipSlot.None;
                item.stackable = true;
                item.maxStack = 20;
                item.vendorPrice = 1;

                // Лечит много, но долго и только в покое: это отдых
                // между схватками, а не глоток посреди боя.
                item.healAmount = 60;
                item.healDuration = 18f;
            });

            var epicDagger = CreateItem("I_ShadowfangDagger", item =>
            {
                item.displayName = "Клык Тени";
                item.description = "Лёгкий до невесомости. В темноте кажется, что он дышит.";
                item.rarity = ItemRarity.Epic;
                item.iconColor = new Color32(0xA0, 0x6A, 0xD8, 0xFF);
                item.slot = EquipSlot.MainHand;
                item.weaponDamage = 26;
                item.attackInterval = 1.2f;
                item.dualWieldable = true;
                item.agility = 8;
                item.stamina = 4;
                item.requiredLevel = 3;
                item.vendorPrice = 400;
                item.worldModel = LoadModel("dagger");
            });

            // --- Таблицы добычи ---

            CreateLoot("LT_Bandit", table =>
            {
                table.minGold = 3;
                table.maxGold = 14;
                table.goldChance = 0.55f;
                table.entries = new[]
                {
                    Entry(pelt, 0.55f, 1, 2),
                    Entry(skeletonBone, 0.65f, 1, 2),
                    Entry(buckle, 0.3f, 1, 1),
                    Entry(rustyDagger, 0.12f, 1, 1),
                    Entry(banditDagger, 0.05f, 1, 1),
                };
            });

            CreateLoot("LT_Thug", table =>
            {
                table.minGold = 9;
                table.maxGold = 40;
                table.goldChance = 0.7f;
                table.entries = new[]
                {
                    Entry(pelt, 0.5f, 1, 3),
                    Entry(skeletonBone, 0.8f, 1, 3),
                    Entry(leatherChest, 0.28f, 1, 1),
                    Entry(banditDagger, 0.15f, 1, 1),
                    Entry(swiftRing, 0.04f, 1, 1),
                };
            });

            CreateLoot("LT_Drifter", table =>
            {
                table.minGold = 5;
                table.maxGold = 26;
                table.goldChance = 0.6f;
                table.entries = new[]
                {
                    Entry(pelt, 0.5f, 1, 2),
                    Entry(skeletonBone, 0.7f, 1, 2),
                    Entry(buckle, 0.35f, 1, 2),
                    Entry(leatherChest, 0.1f, 1, 1),
                };
            });

            // Разбойники-люди. Золота у них больше, чем у нежити: живые
            // грабят и носят добычу при себе, а у скелета в карманах кости.
            CreateLoot("LT_Bandit_Human", table =>
            {
                table.minGold = 14;
                table.maxGold = 48;
                table.goldChance = 0.85f;
                table.entries = new[]
                {
                    Entry(pelt, 0.45f, 1, 2),
                    Entry(buckle, 0.4f, 1, 2),
                    Entry(apple, 0.5f, 1, 2),
                    Entry(dart, 0.3f, 1, 3),
                    Entry(banditDagger, 0.14f, 1, 1),
                    Entry(cloak, 0.12f, 1, 1),
                };
            });

            CreateLoot("LT_Bandit_Chief", table =>
            {
                table.minGold = 60;
                table.maxGold = 140;
                table.goldChance = 1f;
                table.entries = new[]
                {
                    Entry(apple, 0.9f, 2, 3),
                    Entry(leatherChest, 0.35f, 1, 1),
                    Entry(signet, 0.3f, 1, 1),
                    Entry(necklace, 0.25f, 1, 1),
                    Entry(banditDagger, 0.4f, 1, 1),
                };
            });

            // Сундук владыки. Золота много, вещей мало: главная награда в
            // нём и так лежит гарантированно, а мусор рядом с эпической
            // вещью обесценивает сам момент открытия.
            CreateLoot("LT_Chest", table =>
            {
                table.minGold = 120;
                table.maxGold = 260;
                table.goldChance = 1f;
                table.entries = new[]
                {
                    Entry(apple, 0.8f, 2, 4),
                    Entry(signet, 0.35f, 1, 1),
                    Entry(necklace, 0.3f, 1, 1),
                };
            });

            // Кабан. Клык гарантированно, шкура через раз, золота у зверя нет.
            CreateLoot("LT_Boar", table =>
            {
                table.minGold = 0;
                table.maxGold = 0;
                table.goldChance = 0f;
                table.entries = new[]
                {
                    Entry(boarTusk, 1f, 1, 1),
                    Entry(pelt, 0.45f, 1, 1),
                };
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            int filled = FillMissingModels();
            int lootFilled = FillMissingLootEntries();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Предметы и таблицы добычи созданы. " +
                      "Дозаполнено моделей: " + filled +
                      ", записей в таблицах: " + lootFilled + ".");
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

        /// <summary>
        /// Дописывает в таблицы добычи записи, появившиеся позже самих таблиц.
        ///
        /// То же слепое пятно, что и с полями предметов: сборщик намеренно не
        /// перезаписывает существующие ассеты, чтобы не стирать ручные правки
        /// — и новая строка в таблице не появляется никогда. Снаружи это
        /// выглядит как «предмет не падает», а не как «таблица старая».
        ///
        /// Трогаем только отсутствующее: если запись есть, её шансы и числа
        /// остаются как были.
        /// </summary>
        private static int FillMissingLootEntries()
        {
            // Сборщик не переписывает готовые таблицы — правки Павла в них
            // должны переживать пересборку. Поэтому новый предмет попадает в
            // добычу только здесь, дозаполнением недостающего.
            //
            // Шансы разные: с крупного скелета костей больше, чем с мелкого.
            // Яблоки падают часто и помалу — это расходник, а не награда.
            var wanted = new (string item, string table, float chance, int min, int max)[]
            {
                ("I_SkeletonBone", "LT_Bandit",  0.65f, 1, 2),
                ("I_SkeletonBone", "LT_Thug",    0.80f, 1, 3),
                ("I_SkeletonBone", "LT_Drifter", 0.70f, 1, 2),
                ("I_Apple",        "LT_Bandit",  0.40f, 1, 2),
                ("I_Apple",        "LT_Thug",    0.45f, 1, 3),
                ("I_Apple",        "LT_Drifter", 0.50f, 1, 2),

                // Снаряжение падает редко: вещь, которая выпадает каждый бой,
                // перестаёт быть находкой уже к третьему разу.
                ("I_Dart",         "LT_Bandit",  0.22f, 1, 3),
                ("I_Dart",         "LT_Drifter", 0.18f, 1, 2),
                ("I_WornCloak",    "LT_Bandit",  0.10f, 1, 1),
                ("I_WornCloak",    "LT_Thug",    0.12f, 1, 1),
                ("I_ThiefSignet",  "LT_Thug",    0.07f, 1, 1),
                ("I_ThiefSignet",  "LT_Drifter", 0.05f, 1, 1),
                ("I_BoneNecklace", "LT_Thug",    0.06f, 1, 1),
            };

            int filled = 0;

            foreach (var (itemName, name, chance, min, max) in wanted)
            {
                var bone = LoadItem(itemName);

                if (bone == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет предмета " + itemName + " — в таблицы не добавлен.");
                    continue;
                }

                var table = LoadTable(name);
                if (table == null) continue;

                bool has = false;

                if (table.entries != null)
                {
                    foreach (var entry in table.entries)
                        if (entry.item == bone) { has = true; break; }
                }

                if (has) continue;

                var list = new System.Collections.Generic.List<LootEntry>();
                if (table.entries != null) list.AddRange(table.entries);

                list.Add(Entry(bone, chance, min, max));
                table.entries = list.ToArray();

                EditorUtility.SetDirty(table);
                filled++;
            }

            return filled;
        }

        /// <summary>
        /// Дозаполняет поля, появившиеся позже самих предметов.
        ///
        /// Сборщик намеренно не перезаписывает существующие ассеты, чтобы не
        /// стирать правки в инспекторе. У этого правила есть слепое пятно:
        /// поле, добавленное в описание предмета уже после его создания,
        /// остаётся пустым навсегда — и предмет молча ведёт себя как
        /// сломанный. Здесь мы трогаем только пустое: пустота правкой не
        /// бывает.
        /// </summary>
        private static int FillMissingModels()
        {
            int filled = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

                if (item == null || !item.IsWeapon) continue;

                bool changed = false;

                if (item.worldModel == null)
                {
                    item.worldModel = LoadModel(ModelFor(item));
                    changed = true;
                }

                // Кинжалы у нас парные по замыслу класса. Флаг появился
                // позже самих предметов, поэтому дозаполняем так же, как
                // модель: трогаем только то, чего нет.
                if (!item.dualWieldable && item.name.ToLower().Contains("dagger"))
                {
                    item.dualWieldable = true;
                    changed = true;
                }

                if (!changed) continue;

                EditorUtility.SetDirty(item);
                filled++;
            }

            return filled;
        }

        /// <summary>
        /// Какая модель какому оружию. Пока весь наш арсенал — кинжалы, но
        /// список нужен уже сейчас: с появлением второго типа оружия
        /// подстановка по умолчанию превратится в мечи, выглядящие кинжалами.
        /// </summary>
        private static string ModelFor(ItemDefinition item)
        {
            string name = item.name.ToLower();

            if (name.Contains("sword")) return "sword_1handed";
            if (name.Contains("axe")) return "axe_1handed";
            if (name.Contains("bow")) return "bow";

            return "dagger";
        }

        /// <summary>
        /// Модель предмета из набора Synty. Пусто — предмет будет невидим
        /// в руке, поэтому о промахе говорим вслух.
        ///
        /// Имена приходят из старого набора KayKit («dagger», «sword_1handed»),
        /// поэтому здесь стоит перевод на имена Synty. Переименовывать вызовы
        /// по всему файлу дороже и рискованнее: имя предмета участвует ещё и
        /// в подборе иконки.
        /// </summary>
        private static GameObject LoadModel(string fileName)
        {
            string path = SyntyWeapon(fileName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
                Debug.LogWarning("[IsoRPG] Не найдена модель " + path + " — предмет останется невидимым.");

            return model;
        }

        /// <summary>
        /// Путь к модели Synty под старое имя из набора KayKit.
        ///
        /// Пути полные, а не имя плюс общая папка: оружие Synty разложено по
        /// двум наборам — клинки и топоры в Fantasy Kingdom, луки там же, но
        /// с приставкой Prop, а кинжал персонажей в Fantasy Characters.
        /// Собирать путь из кусков тут значит каждый раз промахиваться.
        /// </summary>
        private static string SyntyWeapon(string fileName)
        {
            const string Kingdom = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/";

            switch (fileName)
            {
                case "sword_1handed":  return Kingdom + "SM_Wep_Sword_01.prefab";
                case "sword_2handed":  return Kingdom + "SM_Wep_Sword_02.prefab";
                case "axe_1handed":    return Kingdom + "SM_Wep_Axe_01.prefab";
                case "bow":            return Kingdom + "SM_Prop_Bow_01.prefab";
                default:               return Kingdom + "SM_Wep_Dagger_01.prefab";
            }
        }
    }

}
