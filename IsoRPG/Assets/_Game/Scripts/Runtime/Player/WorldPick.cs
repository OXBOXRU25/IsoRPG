using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Items;
using IsoRPG.Localization;

namespace IsoRPG.Player
{
    /// <summary>Что оказалось под точкой мира и что с этим сделает клик.</summary>
    public enum PickKind
    {
        None,
        Trade,     // торговец
        Talk,      // собеседник с заданием
        Chest,     // сундук, который ещё не открыт
        Loot,      // мешок с добычей
        Enemy,     // живая враждебная цель
        Neutral,   // живое мирное существо
        Self,      // сам игрок
    }

    /// <summary>
    /// Опознание того, что лежит под курсором. Только опознание, без действия.
    ///
    /// Заведено потому, что цепочка «торговец, собеседник, сундук, мешок,
    /// живая цель» нужна теперь ДВАЖДЫ: клик по ней решает, что сделать, а
    /// наведение — что написать в подсказке. Две копии одной цепочки
    /// разъедутся обязательно, и разъедутся молча: подсказка начнёт обещать
    /// одно, а клик делать другое. Хуже такой подсказки только её отсутствие,
    /// потому что ей верят.
    ///
    /// Расстояния сюда не входят намеренно. «Далеко — подойди, близко —
    /// открой» относится к действию, а не к опознанию: сундук остаётся
    /// сундуком с любой дистанции, и подсказка над ним одна и та же.
    /// </summary>
    public readonly struct WorldPick
    {
        public readonly PickKind Kind;

        /// <summary>Корень найденного: торговец, сундук, мешок, существо.</summary>
        public readonly Component Thing;

        /// <summary>Метка цели, если она у найденного есть. Иначе null.</summary>
        public readonly Targetable Target;

        private readonly string label;

        private WorldPick(PickKind kind, Component thing, Targetable target, string label)
        {
            Kind = kind;
            Thing = thing;
            Target = target;
            this.label = label;
        }

        public static readonly WorldPick Nothing =
            new WorldPick(PickKind.None, null, null, null);

        public bool Found => Kind != PickKind.None;

        /// <summary>Имя для панели. Уже переведено.</summary>
        public string Name => string.IsNullOrEmpty(label) ? string.Empty : Loc.T(label);

        /// <summary>
        /// Имя как оно записано в данных, по-русски и без перевода.
        ///
        /// Нужно отдельно от <see cref="Name"/>, потому что справочник
        /// портретов ищет именно по русскому имени — оно же служит ключом
        /// перевода. Передать туда переведённое значит остаться без портретов
        /// на всех языках, кроме русского, причём молча: справочник просто
        /// вернёт пусто, а круг портрета останется пустым.
        /// </summary>
        public string RawName => label ?? string.Empty;

        /// <summary>
        /// Что случится по нажатию. Пустая строка — писать нечего.
        ///
        /// Формулировки от лица игрока и в повелительном наклонении: человек
        /// читает подсказку как ответ на «что мне тут делать», а не как
        /// описание объекта.
        /// </summary>
        public string Hint
        {
            get
            {
                switch (Kind)
                {
                    case PickKind.Trade:   return Loc.T("ЛКМ — торговать");
                    case PickKind.Talk:    return Loc.T("ЛКМ — говорить");
                    case PickKind.Chest:   return Loc.T("ЛКМ — открыть");
                    case PickKind.Loot:    return Loc.T("ЛКМ — обыскать");
                    case PickKind.Enemy:   return Loc.T("ЛКМ — атаковать");
                    default:               return string.Empty;
                }
            }
        }

        /// <summary>Можно ли бить. От этого зависит, рисовать ли полосу жизни.</summary>
        public bool Attackable => Kind == PickKind.Enemy;

        /// <summary>
        /// Разобрать один коллайдер.
        ///
        /// Порядок проверок повторяет порядок в обработчике клика и должен
        /// повторять его впредь: лавочник раньше собеседника, потому что у
        /// него может быть и квест, но пришли к нему торговать; сундук раньше
        /// мешка, потому что выпавший мешок ложится рядом с открытым сундуком.
        /// </summary>
        public static WorldPick From(Collider hit, GameObject self)
        {
            if (hit == null) return Nothing;

            var shop = hit.GetComponentInParent<Merchant>();
            if (shop != null)
                return new WorldPick(PickKind.Trade, shop, null, shop.DisplayName);

            var giver = hit.GetComponentInParent<IsoRPG.Quests.QuestGiver>();
            if (giver != null)
                return new WorldPick(PickKind.Talk, giver, null, giver.DisplayName);

            var chest = hit.GetComponentInParent<TreasureChest>();
            if (chest != null && !chest.IsOpen)
                return new WorldPick(PickKind.Chest, chest, null, "Сундук");

            var bag = hit.GetComponentInParent<LootDrop>();
            if (bag != null)
                return new WorldPick(PickKind.Loot, bag, null, "Мешок с добычей");

            var targetable = hit.GetComponentInParent<Targetable>();
            if (targetable == null) return Nothing;

            if (targetable.gameObject == self)
                return new WorldPick(PickKind.Self, targetable, targetable,
                                     targetable.DisplayName);

            // Мёртвые не опознаются вовсе: труп либо уже превратился в мешок
            // и попал в ветку выше, либо доигрывает падение — и подсказка
            // «атаковать» над ним была бы враньём.
            if (!targetable.IsAlive) return Nothing;

            var kind = targetable.Faction == Faction.Hostile
                ? PickKind.Enemy
                : PickKind.Neutral;

            return new WorldPick(kind, targetable, targetable, targetable.DisplayName);
        }

        /// <summary>
        /// Разобрать луч целиком: первое попадание, в котором есть смысл.
        ///
        /// Именно все попадания, а не ближайшее: коллайдер существа
        /// перекрывается травой и собственной полоской здоровья, и ближайшее
        /// окажется не тем, во что целился игрок.
        /// </summary>
        public static WorldPick From(RaycastHit[] hits, int count, GameObject self)
        {
            for (int i = 0; i < count; i++)
            {
                var pick = From(hits[i].collider, self);
                if (pick.Found) return pick;
            }

            return Nothing;
        }
    }
}
