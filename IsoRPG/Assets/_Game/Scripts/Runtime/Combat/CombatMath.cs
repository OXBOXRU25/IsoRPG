using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>Чем закончился удар. Нужно интерфейсу: три исхода рисуются по-разному.</summary>
    public enum HitResult
    {
        Normal,
        Crit,
        Miss,   // не полный промах, а частично отражённый удар — урон вдвое меньше
    }

    /// <summary>
    /// Общие правила расчёта урона: крит и отражение.
    ///
    /// Вынесено отдельно, чтобы автоатака, способности и будущие эффекты
    /// считали одинаково. Разные формулы в разных местах — самый быстрый
    /// способ получить баланс, который невозможно понять.
    /// </summary>
    public static class CombatMath
    {
        public const float DefaultCritChance = 0.1f;
        public const float DefaultCritMultiplier = 2f;
        public const float DefaultMissChance = 0.1f;
        public const float DefaultMissMultiplier = 0.5f;

        // Константы формулы брони. Чем они больше, тем слабее броня в целом.
        // При этих значениях и первом уровне броня 150 съедает примерно 43%
        // урона, броня 50 — около 20%.
        public const float ArmorConstant = 150f;
        public const float ArmorPerLevel = 50f;

        // Разница уровней: за каждый уровень цели выше нашего шанс отражения
        // растёт, ниже — падает. Полтора процента за уровень — мягко, но
        // за пять уровней разницы разница уже заметна.
        public const float MissChancePerLevel = 0.015f;

        /// <summary>Шанс отражения с поправкой на разницу уровней.</summary>
        public static float AdjustMissChance(float baseChance, int attackerLevel, int defenderLevel)
        {
            float delta = (defenderLevel - attackerLevel) * MissChancePerLevel;
            return Mathf.Clamp(baseChance + delta, 0f, 0.6f);
        }

        /// <summary>
        /// Бросок на исход и итоговый урон.
        ///
        /// Порядок бросков важен: сначала отражение, потом крит. Иначе
        /// отражённый крит давал бы обычный урон и выглядел бы как ошибка.
        /// </summary>
        public static int Roll(int baseDamage, float critChance, float critMultiplier,
                               float missChance, float missMultiplier, out HitResult result)
        {
            if (Random.value < missChance)
            {
                result = HitResult.Miss;
                return Mathf.Max(1, Mathf.RoundToInt(baseDamage * missMultiplier));
            }

            if (Random.value < critChance)
            {
                result = HitResult.Crit;
                return Mathf.RoundToInt(baseDamage * critMultiplier);
            }

            result = HitResult.Normal;
            return baseDamage;
        }
    }
}
