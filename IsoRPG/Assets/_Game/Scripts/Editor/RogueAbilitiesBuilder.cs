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
            "A_Stealth",          // 0
        };

        /// <summary>
        /// Загрузить готовые способности по порядку. Недостающие создаёт.
        ///
        /// Раньше здесь была ловушка: если хоть один ассет находился, список
        /// считался готовым, и переименованная способность молча пропадала
        /// с панели. Проверять надо каждую, а не факт непустого списка.
        /// </summary>
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
