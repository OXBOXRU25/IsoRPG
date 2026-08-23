using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.Progression
{
    /// <summary>
    /// Применяет те таланты, которые нельзя спросить в момент удара.
    ///
    /// Урон и криты считаются на месте — там прибавка нужна ровно на один
    /// бросок. А запас здоровья и броня хранятся в компонентах и живут между
    /// ударами, поэтому их надо пересчитывать, когда дерево изменилось.
    ///
    /// База снимается один раз при старте. Иначе прибавка ложилась бы на уже
    /// увеличенное значение, и три очка в живучесть давали бы не +15%, а
    /// +15% от +15% от +15% — классический способ получить бессмертного
    /// героя из совершенно правильных чисел.
    /// </summary>
    [RequireComponent(typeof(TalentBook))]
    public sealed class TalentStats : MonoBehaviour
    {
        private TalentBook book;
        private Health health;
        private DefenseStats defense;
        private Equipment equipment;
        private Experience experience;

        private int baseMaxHealth;

        private void Awake()
        {
            book = GetComponent<TalentBook>();
            health = GetComponent<Health>();
            defense = GetComponent<DefenseStats>();
            equipment = GetComponent<Equipment>();
            experience = GetComponent<Experience>();
        }

        private void Start()
        {
            // В Start, а не в Awake: к этому моменту сборщик и стартовое
            // снаряжение уже выставили настоящие числа.
            if (health != null) baseMaxHealth = health.Max;

            Apply();
        }

        private void OnEnable()
        {
            if (book != null) book.Changed += Apply;
        }

        private void OnDisable()
        {
            if (book != null) book.Changed -= Apply;
        }

        private void Apply()
        {
            if (book == null) return;

            if (health != null && baseMaxHealth > 0)
            {
                int target = Mathf.RoundToInt(
                    baseMaxHealth * (1f + book.Bonus(TalentEffect.MaxHealth)));

                // Не долечиваем: прибавка даёт запас, а не бесплатное
                // исцеление посреди боя.
                if (target != health.Max) health.Setup(target, refill: false);
            }

            if (defense != null)
            {
                int level = experience != null ? experience.Level : defense.Level;
                int armor = equipment != null ? equipment.TotalArmor() : defense.Armor;

                defense.Setup(level, armor);
            }
        }
    }
}
