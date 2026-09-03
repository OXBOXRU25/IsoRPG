using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Создаёт ассеты способностей разбойника.
    ///
    /// Числа здесь — черновик из PROJECT.md, и после первых боёв их будут
    /// крутить. Крутить можно прямо в инспекторе на самом ассете: код это
    /// не затрагивает вообще, в том и был смысл.
    /// </summary>
    public static class RogueAbilitiesBuilder
    {
        private const string Folder = "Assets/_Game/Data/Abilities";

        [MenuItem("Tools/IsoRPG/Создать способности разбойника", priority = 30)]
        public static void Build()
        {
            EnsureFolder(Folder);

            // Числа заданы Павлоном 23.08.2026. Автоатака бьёт чистым уроном
            // оружия и комбо-очков НЕ даёт: очки приносит только приём.

            Create("A_SinisterStrike", ability =>
            {
                ability.displayName = "Коварный удар";
                ability.description = "Удар кинжалом. Урон оружия плюс пять. Даёт комбо-очко.";
                ability.hotkeyLabel = "1";
                ability.iconColor = new Color32(0xB8, 0x9A, 0x5A, 0xFF);
                ability.energyCost = 45;
                ability.cooldown = 0f;
                ability.comboRole = ComboRole.Generator;
                ability.comboGain = 1;
                ability.dealsDamage = true;
                ability.bonusDamage = 5;      // кинжал 10 + 5 = 15
                ability.reach = 0.9f;
                ability.impactDelay = 0.4f;
            });

            // --- Приёмы из скрытности --------------------------------
            //
            // Смысл скрытности в том, что она даёт не преимущество в бою, а
            // ПЕРВЫЙ ХОД. Оба приёма доступны только до драки, оба сразу
            // наполняют серию — то есть открывают бой с позиции, до которой
            // в честной схватке пришлось бы добираться тремя ударами.

            Create("A_Ambush", ability =>
            {
                ability.displayName = "Внезапный удар";
                ability.description = "Удар в спину из скрытности. 250% урона оружия и ещё сотня сверху. Даёт комбо-очко.";
                ability.hotkeyLabel = "1";
                ability.iconColor = new Color32(0x8A, 0x3A, 0x5A, 0xFF);
                ability.energyCost = 60;
                ability.cooldown = 0f;
                ability.comboRole = ComboRole.Generator;
                ability.comboGain = 1;
                ability.dealsDamage = true;
                ability.weaponMultiplier = 2.5f;
                ability.bonusDamage = 100;
                ability.reach = 0.9f;
                ability.impactDelay = 0.35f;

                // Требование зайти со спины — то, что делает приём приёмом, а
                // не просто сильным ударом. В открытом бою выполнить почти
                // невозможно, из скрытности — естественно.
                ability.requiresStealth = true;
                ability.requiresBehindTarget = true;
                ability.behindAngle = 120f;
                ability.breaksStealth = true;

                // Отдельная анимация добивания: обычный замах на таком уроне
                // выглядит несоразмерно тому, что произошло.
                ability.animationTrigger = "StealthKill";
            });

            Create("A_CheapShot", ability =>
            {
                ability.displayName = "Подлый трюк";
                ability.description = "Оглушает цель на четыре секунды. Урона не наносит, но сразу даёт два комбо-очка.";
                ability.hotkeyLabel = "2";
                ability.iconColor = new Color32(0x6A, 0x5A, 0x8A, 0xFF);
                ability.energyCost = 40;
                ability.cooldown = 0f;
                ability.comboRole = ComboRole.Generator;
                ability.comboGain = 2;

                // Урона нет вовсе: приём платит не им, а временем. Четыре
                // секунды неподвижной цели — это два-три свободных удара.
                ability.dealsDamage = false;
                ability.stunBase = 4f;
                ability.stunPerCombo = 0f;
                ability.reach = 0.9f;
                ability.impactDelay = 0.3f;

                ability.requiresStealth = true;
                ability.requiresBehindTarget = false;
                ability.breaksStealth = true;
                ability.animationTrigger = "Attack";
            });

            Create("A_KidneyShot", ability =>
            {
                // Финишер, который платит не уроном, а контролем. Отсюда выбор
                // в бою: добить сейчас или обездвижить и перевести дух.
                ability.displayName = "Удар по почкам";
                ability.description = "Оглушает цель. Одно комбо-очко — две секунды, пять очков — шесть.";
                ability.hotkeyLabel = "2";
                ability.iconColor = new Color32(0x4A, 0x7A, 0xB8, 0xFF);
                ability.energyCost = 20;
                ability.cooldown = 20f;

                ability.comboRole = ComboRole.Finisher;
                ability.dealsDamage = false;  // урона не наносит вообще

                // Длительность = очки + 1: одно очко даёт две секунды,
                // пять — шесть.
                ability.stunBase = 1f;
                ability.stunPerCombo = 1f;

                ability.reach = 0.9f;
                ability.impactDelay = 0.35f;
            });

            Create("A_Eviscerate", ability =>
            {
                ability.displayName = "Потрошение";
                ability.description = "Добивающий удар. Тратит все комбо-очки, урон резко растёт от их числа.";
                ability.hotkeyLabel = "3";
                ability.iconColor = new Color32(0xC4, 0x4A, 0x3A, 0xFF);
                ability.energyCost = 35;

                // Отката нет намеренно: способность и так ограничена
                // комбо-очками, а их надо копить приёмами по 45 энергии.
                ability.cooldown = 0f;

                ability.comboRole = ComboRole.Finisher;
                ability.dealsDamage = true;

                // Урон разбросом по числу очков. Разброс важнее ровного
                // числа: одинаковый урон каждый раз читается как таблица,
                // а не как удар.
                ability.finisherDamage = new[]
                {
                    new DamageRange { min =  40, max =  50 },   // 1 очко
                    new DamageRange { min =  60, max =  70 },   // 2
                    new DamageRange { min =  70, max =  85 },   // 3
                    new DamageRange { min =  85, max = 100 },   // 4
                    new DamageRange { min = 100, max = 120 },   // 5
                };

                ability.reach = 0.9f;
                ability.animationTrigger = "StealthKill";  // добивание весомее обычного удара
                ability.impactDelay = 0.45f;
            });

            Create("A_Stealth", ability =>
            {
                ability.displayName = "Скрытность";
                ability.description = "Уйти в тень. Монстры не замечают, скорость ниже. В бою недоступна.";
                ability.hotkeyLabel = "0";
                ability.iconColor = new Color32(0x3A, 0x46, 0x5A, 0xFF);

                ability.energyCost = 0;
                ability.cooldown = 10f;

                ability.comboRole = ComboRole.None;
                ability.dealsDamage = false;

                // Ключевые флаги: цели не нужно, из тени не выводит,
                // а наоборот — включает её.
                ability.requiresTarget = false;
                ability.breaksStealth = false;
                ability.togglesStealth = true;

                ability.impactDelay = 0f;
            });

            // Спринт. Числа заданы Павлоном 01.09.2026.
            Create("A_Sprint", ability =>
            {
                ability.displayName = "Спринт";
                ability.description =
                    "Мгновенное действие. Скорость передвижения выше на 70% в течение 15 секунд. " +
                    "Не нарушает незаметности. Требуется 5-й уровень.";

                ability.hotkeyLabel = "4";
                ability.iconColor = new Color32(0x6A, 0x9A, 0xC8, 0xFF);

                ability.energyCost = 0;
                ability.cooldown = 120f;      // две минуты
                ability.requiredLevel = 5;

                ability.comboRole = ComboRole.None;
                ability.dealsDamage = false;

                // Ни цели, ни удара: приём про себя, а не про противника.
                ability.requiresTarget = false;

                // Из тени НЕ выводит — так и записано в описании приёма.
                ability.breaksStealth = false;

                ability.moveSpeedBonus = 0.7f;
                ability.buffDuration = 15f;

                // Жест усиления, а не замах: игрок должен видеть, что нажал
                // не атаку.
                ability.animationTrigger = "CastBuff";
                ability.impactDelay = 0f;
            });

            // Метание кинжала. Заведено 04.09.2026 по просьбе Павла —
            // «у нас нет такого скила, добавь сразу пока без иконки».
            //
            // Единственный дальний приём разбойника: бьёт с пятнадцати
            // метров, то есть работает там, где остальные его умения
            // бесполезны — по убегающему, по лучнику, по цели за пропастью.
            // Оттого и откат заметный: иначе им можно было бы вести весь бой,
            // не подходя, и ближний бой стал бы не нужен.
            Create("A_ThrowDagger", ability =>
            {
                ability.displayName = "Метание кинжала";
                ability.description =
                    "Бросок клинка в цель на расстоянии до пятнадцати метров. " +
                    "Урон оружия плюс восемь. Даёт комбо-очко.";

                ability.hotkeyLabel = "5";
                ability.iconColor = new Color32(0x9A, 0xA8, 0xB8, 0xFF);

                ability.energyCost = 30;
                ability.cooldown = 8f;

                ability.comboRole = ComboRole.Generator;
                ability.comboGain = 1;

                ability.dealsDamage = true;
                ability.bonusDamage = 8;

                // Дальность — то, ради чего приём и заводится.
                ability.reach = 15f;
                ability.requiresTarget = true;

                // Задержка больше, чем у удара: клинок должен успеть
                // покинуть руку, иначе цель вздрагивает раньше броска.
                ability.animationTrigger = "Throw";
                ability.impactDelay = 0.55f;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IsoRPG] Способности разбойника созданы в " + Folder);
        }

        /// <summary>Порядок способностей на панели. Стелс идёт последним — он на клавише 0.</summary>
        private static readonly string[] Names =
        {
            "A_SinisterStrike",   // 1
            "A_KidneyShot",       // 2
            "A_Eviscerate",       // 3
            "A_Sprint",           // 4
            "A_Stealth",          // 0
        };

        /// <summary>
        /// Панель скрытности. Подменяет обычную целиком, поэтому нумерация
        /// здесь тоже начинается с единицы: рука не должна переучиваться,
        /// меняется только то, что под пальцем.
        /// </summary>
        private static readonly string[] StealthNames =
        {
            "A_Ambush",           // 1  в спину
            "A_CheapShot",        // 2  оглушение
        };

        /// <summary>
        /// Загрузить готовые способности по порядку. Недостающие создаёт.
        ///
        /// Раньше здесь была ловушка: если хоть один ассет находился, список
        /// считался готовым, и переименованная способность молча пропадала
        /// с панели. Проверять надо каждую, а не факт непустого списка.
        /// </summary>
        /// <summary>Приёмы, доступные только из тени.</summary>
        public static List<AbilityDefinition> LoadStealth()
        {
            var result = new List<AbilityDefinition>();

            foreach (string name in StealthNames)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Folder + "/" + name + ".asset");

                // Молчать нельзя: без приёма панель скрытности соберётся
                // короче, и снаружи это выглядит как «способность не
                // работает», а не как «ассета нет».
                if (asset == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет способности " + name +
                                     " — пересоздай их через Tools/IsoRPG.");
                    continue;
                }

                result.Add(asset);
            }

            return result;
        }

        public static List<AbilityDefinition> Load()
        {
            var result = new List<AbilityDefinition>();
            bool missing = false;

            foreach (string name in Names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Folder + "/" + name + ".asset");
                if (asset == null) { missing = true; break; }
                result.Add(asset);
            }

            if (!missing) return result;

            Build();

            result.Clear();
            foreach (string name in Names)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Folder + "/" + name + ".asset");
                if (asset != null) result.Add(asset);
            }

            return result;
        }

        [MenuItem("Tools/IsoRPG/Пересоздать способности (сбросит правки)", priority = 31)]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Пересоздать способности",
                    "Все ассеты способностей будут удалены и созданы заново.\n\n" +
                    "Если ты правил числа в инспекторе — они пропадут.",
                    "Пересоздать", "Отмена"))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.Refresh();
            }

            Build();
        }

        private static void Create(string fileName, System.Action<AbilityDefinition> configure)
        {
            string path = Folder + "/" + fileName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (existing != null)
            {
                // Не перезаписываем: если Павлон крутил числа в инспекторе,
                // повторный вызов пункта меню не должен стирать его правки.
                Debug.Log("[IsoRPG] " + fileName + " уже есть — оставляю как есть.");
                return;
            }

            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            configure(ability);
            AssetDatabase.CreateAsset(ability, path);
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
