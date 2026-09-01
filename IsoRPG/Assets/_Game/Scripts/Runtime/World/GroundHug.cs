using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.World
{
    /// <summary>
    /// Прижать персонажа к грунту, не наклоняя его.
    ///
    /// Человек на склоне держит корпус вертикально — в отличие от зверя,
    /// которому наклон обязателен, иначе лапы висят. Поэтому здесь только
    /// высота: <see cref="GroundAlign"/> для четвероногих, этот — для тех,
    /// кто ходит стоя.
    ///
    /// Зачем вообще. Навигационная сетка печётся по коллайдерам и ложится
    /// ВЫШЕ грунта — мелкий декор её задирает, на нашей карте расхождение
    /// доходило до 89 см. Агент честно ставит персонажа на сетку, и тот
    /// висит в воздухе. Смотрится это как ошибка модели, а не навигации,
    /// и чинить тянет посадку персонажа.
    ///
    /// Правим не позицию, а <c>baseOffset</c> агента: это штатное смещение
    /// «модель относительно сетки», и агент его уважает. Присваивание
    /// <c>transform.position</c> агент бы перезаписывал каждый кадр, и мы
    /// спорили бы с ним за одну и ту же величину.
    ///
    /// Считать надо каждый кадр, а не один раз при расстановке: разница
    /// между сеткой и грунтом своя в каждой точке карты, и посадка, верная
    /// на старте, через десять шагов уже неверна.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GroundHug : MonoBehaviour
    {
        [Tooltip("Откуда пускать луч вниз, метров над персонажем.")]
        [SerializeField] private float rayStart = 1.5f;

        [Tooltip("Насколько далеко искать землю, метров.")]
        [SerializeField] private float rayLength = 6f;

        [Tooltip("Скорость подгонки. Больше — резче, меньше — плавнее.")]
        [SerializeField] private float smooth = 10f;

        [Tooltip("Предел смещения, метров. Защита от улёта под землю.")]
        [SerializeField] private float limit = 2f;

        [Tooltip("По каким слоям искать землю.")]
        [SerializeField] private LayerMask ground = ~0;

        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void LateUpdate()
        {
            // LateUpdate: агент уже отработал и поставил персонажа на сетку.
            // Наша поправка ложится поверх, а не стирается в том же кадре.
            Vector3 from = transform.position + Vector3.up * rayStart;

            // Луч первым делом встречает СВОЙ коллайдер — капсула персонажа
            // стоит ровно здесь, и её бок отлично сходит за землю. Перебираем
            // попадания и берём ближайшее чужое.
            var hits = Physics.RaycastAll(from, Vector3.down, rayLength, ground,
                                          QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearest = float.MaxValue;
            RaycastHit hit = default;

            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(transform)) continue;
                // Чужое живое тело тоже не земля: иначе моб встаёт на спину
                // соседа. Признак живого — навигационный агент выше по ветке.
                if (candidate.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                if (candidate.distance >= nearest) continue;

                nearest = candidate.distance;
                hit = candidate;
                found = true;
            }

            // Земли под ногами нет — оставляем как есть. Обнулять смещение
            // нельзя: над обрывом персонаж дёрнется вверх на глазах игрока.
            if (!found) return;

            // Насколько ноги висят над грунтом прямо сейчас.
            float gap = transform.position.y - hit.point.y;

            float wanted = Mathf.Clamp(agent.baseOffset - gap, -limit, limit);

            agent.baseOffset = Mathf.Lerp(agent.baseOffset, wanted,
                                          1f - Mathf.Exp(-smooth * Time.deltaTime));
        }
    }
}
