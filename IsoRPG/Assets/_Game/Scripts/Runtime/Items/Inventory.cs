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
        /// <summary>
        /// Сорок ячеек. Двадцати переставало хватать примерно на втором
        /// уровне: половину занимали яблоки и шкуры, и до торговца игрок
        /// доходил с полной сумкой, выбрасывая находки прямо у трупа.
        /// </summary>
        [SerializeField] private int capacity = 40;

        [SerializeField] private int gold = 0;

        private ItemStack[] slots;

        public int Capacity => capacity;

        /// <summary>
        /// Задаёт размер сумки. Нужно сборщику сцены.
        ///
        /// Менять значение поля по умолчанию мало: у объекта, уже лежащего
        /// в сцене, сохранена своя копия, и новое умолчание её не трогает.
        /// Это та ловушка, из-за которой правка «просто не применяется»,
        /// хотя в коде всё верно.
        /// </summary>
        public void SetCapacity(int value)
        {
            capacity = Mathf.Max(1, value);

            // Пересобираем ячейки, если размер поменялся уже после старта.
            if (slots == null || slots.Length != capacity)
            {
                var old = slots;
                slots = new ItemStack[capacity];

                for (int i = 0; i < capacity; i++)
                {
                    slots[i] = old != null && i < old.Length ? old[i] : ItemStack.Empty;
                }
            }
        }
        public int Gold => gold;

        /// <summary>Содержимое изменилось — интерфейсу пора перерисоваться.</summary>
        public event Action Changed;

        /// <summary>Не поместилось: предмет и сколько штук пропало.</summary>
        public event Action<ItemDefinition, int> Overflow;

        /// <summary>
        /// Ячейки сумки. Создаются при первом обращении, а не в Awake.
        ///
        /// Порядок пробуждения объектов Unity не гарантирует: окно сумки —
        /// отдельный объект, и его OnEnable законно случается раньше, чем
        /// Awake самой сумки. Половинчатая защита (проверка на null в одних
        /// методах и не в других) это не лечит — она только переносит падение
        /// в следующий метод.
        /// </summary>
        private ItemStack[] Slots
        {
            get
            {
                if (slots == null || slots.Length != capacity)
                {
                    slots = new ItemStack[capacity];
                    for (int i = 0; i < capacity; i++) slots[i] = ItemStack.Empty;
                }

                return slots;
            }
        }

        public ItemStack GetSlot(int index) =>
            index >= 0 && index < Slots.Length ? Slots[index] : ItemStack.Empty;

        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < Slots.Length; i++)
                    if (!Slots[i].IsEmpty) used++;
                return used;
            }
        }

        /// <summary>
        /// Отдать содержимое для сохранения — включая пустые ячейки.
        ///
        /// Порядок важен: игрок раскладывает вещи так, как ему удобно, и
        /// вернуть их «как влезло» значит перемешать сумку при каждом входе.
        /// </summary>
        public IsoRPG.Save.SavedStack[] CaptureState()
        {
            var result = new IsoRPG.Save.SavedStack[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                var stack = slots[i];

                result[i] = new IsoRPG.Save.SavedStack
                {
                    item = stack.IsEmpty || stack.Item == null ? "" : stack.Item.name,
                    count = stack.IsEmpty ? 0 : stack.Count,
                };
            }

            return result;
        }

        public void RestoreState(System.Collections.Generic.List<IsoRPG.Save.SavedStack> saved, int savedGold)
        {
            var database = IsoRPG.Save.GameDatabase.Instance;

            for (int i = 0; i < slots.Length; i++) slots[i] = ItemStack.Empty;

            if (saved != null && database != null)
            {
                for (int i = 0; i < saved.Count && i < slots.Length; i++)
                {
                    if (string.IsNullOrEmpty(saved[i].item)) continue;

                    var item = database.Item(saved[i].item);
                    if (item == null) continue;

                    slots[i] = new ItemStack(item, Mathf.Max(1, saved[i].count));
                }
            }

            gold = Mathf.Max(0, savedGold);
            Changed?.Invoke();
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
                for (int i = 0; i < Slots.Length && left > 0; i++)
                {
                    if (!Slots[i].Accepts(item)) continue;
                    Slots[i] = Slots[i].Add(left, out left);
                }
            }

            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;

                int put = item.stackable ? Mathf.Min(left, Mathf.Max(1, item.maxStack)) : 1;
                Slots[i] = new ItemStack(item, put);
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
            if (index < 0 || index >= Slots.Length) return ItemStack.Empty;
            if (Slots[index].IsEmpty) return ItemStack.Empty;

            var item = Slots[index].Item;
            Slots[index] = Slots[index].Take(amount, out int taken);

            Changed?.Invoke();
            return new ItemStack(item, taken);
        }

        /// <summary>Положить предмет обратно в конкретную ячейку. Нужно при снятии экипировки.</summary>
        public bool PutInto(int index, ItemStack stack)
        {
            if (index < 0 || index >= Slots.Length || stack.IsEmpty) return false;
            if (!Slots[index].IsEmpty) return false;

            Slots[index] = stack;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Сколько таких предметов в сумке всего.
        ///
        /// Считает по всем ячейкам, а не по первой найденной: один и тот же
        /// предмет может лежать несколькими стопками, и счёт по первой дал бы
        /// заниженный результат ровно тогда, когда его больше всего.
        /// </summary>
        public int CountOf(ItemDefinition item)
        {
            if (item == null) return 0;

            int total = 0;

            for (int i = 0; i < Slots.Length; i++)
                if (!Slots[i].IsEmpty && Slots[i].Item == item) total += Slots[i].Count;

            return total;
        }

        /// <summary>
        /// Убрать заданное число предметов. Возвращает, сколько убрали
        /// на самом деле — может быть меньше, если столько не нашлось.
        /// </summary>
        public int Remove(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return 0;

            int left = count;

            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (Slots[i].IsEmpty || Slots[i].Item != item) continue;

                int take = Mathf.Min(left, Slots[i].Count);
                Slots[i] = Slots[i].Take(take, out int taken);
                left -= taken;
            }

            if (left < count) Changed?.Invoke();

            return count - left;
        }

        public bool HasFreeSlot()
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].IsEmpty) return true;
            return false;
        }

        /// <summary>Всё содержимое — для интерфейса и сохранений.</summary>
        public IEnumerable<ItemStack> All()
        {
            for (int i = 0; i < Slots.Length; i++) yield return Slots[i];
        }
    }
}
