using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Ресурс, который тратится на действия и восстанавливается сам.
    ///
    /// У разбойника это энергия: потолок не зависит от уровня, восстановление
    /// постоянное. Отсюда весь ритм класса — не «хватит ли маны до конца боя»,
    /// а «успела ли накапать энергия на следующий удар».
    /// </summary>
    public sealed class ResourcePool : MonoBehaviour
    {
        [SerializeField] private ResourceType type = ResourceType.Energy;
        [SerializeField] private int max = 100;

        [Tooltip("Сколько единиц восстанавливается за секунду.")]
        [SerializeField] private float regenPerSecond = 10f;

        [Tooltip("Пауза восстановления после траты. Ноль — копится всегда. У энергии паузы нет, это её отличие от маны.")]
        [SerializeField] private float regenDelay = 0f;

        private float current;
        private float nextRegenTime;
        private IsoRPG.Progression.TalentBook talents;
        private bool talentsChecked;

        public ResourceType Type => type;
        public int Max => max;
        public int Current => Mathf.FloorToInt(current);
        public float Fraction => max > 0 ? Mathf.Clamp01(current / max) : 0f;

        /// <summary>Значение изменилось: текущее, максимум.</summary>
        public event Action<int, int> Changed;

        private void Awake()
        {
            current = max;
        }

        private void Start()
        {
            Changed?.Invoke(Current, max);
        }

        private void Update()
        {
            if (current >= max) return;
            if (Time.time < nextRegenTime) return;

            int before = Current;

            // Таланты ускоряют восстановление. Спрашиваем книгу лениво: у
            // монстров её нет, а Update тут идёт каждый кадр.
            if (!talentsChecked)
            {
                talents = GetComponent<IsoRPG.Progression.TalentBook>();
                talentsChecked = true;
            }

            float rate = regenPerSecond;
            if (talents != null)
                rate *= 1f + talents.Bonus(IsoRPG.Progression.TalentEffect.EnergyRegen);

            current = Mathf.Min(max, current + rate * Time.deltaTime);

            // Событие шлём только когда изменилось целое значение: иначе
            // интерфейс перерисовывается каждый кадр без всякой пользы.
            if (Current != before) Changed?.Invoke(Current, max);
        }

        public bool Has(int amount) => current >= amount;

        /// <summary>Потратить. Возвращает false, если не хватило — вызывающий сам решает, что делать.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (current < amount) return false;

            current -= amount;
            if (regenDelay > 0f) nextRegenTime = Time.time + regenDelay;

            Changed?.Invoke(Current, max);
            return true;
        }

        /// <summary>Вернуть запас из сохранения.</summary>
        public void RestoreState(int value)
        {
            current = Mathf.Clamp(value, 0, max);
            Changed?.Invoke(Current, max);
        }

        public void Refill()
        {
            current = max;
            Changed?.Invoke(Current, max);
        }

        public void Setup(ResourceType newType, int newMax, float newRegen)
        {
            type = newType;
            max = Mathf.Max(1, newMax);
            regenPerSecond = newRegen;
            current = max;
        }
    }
}
