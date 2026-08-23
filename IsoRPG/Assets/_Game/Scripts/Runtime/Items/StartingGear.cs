using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Выдаёт стартовые вещи и надевает их при первом запуске.
    ///
    /// Нужен потому, что экипировка при старте видит пустые руки и честно
    /// ставит «Кулаки» — затирая любое оружие, прописанное вручную. Значит
    /// стартовое снаряжение должно приходить тем же путём, что и добыча:
    /// через сумку и надевание, а не в обход.
    /// </summary>
    public sealed class StartingGear : MonoBehaviour
    {
        [Tooltip("Что выдать. Всё, что надевается, будет надето сразу.")]
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

        [SerializeField] private int startingGold = 0;

        private Inventory inventory;
        private Equipment equipment;

        public void Setup(IEnumerable<ItemDefinition> gear, int gold)
        {
            items.Clear();
            items.AddRange(gear);
            startingGold = gold;
        }

        private void Start()
        {
            inventory = GetComponent<Inventory>();
            equipment = GetComponent<Equipment>();

            if (inventory == null) return;

            if (startingGold > 0) inventory.AddGold(startingGold);

            foreach (var item in items)
            {
                if (item == null) continue;

                inventory.Add(item);

                // Надеваем сразу, если есть куда. Ищем ячейку, куда предмет
                // только что лёг: инвентарь не сообщает индекс, поэтому
                // проходим по ячейкам и берём первую подходящую.
                if (!item.IsEquippable || equipment == null) continue;

                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (stack.IsEmpty || stack.Item != item) continue;

                    equipment.EquipFromInventory(i);
                    break;
                }
            }
        }
    }
}
