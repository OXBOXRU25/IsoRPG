using System;
using System.Collections.Generic;
using IsoRPG.Localization;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Надетые вещи и их влияние на характеристики.
    ///
    /// Ключевая идея: экипировка не знает, что делают её числа. Она их
    /// складывает и сообщает «состав изменился», а боевые системы сами
    /// пересчитывают то, что им нужно. Иначе добавление нового вида бонуса
    /// заставляло бы править и экипировку, и бой одновременно.
    /// </summary>
    public sealed class Equipment : MonoBehaviour
    {
        private readonly Dictionary<EquipSlot, ItemStack> worn = new Dictionary<EquipSlot, ItemStack>();

        private Inventory inventory;
        private WeaponStats weapon;
        private DefenseStats defense;
        private Experience experience;

        [Tooltip("Урон и скорость голых рук — когда оружие не надето.")]
        [SerializeField] private int unarmedDamage = 4;
        [SerializeField] private float unarmedInterval = 2f;

        [Tooltip("Собственная броня персонажа, без вещей.")]
        [SerializeField] private int baseArmor = 0;

        /// <summary>Состав изменился: надели, сняли или заменили.</summary>
        public event Action Changed;

        /// <summary>Не удалось надеть: предмет и причина.</summary>
        public event Action<ItemDefinition, string> Rejected;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            weapon = GetComponent<WeaponStats>();
            defense = GetComponent<DefenseStats>();
            experience = GetComponent<Experience>();
        }

        private void Start() => Recalculate();

        public ItemStack GetSlot(EquipSlot slot) =>
            worn.TryGetValue(slot, out var stack) ? stack : ItemStack.Empty;

        public bool IsEmpty(EquipSlot slot) => GetSlot(slot).IsEmpty;

        /// <summary>
        /// Надеть предмет из ячейки сумки. Снятое возвращается в ту же ячейку —
        /// так замена оружия не требует свободного места.
        /// </summary>
        public bool EquipFromInventory(int inventorySlot)
        {
            if (inventory == null) return false;

            var stack = inventory.GetSlot(inventorySlot);
            if (stack.IsEmpty) return false;

            var item = stack.Item;

            if (!item.IsEquippable)
            {
                Rejected?.Invoke(item, "это не надевается");
                return false;
            }

            int level = experience != null ? experience.Level
                      : defense != null ? defense.Level : 1;

            if (item.requiredLevel > level)
            {
                Rejected?.Invoke(item, Loc.F("нужен {0} уровень", item.requiredLevel));
                return false;
            }

            // Забираем ровно одну штуку: надеть можно только один предмет,
            // даже если в ячейке лежит стопка.
            var taken = inventory.TakeFrom(inventorySlot, 1);
            if (taken.IsEmpty) return false;

            var target = ChooseHand(item);
            var previous = GetSlot(target);
            worn[target] = taken;

            // Снятое кладём в освободившуюся ячейку. Она точно свободна,
            // если оттуда взяли последний предмет; иначе ищем любую.
            if (!previous.IsEmpty && !inventory.PutInto(inventorySlot, previous))
                inventory.Add(previous);

            Recalculate();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// В какую руку кладём предмет.
        ///
        /// Парное оружие идёт в свободную руку, а не выбивает уже надетое:
        /// второй кинжал у разбойника — это второй кинжал, а не замена
        /// первому. Всё остальное ложится в свой слот без вариантов.
        /// </summary>
        /// <summary>
        /// Куда на самом деле пойдёт вещь.
        ///
        /// Парных слотов у нас два вида — руки и кольца, — и правило одно:
        /// занят первый, свободен второй — идём во второй. Иначе второе
        /// кольцо пришлось бы надевать перетаскиванием, а его у нас нет.
        /// </summary>
        private EquipSlot ChooseHand(ItemDefinition item)
        {
            if (item.slot == EquipSlot.Ring)
                return PickFree(EquipSlot.Ring, EquipSlot.Ring2);

            if (item.slot != EquipSlot.MainHand || !item.dualWieldable)
                return item.slot;

            return PickFree(EquipSlot.MainHand, EquipSlot.OffHand);
        }

        private EquipSlot PickFree(EquipSlot first, EquipSlot second)
        {
            if (!GetSlot(first).IsEmpty && GetSlot(second).IsEmpty) return second;

            return first;
        }

        /// <summary>Снять предмет в сумку.</summary>
        public bool Unequip(EquipSlot slot)
        {
            var stack = GetSlot(slot);
            if (stack.IsEmpty) return false;

            if (inventory != null && !inventory.HasFreeSlot())
            {
                Rejected?.Invoke(stack.Item, "нет места в сумке");
                return false;
            }

            worn[slot] = ItemStack.Empty;
            if (inventory != null) inventory.Add(stack);

            Recalculate();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Суммарные прибавки к характеристикам от всех надетых вещей.</summary>
        public StatBlock TotalStatBonus()
        {
            var total = new StatBlock(0, 0, 0);

            foreach (var pair in worn)
            {
                if (pair.Value.IsEmpty) continue;
                total += pair.Value.Item.StatBonus;
            }

            return total;
        }

        public int TotalArmor()
        {
            int total = baseArmor;

            // Броня от талантов считается здесь же, а не поверх: это
            // единственное место, где броня собирается, и второй источник
            // рано или поздно затёр бы первый.
            var talents = GetComponent<IsoRPG.Progression.TalentBook>();
            if (talents != null)
                total += Mathf.RoundToInt(talents.Bonus(IsoRPG.Progression.TalentEffect.Armor));

            foreach (var pair in worn)
            {
                if (pair.Value.IsEmpty) continue;
                total += pair.Value.Item.armor;
            }

            return total;
        }

        /// <summary>
        /// Пересчитать всё, на что влияют вещи.
        ///
        /// Вызывается в одном месте после любого изменения — иначе часть
        /// систем осталась бы со старыми числами, и найти это было бы
        /// тяжело: игра работает, просто урон «какой-то не такой».
        /// </summary>
        public void Recalculate()
        {
            var mainHand = GetSlot(EquipSlot.MainHand);

            if (weapon != null)
            {
                if (!mainHand.IsEmpty && mainHand.Item.IsWeapon)
                {
                    weapon.Equip(mainHand.Item.displayName,
                                 mainHand.Item.weaponDamage,
                                 mainHand.Item.attackInterval);
                }
                else
                {
                    weapon.Equip("Кулаки", unarmedDamage, unarmedInterval);
                }
            }

            if (defense != null)
            {
                int level = experience != null ? experience.Level : defense.Level;
                defense.Setup(level, TotalArmor());
            }
        }

        /// <summary>Все надетые вещи — для интерфейса.</summary>
        /// <summary>Что надето — для сохранения.</summary>
        public List<IsoRPG.Save.SavedEquip> CaptureState()
        {
            var result = new List<IsoRPG.Save.SavedEquip>();

            foreach (var pair in worn)
            {
                if (pair.Value.IsEmpty || pair.Value.Item == null) continue;

                result.Add(new IsoRPG.Save.SavedEquip
                {
                    slot = (int)pair.Key,
                    item = pair.Value.Item.name,
                });
            }

            return result;
        }

        /// <summary>
        /// Надеть сохранённое.
        ///
        /// Кладём напрямую в слоты, минуя проверку уровня: вещь уже была
        /// надета, и отказать в ней при загрузке значит раздеть героя за то,
        /// что он вышел из игры.
        /// </summary>
        public void RestoreState(List<IsoRPG.Save.SavedEquip> saved)
        {
            worn.Clear();

            var database = IsoRPG.Save.GameDatabase.Instance;

            if (saved != null && database != null)
            {
                foreach (var entry in saved)
                {
                    var item = database.Item(entry.item);
                    if (item == null) continue;

                    worn[(EquipSlot)entry.slot] = new ItemStack(item, 1);
                }
            }

            Recalculate();
            Changed?.Invoke();
        }

        public IEnumerable<KeyValuePair<EquipSlot, ItemStack>> All()
        {
            foreach (var pair in worn)
                if (!pair.Value.IsEmpty) yield return pair;
        }
    }
}
