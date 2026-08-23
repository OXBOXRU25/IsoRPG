using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Торговец: покупает хлам, продаёт припасы.
    ///
    /// Появился не ради разнообразия, а потому что золото было числом без
    /// применения, а хлам копился и занимал сумку. Обе беды лечит одно и то
    /// же — место, где одно меняют на другое.
    ///
    /// Цены считаются от стоимости предмета, а не пишутся руками для каждого:
    /// иначе новая вещь приходит без цены, и это замечают, только когда её
    /// пытаются продать за ноль.
    ///
    /// Решения (можно ли купить, хватает ли места) отделены от показа. Сейчас
    /// это выглядит избыточным, но в сетевой игре проверять будет хост, а
    /// окно — только показывать его ответ.
    /// </summary>
    public sealed class Merchant : MonoBehaviour
    {
        /// <summary>
        /// Наценка: покупаем дешевле, продаём дороже.
        ///
        /// Разница — не жадность торговца, а то, что делает золото ценным.
        /// Без неё вещи можно продавать и выкупать без потерь, и деньги
        /// перестают быть ресурсом.
        /// </summary>
        private const float SellMarkup = 2.5f;

        [Tooltip("Как зовут. Показывается в окне торговли.")]
        [SerializeField] private string displayName = "Торговец";

        [Tooltip("Что продаёт. Запас бесконечный: лавка, а не сундук.")]
        [SerializeField] private List<ItemDefinition> stock = new List<ItemDefinition>();

        [Tooltip("Насколько близко надо подойти.")]
        [SerializeField] private float talkRange = 3.2f;

        public string DisplayName => displayName;
        public IReadOnlyList<ItemDefinition> Stock => stock;
        public float TalkRange => talkRange;

        public void Setup(string name, List<ItemDefinition> goods)
        {
            displayName = name;
            stock = goods;
        }

        /// <summary>Сколько торговец просит за свой товар.</summary>
        public static int PriceToBuy(ItemDefinition item) =>
            item == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(item.vendorPrice * SellMarkup));

        /// <summary>Сколько даёт за принесённое.</summary>
        public static int PriceToSell(ItemDefinition item) =>
            item == null ? 0 : Mathf.Max(1, item.vendorPrice);

        // ------------------------------------------------------------------

        /// <summary>
        /// Продать игроку. False — сделка не состоялась, и причина уже
        /// названа в логе.
        /// </summary>
        public bool Sell(ItemDefinition item, Inventory buyer)
        {
            if (item == null || buyer == null) return false;

            int price = PriceToBuy(item);

            if (buyer.Gold < price)
            {
                CombatLog.Add("Не хватает золота: нужно " + price + ".", LogKind.System);
                return false;
            }

            // Место проверяем ДО того, как берём деньги. Иначе золото уйдёт,
            // а вещь останется у торговца — и виноватым будет выглядеть игрок.
            if (!buyer.HasFreeSlot() && !CanStack(buyer, item))
            {
                CombatLog.Add("В сумке нет места.", LogKind.System);
                return false;
            }

            if (!buyer.SpendGold(price)) return false;

            buyer.Add(item, 1);

            CombatLog.Looted(item.displayName + " куплен за " + price, item.RarityColor);
            IsoRPG.Audio.Sfx.Gold(transform.position);

            return true;
        }

        /// <summary>Купить у игрока одну штуку из ячейки.</summary>
        public bool Buy(int inventorySlot, Inventory seller)
        {
            if (seller == null) return false;

            var stack = seller.GetSlot(inventorySlot);
            if (stack.IsEmpty || stack.Item == null) return false;

            int price = PriceToSell(stack.Item);

            var taken = seller.TakeFrom(inventorySlot, 1);
            if (taken.IsEmpty) return false;

            seller.AddGold(price);

            CombatLog.Add("Продано: " + taken.Item.displayName + " за " + price + " золота",
                          LogKind.System);
            IsoRPG.Audio.Sfx.Gold(transform.position);

            return true;
        }

        /// <summary>Влезет ли в уже начатую стопку — место при этом не нужно.</summary>
        private static bool CanStack(Inventory inventory, ItemDefinition item)
        {
            if (!item.stackable) return false;

            return inventory.CountOf(item) % item.maxStack != 0;
        }
    }
}
