using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Комбо-очки разбойника. Копятся НА ЦЕЛИ, а не на персонаже.
    ///
    /// Это не мелочь, а суть механики: очки, набитые на одном враге, нельзя
    /// потратить на другого. Отсюда решение, которое игрок принимает в каждом
    /// бою — добивать текущего или переключаться, теряя накопленное.
    /// </summary>
    public sealed class ComboPoints : MonoBehaviour
    {
        [SerializeField] private int maxPoints = 5;

        private Targetable holder;   // на ком сейчас висят очки
        private int points;

        public int Points => points;
        public int MaxPoints => maxPoints;
        public Targetable Holder => holder;

        /// <summary>Очки изменились: сколько, максимум.</summary>
        public event Action<int, int> Changed;

        /// <summary>Начислить очки за удар по цели.</summary>
        public void Add(Targetable target, int amount = 1)
        {
            if (target == null || amount <= 0) return;

            // Сменилась цель — накопленное сгорает. Игрок должен чувствовать
            // цену переключения, иначе комбо превращается в общий счётчик.
            if (holder != target)
            {
                holder = target;
                points = 0;
            }

            points = Mathf.Min(maxPoints, points + amount);
            Changed?.Invoke(points, maxPoints);
        }

        /// <summary>Забрать все очки для финишера. Возвращает, сколько было.</summary>
        public int Consume(Targetable target)
        {
            if (holder != target || points <= 0) return 0;

            int spent = points;
            points = 0;
            Changed?.Invoke(points, maxPoints);
            return spent;
        }

        /// <summary>Сколько очков сейчас на этой цели. Ноль, если очки висят на другой.</summary>
        public int PointsOn(Targetable target) => holder == target ? points : 0;

        public void Clear()
        {
            if (points == 0 && holder == null) return;

            holder = null;
            points = 0;
            Changed?.Invoke(points, maxPoints);
        }

        private void Update()
        {
            // Цель умерла или исчезла — очки на ней больше не имеют смысла.
            if (holder != null && !holder.IsAlive) Clear();
        }
    }
}
