using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>Насколько цель опасна относительно игрока.</summary>
    public enum DifficultyTier
    {
        Trivial,    // серый — опыта не даёт
        Easy,       // зелёный — заметно слабее
        Even,       // жёлтый — равный
        Hard,       // оранжевый — опасный
        Deadly,     // красный — не лезь
    }

    /// <summary>
    /// Оценка опасности цели по разнице уровней: цвет для интерфейса и
    /// поправка к урону.
    ///
    /// Цвета взяты каноничные для жанра. Это не украшательство: игрок читает
    /// цвет мгновенно и не задумываясь, и если наш зелёный будет означать не
    /// то, что везде, игроки станут ошибаться в оценке противника.
    /// </summary>
    public static class LevelDifficulty
    {
        // Максимальный уровень персонажа. Решено 23.08.2026: шестьдесят,
        // как в классическом WoW.
        public const int MaxPlayerLevel = 60;

        // Поправка к урону за уровень разницы. Намеренно слабая: разницу
        // уровней у нас уже отыгрывают броня (растёт с уровнем цели) и шанс
        // отражения. Третье наказание поверх них сделало бы бой с сильным
        // противником не сложным, а бессмысленным.
        //
        // Если понадобится острее — крутить здесь, это единственное место.
        public const float DamagePerLevelDiff = 0.025f;

        // Предел поправки в обе стороны, чтобы разница в двадцать уровней
        // не превращала удар в ноль или в десятикратный.
        public const float MaxDamageBonus = 0.30f;
        public const float MaxDamagePenalty = 0.40f;

        private static readonly Color TrivialColor = new Color32(0x9A, 0x9A, 0x96, 0xFF);
        private static readonly Color EasyColor = new Color32(0x4E, 0xA8, 0x3C, 0xFF);
        private static readonly Color EvenColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color HardColor = new Color32(0xE0, 0x8A, 0x30, 0xFF);
        private static readonly Color DeadlyColor = new Color32(0xC4, 0x3A, 0x32, 0xFF);

        public static DifficultyTier Evaluate(int targetLevel, int playerLevel)
        {
            int diff = targetLevel - playerLevel;

            if (diff <= -10) return DifficultyTier.Trivial;
            if (diff <= -3) return DifficultyTier.Easy;
            if (diff <= 2) return DifficultyTier.Even;
            if (diff <= 4) return DifficultyTier.Hard;
            return DifficultyTier.Deadly;
        }

        public static Color ColorOf(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Trivial => TrivialColor,
            DifficultyTier.Easy => EasyColor,
            DifficultyTier.Even => EvenColor,
            DifficultyTier.Hard => HardColor,
            _ => DeadlyColor
        };

        public static Color ColorOf(int targetLevel, int playerLevel) =>
            ColorOf(Evaluate(targetLevel, playerLevel));

        /// <summary>Даёт ли цель опыт. Серые не дают — это и есть смысл серого.</summary>
        public static bool GivesExperience(int targetLevel, int playerLevel) =>
            Evaluate(targetLevel, playerLevel) != DifficultyTier.Trivial;

        /// <summary>
        /// Множитель урона по разнице уровней. Бьём слабого — чуть больнее,
        /// сильного — чуть слабее.
        /// </summary>
        public static float DamageMultiplier(int attackerLevel, int defenderLevel)
        {
            int diff = attackerLevel - defenderLevel;
            float raw = diff * DamagePerLevelDiff;

            return 1f + Mathf.Clamp(raw, -MaxDamagePenalty, MaxDamageBonus);
        }
    }
}
