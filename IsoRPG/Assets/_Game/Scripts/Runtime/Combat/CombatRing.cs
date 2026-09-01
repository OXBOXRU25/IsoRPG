using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Кольцо вокруг цели: каждый нападающий держит свой сектор.
    ///
    /// Заведено 01.09.2026 по решению Павла. Повод: босс отходил от игрока и
    /// вставал ровно на мелкого кабана — все нападающие целились в одну и ту
    /// же точку «перед целью», и в тесноте лезли друг в друга.
    ///
    /// Как в больших РПГ: вокруг цели восемь секторов, боец занимает
    /// свободный и держится его, пока дерётся. Свободного нет — берёт
    /// ближайший к себе, чтобы не бежать через всё кольцо.
    ///
    /// Считаем от ЦЕЛИ, а не от конкретного героя: это ММО, и когда одного
    /// кабана бьют двое, кольцо остаётся одно — вокруг кабана, — и мобы
    /// расходятся по нему так же спокойно.
    ///
    /// Цена: словарь по цели и массив на восемь ссылок. Никаких поисков по
    /// сцене и обходов иерархии — в бою участвует каждый кадр.
    /// </summary>
    public static class CombatRing
    {
        /// <summary>Сколько мест вокруг цели. Восемь — по числу сторон света, как в жанре.</summary>
        private const int Sectors = 8;

        private static readonly Dictionary<Transform, Component[]> rings =
            new Dictionary<Transform, Component[]>();

        /// <summary>
        /// Точка, где этому бойцу стоять относительно цели.
        ///
        /// Сектор закрепляется за бойцом при первом обращении и держится,
        /// пока он не отпустит место: иначе моб перебирал бы сектора каждый
        /// кадр и дрожал вокруг цели.
        /// </summary>
        public static Vector3 StandPoint(Transform target, Component fighter, float radius)
        {
            int sector = Claim(target, fighter);

            float angle = sector * Mathf.PI * 2f / Sectors;
            var offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;

            return target.position + offset;
        }

        /// <summary>Освобождает место: боец умер, потерял цель или вышел из боя.</summary>
        public static void Release(Transform target, Component fighter)
        {
            if (target == null || !rings.TryGetValue(target, out var ring)) return;

            for (int i = 0; i < ring.Length; i++)
                if (ReferenceEquals(ring[i], fighter)) ring[i] = null;
        }

        /// <summary>Забывает цель целиком. Зовётся, когда она умерла.</summary>
        public static void Forget(Transform target)
        {
            if (target != null) rings.Remove(target);
        }

        private static int Claim(Transform target, Component fighter)
        {
            if (!rings.TryGetValue(target, out var ring))
            {
                ring = new Component[Sectors];
                rings[target] = ring;
            }

            // Уже стоим в кольце — остаёмся на месте.
            for (int i = 0; i < Sectors; i++)
                if (ReferenceEquals(ring[i], fighter)) return i;

            // Ищем свободный сектор, начиная с того, что ближе к нам сейчас:
            // так боец занимает место рядом, а не бежит на другую сторону.
            Vector3 toSelf = fighter.transform.position - target.position;
            toSelf.y = 0f;

            float own = Mathf.Atan2(toSelf.x, toSelf.z);
            if (own < 0f) own += Mathf.PI * 2f;

            int nearest = Mathf.RoundToInt(own / (Mathf.PI * 2f) * Sectors) % Sectors;

            for (int step = 0; step < Sectors; step++)
            {
                // Обходим кольцо в обе стороны от ближайшего: справа, слева, дальше.
                int side = (step % 2 == 0) ? step / 2 : -(step / 2 + 1);
                int index = ((nearest + side) % Sectors + Sectors) % Sectors;

                var holder = ring[index];
                if (holder == null || holder.Equals(null))
                {
                    ring[index] = fighter;
                    return index;
                }
            }

            // Все восемь заняты — встаём вторым рядом на своём же секторе.
            return nearest;
        }
    }
}
