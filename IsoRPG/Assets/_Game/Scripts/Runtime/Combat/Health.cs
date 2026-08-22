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

        public void Setup(int newMax, bool refill = true)
        {
            maxHealth = Mathf.Max(1, newMax);
            if (refill) current = maxHealth;
            else current = Mathf.Min(current, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        public void TakeDamage(int amount, GameObject source = null)
        {
            if (!IsAlive || amount <= 0) return;

            current = Mathf.Max(0, current - amount);
            Changed?.Invoke(current, maxHealth);
            Damaged?.Invoke(amount, source);

            if (current == 0)
            {
                Died?.Invoke(source);
                if (destroyOnDeath) Destroy(gameObject, destroyDelay);
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
