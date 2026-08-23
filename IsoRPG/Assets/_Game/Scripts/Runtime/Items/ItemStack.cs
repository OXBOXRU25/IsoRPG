using System;
using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Предмет и его количество. То, что лежит в одной ячейке сумки.
    ///
    /// Структура, а не класс: ячейка инвентаря это значение, а не сущность.
    /// Две ячейки с одинаковым содержимым равны, и копирование их не связывает.
    /// </summary>
    [Serializable]
    public struct ItemStack
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int count;

        public ItemDefinition Item => item;
        public int Count => count;

        public bool IsEmpty => item == null || count <= 0;
        public int MaxStack => item != null && item.stackable ? Mathf.Max(1, item.maxStack) : 1;
        public int FreeSpace => IsEmpty ? 0 : MaxStack - count;

        public static readonly ItemStack Empty = new ItemStack(null, 0);

        public ItemStack(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = Mathf.Max(0, count);
        }

        /// <summary>Можно ли досыпать сюда указанный предмет.</summary>
        public bool Accepts(ItemDefinition other) =>
            !IsEmpty && other == item && item.stackable && FreeSpace > 0;

        /// <summary>
        /// Добавить сколько влезет. Возвращает остаток, который не поместился —
        /// вызывающий разложит его по другим ячейкам.
        /// </summary>
        public ItemStack Add(int amount, out int leftover)
        {
            if (IsEmpty || amount <= 0)
            {
                leftover = amount;
                return this;
            }

            int fits = Mathf.Min(FreeSpace, amount);
            leftover = amount - fits;

            return new ItemStack(item, count + fits);
        }

        public ItemStack Take(int amount, out int taken)
        {
            taken = Mathf.Min(count, Mathf.Max(0, amount));
            int left = count - taken;

            return left > 0 ? new ItemStack(item, left) : Empty;
        }

        public override string ToString() =>
            IsEmpty ? "пусто" : (count > 1 ? item.displayName + " x" + count : item.displayName);
    }
}
