using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Здоровье любого участника боя — и игрока, и монстра.
    ///
    /// Числа сюда приходят снаружи (пока из инспектора, потом из таблиц),
    /// сам компонент только считает и сообщает о событиях. Всё остальное —
    /// анимация смерти, выпадение лута, начисление опыта — подписывается
    /// на события и делает свою часть.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private bool destroyOnDeath = false;
        [SerializeField] private float destroyDelay = 3f;

        private int current;
        private DefenseStats defense;

        public int Max => maxHealth;
        public int Current => current;
        public float Fraction => maxHealth > 0 ? (float)current / maxHealth : 0f;
        public bool IsAlive => current > 0;

        /// <summary>Изменилось здоровье: текущее, максимум.</summary>
        public event Action<int, int> Changed;

        /// <summary>Получен урон: сколько, от кого. Нужно для реакции и подсчёта угрозы.</summary>
        public event Action<int, GameObject> Damaged;

        /// <summary>Умер. От кого — понадобится, чтобы начислить опыт убийце.</summary>
        public event Action<GameObject> Died;

        private void Awake()
        {
            current = maxHealth;
        }

        private void Start()
        {
            // Сообщаем стартовое значение, чтобы полоски проставились
            // на первом же кадре, а не после первого удара.
            Changed?.Invoke(current, maxHealth);
        }

        /// <summary>
        /// Вернуть текущее здоровье из сохранения. Максимум не трогаем: его
        /// уже посчитали таланты и снаряжение, и перезаписать его сохранённым
        /// значило бы откатить их работу.
        /// </summary>
        public void RestoreState(int value)
        {
            current = Mathf.Clamp(value, 1, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        public void Setup(int newMax, bool refill = true)
        {
            maxHealth = Mathf.Max(1, newMax);
            if (refill) current = maxHealth;
            else current = Mathf.Min(current, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        /// <summary>
        /// Нанести урон. Возвращает, сколько дошло на самом деле — броня
        /// применяется здесь, и вызывающий должен показать игроку итог,
        /// а не то, что замахивался нанести.
        ///
        /// Защиту считает получатель, а не бьющий: иначе каждый источник
        /// урона обязан знать правила всех броней и щитов, и добавление
        /// нового вида защиты становится правкой всей боевой системы.
        /// </summary>
        public int TakeDamage(int amount, GameObject source = null)
        {
            if (!IsAlive || amount <= 0) return 0;

            int dealt = ApplyDefenses(amount, source);

            current = Mathf.Max(0, current - dealt);
            Changed?.Invoke(current, maxHealth);
            Damaged?.Invoke(dealt, source);

            if (current == 0)
            {
                Died?.Invoke(source);
                if (destroyOnDeath) Destroy(gameObject, destroyDelay);
            }

            return dealt;
        }

        private int ApplyDefenses(int amount, GameObject source)
        {
            if (defense == null) defense = GetComponent<DefenseStats>();
            if (defense == null) return amount;

            int attackerLevel = 1;
            if (source != null)
            {
                var attackerDefense = source.GetComponent<DefenseStats>();
                if (attackerDefense != null) attackerLevel = attackerDefense.Level;
            }

            // Поправка на разницу уровней. Намеренно мягкая: основную работу
            // делают броня и шанс отражения, а это лишь добавляет остроты.
            float levelFactor = LevelDifficulty.DamageMultiplier(attackerLevel, defense.Level);
            int adjusted = Mathf.Max(1, Mathf.RoundToInt(amount * levelFactor));

            return defense.ApplyArmor(adjusted, attackerLevel);
        }

        /// <summary>Вернуть к жизни с полным здоровьем. Нужно возрождению монстров.</summary>
        public void Revive()
        {
            current = maxHealth;
            Changed?.Invoke(current, maxHealth);
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
