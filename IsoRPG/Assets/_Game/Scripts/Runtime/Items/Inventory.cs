using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Сумка игрока: фиксированное число ячеек плюс золото.
    ///
    /// Золото хранится отдельным числом, а не предметом в ячейке — иначе
    /// оно занимало бы место, и игрок терял бы монеты при полной сумке.
    /// Так устроено во всех играх жанра, и не случайно.
    /// </summary>
    public sealed class Inventory : MonoBehaviour
    {
        [Tooltip("Сколько ячеек в сумке.")]
        [SerializeField] private int capacity = 20;

        [SerializeField] private int gold = 0;

        private ItemStack[] slots;

        public int Capacity => capacity;
        public int Gold => gold;

        /// <summary>Содержимое изменилось — интерфейсу пора перерисоваться.</summary>
        public event Action Changed;

        /// <summary>Не поместилось: предмет и сколько штук пропало.</summary>
        public event Action<ItemDefinition, int> Overflow;

        private void Awake()
        {
            slots = new ItemStack[capacity];
            for (int i = 0; i < capacity; i++) slots[i] = ItemStack.Empty;
        }

        public ItemStack GetSlot(int index) =>
            slots != null && index >= 0 && index < slots.Length ? slots[index] : ItemStack.Empty;

        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (!slots[i].IsEmpty) used++;
                return used;
            }
        }

        public void AddGold(int amount)
        {
            if (amount == 0) return;

            gold = Mathf.Max(0, gold + amount);
            Changed?.Invoke();
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (gold < amount) return false;

            gold -= amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Положить предмет. Возвращает, сколько штук не поместилось.
        ///
        /// Сначала досыпаем в существующие стопки, потом занимаем пустые —
        /// иначе сумка забивается наполовину пустыми стопками одного и того же.
        /// </summary>
        public int Add(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;

            int left = count;

            if (item.stackable)
            {
                for (int i = 0; i < slots.Length && left > 0; i++)
                {
                    if (!slots[i].Accepts(item)) continue;
                    slots[i] = slots[i].Add(left, out left);
                }
            }

            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;

                int put = item.stackable ? Mathf.Min(left, Mathf.Max(1, item.maxStack)) : 1;
                slots[i] = new ItemStack(item, put);
                left -= put;
            }

            Changed?.Invoke();

            if (left > 0) Overflow?.Invoke(item, left);
            return left;
        }

        public int Add(ItemStack stack) => stack.IsEmpty ? 0 : Add(stack.Item, stack.Count);

        /// <summary>Забрать из ячейки. Возвращает, что забрали.</summary>
        public ItemStack TakeFrom(int index, int amount = int.MaxValue)
        {
            if (slots == null || index < 0 || index >= slots.Length) return ItemStack.Empty;
            if (slots[index].IsEmpty) return ItemStack.Empty;

            var item = slots[index].Item;
            slots[index] = slots[index].Take(amount, out int taken);

            Changed?.Invoke();
            return new ItemStack(item, taken);
        }

        /// <summary>Положить предмет обратно в конкретную ячейку. Нужно при снятии экипировки.</summary>
        public bool PutInto(int index, ItemStack stack)
        {
            if (slots == null || index < 0 || index >= slots.Length || stack.IsEmpty) return false;
            if (!slots[index].IsEmpty) return false;

            slots[index] = stack;
            Changed?.Invoke();
            return true;
        }

        public bool HasFreeSlot()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsEmpty) return true;
            return false;
        }

        /// <summary>Всё содержимое — для интерфейса и сохранений.</summary>
        public IEnumerable<ItemStack> All()
        {
            for (int i = 0; i < slots.Length; i++) yield return slots[i];
        }
    }
}
