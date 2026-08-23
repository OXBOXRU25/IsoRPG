using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>Куда предмет надевается. None — надеть нельзя, это хлам или расходник.</summary>
    public enum EquipSlot
    {
        None,
        MainHand,
        OffHand,
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        Ring,

        /// <summary>Второе кольцо. Кольца надеваются в любой свободный.</summary>
        Ring2,

        Necklace,
        Cloak,

        /// <summary>Метательное: дротики, ножи.</summary>
        Ranged,

        Trinket,
    }

    /// <summary>
    /// Редкость. Цвета каноничные для жанра — игрок читает их мгновенно,
    /// и менять их значение нельзя, как и с цветами уровней.
    /// </summary>
    public enum ItemRarity
    {
        Junk,       // серый — только продать
        Common,     // белый
        Uncommon,   // зелёный
        Rare,       // синий
        Epic,       // фиолетовый
        Legendary,  // оранжевый
    }

    /// <summary>
    /// Описание предмета. Один ассет — один предмет.
    ///
    /// Всё, что предмет умеет, лежит здесь числами: урон, броня, прибавки к
    /// характеристикам. Экипировка потом просто складывает эти числа, и ей
    /// не нужно знать, кинжал это или сапоги.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "IsoRPG/Предмет")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Описание")]
        public string displayName = "Предмет";

        [TextArea(2, 4)]
        public string description = "";

        public ItemRarity rarity = ItemRarity.Common;

        [Tooltip("Картинка предмета. Пусто — рисуется цветной квадрат.")]
        public Sprite icon;

        [Tooltip("Цвет значка, пока нет настоящих иконок.")]
        public Color iconColor = new Color32(0x8A, 0x8A, 0x8A, 0xFF);

        [Header("Ношение")]
        public EquipSlot slot = EquipSlot.None;

        [Tooltip("С какого уровня можно надеть.")]
        public int requiredLevel = 1;

        [Header("Стопка")]
        [Tooltip("Складывается ли в стопку. Хлам и расходники — да, оружие и броня — нет.")]
        public bool stackable = false;

        [Tooltip("Сколько влезает в одну стопку.")]
        public int maxStack = 20;

        [Header("Оружие")]
        [Tooltip("Урон оружия. Ноль — предмет не оружие.")]
        public int weaponDamage = 0;

        [Tooltip("Можно взять во вторую руку. Кинжалы и одноручные клинки — да, двуручники — нет.")]
        public bool dualWieldable = false;

        [Tooltip("Секунд между ударами. Задаёт весь ритм боя, включая скорость анимации.")]
        public float attackInterval = 1.4f;

        [Header("Защита")]
        public int armor = 0;

        [Header("Прибавки к характеристикам")]
        public int strength = 0;
        public int agility = 0;
        public int stamina = 0;

        [Header("Еда")]
        [Tooltip("Сколько здоровья восстановит целиком. Ноль — не еда.")]
        public int healAmount = 0;

        [Tooltip("За сколько секунд. Ноль — мгновенно.")]
        public float healDuration = 0f;

        [Header("Внешний вид")]
        [Tooltip("Модель предмета в руке. Пусто — предмет невидим на персонаже.")]
        public GameObject worldModel;

        [Header("Торговля")]
        [Tooltip("За сколько монет купит торговец.")]
        public int vendorPrice = 1;

        public bool IsWeapon => weaponDamage > 0;
        public bool IsEquippable => slot != EquipSlot.None;

        /// <summary>Съедобное: клик в сумке тратит одну штуку и лечит.</summary>
        public bool IsFood => healAmount > 0;

        /// <summary>Прибавки к характеристикам одним блоком — так их удобно складывать.</summary>
        public StatBlock StatBonus => new StatBlock(strength, agility, stamina);

        /// <summary>Цвет названия по редкости. Канон жанра, менять нельзя.</summary>
        public Color RarityColor => rarity switch
        {
            // Приглушённая шкала. Яркий белый у обычных вещей перетягивал
            // внимание сильнее, чем фиолетовый у эпических: в сумке лежат
            // десятки обычных предметов и один-два ценных, и кричать должны
            // редкие, а не мусор.
            //
            // Порядок насыщенности теперь совпадает с порядком ценности —
            // чем реже вещь, тем ярче рамка.
            ItemRarity.Junk => new Color32(0x6A, 0x68, 0x62, 0xFF),
            ItemRarity.Common => new Color32(0x8E, 0x8C, 0x84, 0xFF),
            ItemRarity.Uncommon => new Color32(0x4E, 0xA8, 0x3C, 0xFF),
            ItemRarity.Rare => new Color32(0x3C, 0x74, 0xCC, 0xFF),
            ItemRarity.Epic => new Color32(0x9A, 0x4C, 0xC8, 0xFF),
            _ => new Color32(0xE0, 0x84, 0x1E, 0xFF)
        };

        /// <summary>Короткая строка для подсказки: «Кинжал, урон 12, +3 ловкости».</summary>
        public string ShortStats()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (weaponDamage > 0) parts.Add("урон " + weaponDamage);
            if (armor > 0) parts.Add("броня " + armor);
            if (strength > 0) parts.Add("+" + strength + " силы");
            if (agility > 0) parts.Add("+" + agility + " ловкости");
            if (stamina > 0) parts.Add("+" + stamina + " выносливости");

            return string.Join(", ", parts);
        }
    }
}
