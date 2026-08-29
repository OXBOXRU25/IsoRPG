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

        [Header("Характеристики")]
        [Tooltip("Выносливость на первом уровне без снаряжения.")]
        [SerializeField] private int baseStamina = 10;

        [Tooltip("Сила на первом уровне без снаряжения.")]
        [SerializeField] private int baseStrength = 8;

        [Tooltip("Ловкость на первом уровне без снаряжения.")]
        [SerializeField] private int baseAgility = 12;

        [Tooltip("Сколько каждой характеристики прибавляет уровень.")]
        [SerializeField] private int staminaPerLevel = 2;
        [SerializeField] private int strengthPerLevel = 1;
        [SerializeField] private int agilityPerLevel = 2;

        [Header("Во что превращаются")]
        [Tooltip("Здоровья за единицу выносливости.")]
        [SerializeField] private int healthPerStamina = 10;

        [Tooltip("Здоровье, которое есть даже при нулевой выносливости.")]
        [SerializeField] private int healthFloor = 100;

        /// <summary>
        /// Полные характеристики: основа, прирост за уровни и снаряжение.
        ///
        /// Считаются здесь, а не в трёх местах по отдельности: окно
        /// персонажа, расчёт здоровья и боевые формулы обязаны показывать и
        /// использовать одно и то же число, иначе игрок видит одно, а бьёт
        /// по-другому.
        /// </summary>
        public StatBlock TotalStats
        {
            get
            {
                int level = experience != null ? experience.Level : 1;
                int steps = Mathf.Max(0, level - 1);

                var fromLevels = new StatBlock(
                    baseStrength + strengthPerLevel * steps,
                    baseAgility + agilityPerLevel * steps,
                    baseStamina + staminaPerLevel * steps);

                var fromGear = equipment != null ? equipment.TotalStatBonus() : new StatBlock(0, 0, 0);

                return fromLevels + fromGear;
            }
        }

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
            if (experience != null) experience.LevelUp += OnLevelUp;
        }

        private void OnDisable()
        {
            if (book != null) book.Changed -= Apply;
            if (experience != null) experience.LevelUp -= OnLevelUp;
        }

        /// <summary>
        /// Новый уровень: пересчитать запас и долечить до полного.
        ///
        /// Долечиваем намеренно, хотя обычная прибавка от таланта этого не
        /// делает. Повышение уровня — редкое событие и награда: получить его
        /// посреди боя и остаться при тех же двадцати процентах здоровья
        /// значит не заметить награды вовсе. Так устроено в большинстве игр
        /// с уровнями, и игрок этого ждёт.
        /// </summary>
        private void OnLevelUp(int level)
        {
            Apply();

            if (health != null) health.Heal(health.Max);
        }

        private void Apply()
        {
            if (book == null) return;

            if (health != null)
            {
                // Здоровье = пол плюс выносливость, и уже на это ложатся
                // проценты от талантов. Порядок важен: талант «плюс десять
                // процентов» должен прибавлять от того, что есть сейчас, а
                // не от того, с чем герой начинал игру.
                //
                // На первом уровне без снаряжения выходит ровно 200 — те же,
                // что были заданы вручную до появления характеристик.
                float fromStamina = healthFloor + TotalStats.Stamina * healthPerStamina;

                int target = Mathf.RoundToInt(
                    fromStamina * (1f + book.Bonus(TalentEffect.MaxHealth)));

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
