using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>Одна строка таблицы добычи.</summary>
    [Serializable]
    public struct LootEntry
    {
        public ItemDefinition item;

        [Tooltip("Шанс выпадения, от 0 до 1.")]
        [Range(0f, 1f)] public float chance;

        [Tooltip("Сколько штук, если выпало.")]
        public int minCount;
        public int maxCount;

        public int RollCount() => UnityEngine.Random.Range(Mathf.Max(1, minCount), Mathf.Max(1, maxCount) + 1);
    }

    /// <summary>
    /// Что падает с монстра. Каждая строка проверяется независимо, поэтому
    /// с одного трупа может упасть и всё сразу, и ничего.
    ///
    /// Независимые броски, а не «одна вещь из списка», выбраны намеренно:
    /// так редкая вещь остаётся редкой, сколько бы обычного хлама ни
    /// добавили в таблицу. При выборе одного из списка добавление хлама
    /// молча снижало бы шанс редкого — и это почти всегда обнаруживают
    /// поздно.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "IsoRPG/Таблица добычи")]
    public sealed class LootTable : ScriptableObject
    {
        [Header("Золото")]
        public int minGold = 0;
        public int maxGold = 0;

        [Tooltip("Шанс, что золото вообще выпадет.")]
        [Range(0f, 1f)] public float goldChance = 1f;

        [Header("Предметы")]
        public LootEntry[] entries = new LootEntry[0];

        public int RollGold()
        {
            if (maxGold <= 0) return 0;
            if (UnityEngine.Random.value > goldChance) return 0;

            return UnityEngine.Random.Range(minGold, maxGold + 1);
        }

        public List<ItemStack> RollItems()
        {
            var result = new List<ItemStack>();
            if (entries == null) return result;

            foreach (var entry in entries)
            {
                if (entry.item == null) continue;
                if (UnityEngine.Random.value > entry.chance) continue;

                result.Add(new ItemStack(entry.item, entry.RollCount()));
            }

            return result;
        }
    }
}
