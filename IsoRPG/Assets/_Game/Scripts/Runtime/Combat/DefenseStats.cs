using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Защита: броня и уровень. Всё, что уменьшает входящий урон.
    ///
    /// Живёт на защищающемся, а не на атакующем — броня это его знание.
    /// Атакующий считает «сырой» урон и передаёт цели, а цель уже решает,
    /// сколько от него дойдёт. Иначе каждый источник урона должен знать
    /// правила всех защит, и добавление щита превращается в правку всего.
    /// </summary>
    public sealed class DefenseStats : MonoBehaviour
    {
        [Tooltip("Уровень. Определяет силу и то, насколько цель опасна для игрока.")]
        [SerializeField] private int level = 1;

        [Tooltip("Броня. Снижает урон в процентах, с насыщением — до неуязвимости не доходит никогда.")]
        [SerializeField] private int armor = 0;

        public int Level => level;
        public int Armor => armor;

        public void Setup(int newLevel, int newArmor)
        {
            level = Mathf.Max(1, newLevel);
            armor = Mathf.Max(0, newArmor);
        }

        /// <summary>
        /// Какая доля урона будет поглощена бронёй, от 0 до 1.
        ///
        /// Формула с насыщением, а не вычитание: вычитание убивает слабое
        /// оружие насмерть (броня 10 против удара 10 — ноль урона) и почти
        /// не мешает сильному. Здесь каждая единица брони помогает чуть
        /// меньше предыдущей, а сотни процентов не бывает в принципе.
        /// </summary>
        public float DamageReduction(int attackerLevel)
        {
            if (armor <= 0) return 0f;

            float divisor = armor + CombatMath.ArmorConstant
                                  + CombatMath.ArmorPerLevel * Mathf.Max(1, attackerLevel);

            return Mathf.Clamp01(armor / divisor);
        }

        /// <summary>Сколько урона дойдёт после брони. Минимум единица — совсем без урона удары не бывают.</summary>
        public int ApplyArmor(int rawDamage, int attackerLevel)
        {
            if (rawDamage <= 0) return 0;

            float reduced = rawDamage * (1f - DamageReduction(attackerLevel));
            return Mathf.Max(1, Mathf.RoundToInt(reduced));
        }
    }
}
