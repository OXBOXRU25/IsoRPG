using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Опыт и уровни персонажа.
    ///
    /// Кривая рассчитана сразу на шестьдесят уровней, хотя контента пока на
    /// пять-семь: переделывать масштаб прогрессии потом крайне дорого — от
    /// него зависят урон, здоровье, цены и время боя.
    /// </summary>
    public sealed class Experience : MonoBehaviour
    {
        [Tooltip("Текущий уровень.")]
        [SerializeField] private int level = 1;

        [Tooltip("Опыт, накопленный внутри текущего уровня.")]
        [SerializeField] private int current = 0;

        private DefenseStats defense;

        public int Level => level;
        public int Current => current;
        public int ToNextLevel => RequiredFor(level);
        public float Fraction => ToNextLevel > 0 ? Mathf.Clamp01((float)current / ToNextLevel) : 0f;
        public bool IsMaxLevel => level >= LevelDifficulty.MaxPlayerLevel;

        /// <summary>Опыт изменился: сколько внутри уровня, сколько нужно до следующего.</summary>
        public event Action<int, int> Changed;

        /// <summary>Повышение уровня: новый уровень.</summary>
        public event Action<int> LevelUp;

        private void Awake()
        {
            defense = GetComponent<DefenseStats>();
            SyncLevelToStats();
        }

        private void Start()
        {
            Changed?.Invoke(current, ToNextLevel);
        }

        /// <summary>
        /// Сколько опыта нужно, чтобы уйти с указанного уровня на следующий.
        ///
        /// Степенная кривая: каждый следующий уровень дороже предыдущего, но
        /// не в разы. На шестидесятом суммарно выходит около 1.26 миллиона —
        /// тот же порядок, что в классическом WoW.
        /// </summary>
        public static int RequiredFor(int forLevel)
        {
            if (forLevel >= LevelDifficulty.MaxPlayerLevel) return 0;
            return Mathf.RoundToInt(80f * Mathf.Pow(Mathf.Max(1, forLevel), 1.6f));
        }

        /// <summary>Вернуть уровень и опыт из сохранения, без наград за подъём.</summary>
        public void RestoreState(int savedLevel, int savedExperience)
        {
            level = Mathf.Clamp(savedLevel, 1, LevelDifficulty.MaxPlayerLevel);
            current = Mathf.Max(0, savedExperience);

            SyncLevelToStats();

            // Событие подъёма НЕ шлём: оно лечит, играет джингл и выдаёт очко
            // талантов. При загрузке всё это уже случилось когда-то.
            Changed?.Invoke(current, ToNextLevel);
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

            current += amount;

            // Цикл, а не однократная проверка: с большой награды можно
            // перескочить сразу несколько уровней, и это должно работать.
            while (!IsMaxLevel && current >= ToNextLevel)
            {
                current -= ToNextLevel;
                level++;
                SyncLevelToStats();
                LevelUp?.Invoke(level);
            }

            if (IsMaxLevel) current = 0;

            Changed?.Invoke(current, ToNextLevel);
        }

        /// <summary>Выдать уровни напрямую. Нужно отладке: контента на 60 уровней ещё нет.</summary>
        public void GrantLevels(int count)
        {
            if (count <= 0) return;

            for (int i = 0; i < count && !IsMaxLevel; i++)
            {
                level++;
                SyncLevelToStats();
                LevelUp?.Invoke(level);
            }

            current = 0;
            Changed?.Invoke(current, ToNextLevel);
        }

        /// <summary>
        /// Уровень хранится здесь, но боевые расчёты спрашивают его у
        /// DefenseStats. Держим их в согласии, чтобы не было двух разных
        /// «уровней» у одного персонажа.
        /// </summary>
        private void SyncLevelToStats()
        {
            if (defense == null) defense = GetComponent<DefenseStats>();
            if (defense != null) defense.Setup(level, defense.Armor);
        }

        /// <summary>
        /// Сколько опыта даёт цель этого уровня. Классическая формула жанра
        /// с поправкой на разницу: серые не дают ничего, слабые меньше.
        /// </summary>
        public static int RewardFor(int targetLevel, int killerLevel)
        {
            var tier = LevelDifficulty.Evaluate(targetLevel, killerLevel);
            if (tier == DifficultyTier.Trivial) return 0;

            int baseReward = 5 * targetLevel + 45;

            float factor = tier switch
            {
                DifficultyTier.Easy => 0.6f,
                DifficultyTier.Even => 1f,
                DifficultyTier.Hard => 1.3f,
                DifficultyTier.Deadly => 1.6f,
                _ => 0f
            };

            return Mathf.RoundToInt(baseReward * factor);
        }
    }
}
