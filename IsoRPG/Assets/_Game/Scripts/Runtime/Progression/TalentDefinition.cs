using UnityEngine;

namespace IsoRPG.Progression
{
    /// <summary>Ветка дерева. Три — как у разбойника в Classic.</summary>
    public enum TalentBranch
    {
        /// <summary>Убийство: урон и критические удары.</summary>
        Assassination,

        /// <summary>Бой: темп, стойкость, живучесть.</summary>
        Combat,

        /// <summary>Скрытность: подход, засада, уход.</summary>
        Subtlety,
    }

    /// <summary>
    /// Что талант делает. Проценты, а не плоские числа: плоская прибавка
    /// решает на первом уровне и не значит ничего на двадцатом, а доля
    /// держит вес одинаковым всю игру.
    /// </summary>
    public enum TalentEffect
    {
        /// <summary>Шанс критического удара, доля.</summary>
        CritChance,

        /// <summary>Множитель критического урона, доля сверх обычного.</summary>
        CritMultiplier,

        /// <summary>Весь наносимый урон, доля.</summary>
        Damage,

        /// <summary>Урон приёмов, доля.</summary>
        AbilityDamage,

        /// <summary>Урон приёмов из скрытности, доля.</summary>
        StealthDamage,

        /// <summary>Скорость обычных атак, доля.</summary>
        AttackSpeed,

        /// <summary>Запас здоровья, доля.</summary>
        MaxHealth,

        /// <summary>Броня, единицы.</summary>
        Armor,

        /// <summary>Восстановление энергии, доля.</summary>
        EnergyRegen,

        /// <summary>Скорость передвижения в скрытности, доля.</summary>
        StealthSpeed,
    }

    /// <summary>
    /// Один талант дерева.
    ///
    /// Ярусы открываются вложениями в ЭТУ ветку, а не общим уровнем: иначе
    /// выбора нет — к тридцатому уровню открыто всё и везде. Требование
    /// вкладывать в глубину и есть то, что делает три ветки тремя разными
    /// персонажами, а не одним со всеми умениями сразу.
    /// </summary>
    [CreateAssetMenu(fileName = "TL_New", menuName = "IsoRPG/Талант")]
    public sealed class TalentDefinition : ScriptableObject
    {
        [Tooltip("Как называется в окне талантов.")]
        public string displayName = "Талант";

        [Tooltip("Что делает. Одна фраза без чисел — числа считаются сами.")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Иконка. Пусто — плашка цветом ветки.")]
        public Sprite icon;

        public TalentBranch branch = TalentBranch.Assassination;

        [Tooltip("Ярус: 0 — верхний, доступен сразу.")]
        [Range(0, 3)]
        public int row = 0;

        [Tooltip("Колонка внутри яруса: 0 или 1.")]
        [Range(0, 1)]
        public int column = 0;

        [Tooltip("Сколько раз можно вложить.")]
        [Range(1, 5)]
        public int maxRank = 3;

        public TalentEffect effect = TalentEffect.Damage;

        [Tooltip("Сколько даёт один ранг. Доли — это доли: 0.03 значит +3%.")]
        public float valuePerRank = 0.03f;

        /// <summary>
        /// Сколько очков надо вложить в ветку, чтобы открыть ярус.
        /// Три за ярус — как в Classic, и это не случайное число: ровно
        /// столько стоит один талант в максимальном ранге.
        /// </summary>
        public int RequiredInBranch => row * 3;

        /// <summary>Прибавка при данном числе рангов.</summary>
        public float ValueAt(int rank) => valuePerRank * Mathf.Clamp(rank, 0, maxRank);

        /// <summary>
        /// Строка вида «+6% к урону». Считается, а не пишется руками: иначе
        /// текст и число разъезжаются при первой же правке баланса.
        /// </summary>
        public string EffectLine(int rank)
        {
            float value = ValueAt(Mathf.Max(1, rank));

            string amount = effect == TalentEffect.Armor
                ? "+" + Mathf.RoundToInt(value)
                : "+" + Mathf.RoundToInt(value * 100f) + "%";

            return amount + " " + EffectName(effect);
        }

        public static string EffectName(TalentEffect effect) => effect switch
        {
            TalentEffect.CritChance => "к шансу крита",
            TalentEffect.CritMultiplier => "к урону крита",
            TalentEffect.Damage => "ко всему урону",
            TalentEffect.AbilityDamage => "к урону приёмов",
            TalentEffect.StealthDamage => "к урону из скрытности",
            TalentEffect.AttackSpeed => "к скорости атаки",
            TalentEffect.MaxHealth => "к запасу здоровья",
            TalentEffect.Armor => "к броне",
            TalentEffect.EnergyRegen => "к восстановлению энергии",
            TalentEffect.StealthSpeed => "к скорости в скрытности",
            _ => "",
        };

        public static string BranchName(TalentBranch branch) => branch switch
        {
            TalentBranch.Assassination => "Убийство",
            TalentBranch.Combat => "Бой",
            TalentBranch.Subtlety => "Скрытность",
            _ => "",
        };

        public static Color BranchColor(TalentBranch branch) => branch switch
        {
            TalentBranch.Assassination => new Color32(0x8C, 0x2F, 0x2F, 0xFF),
            TalentBranch.Combat => new Color32(0xB0, 0x7A, 0x2A, 0xFF),
            TalentBranch.Subtlety => new Color32(0x4A, 0x3C, 0x7A, 0xFF),
            _ => Color.gray,
        };
    }
}
