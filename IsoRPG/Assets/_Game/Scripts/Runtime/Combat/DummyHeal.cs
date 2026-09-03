using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Манекен, которого нельзя убить.
    ///
    /// Здоровье возвращается через полсекунды после последнего удара. Именно
    /// возвращается, а не стоит на месте: полоска над головой должна дёрнуться
    /// от попадания, иначе непонятно, попал ты или махнул мимо — а смотреть
    /// боевые анимации приходится по многу раз подряд, и живая цель для этого
    /// не годится.
    ///
    /// Компонент нарочно тупой: ни ИИ, ни ответа, ни смерти. Всё, что делает
    /// манекен манекеном, снимается заданием `dummy` при сборке.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class DummyHeal : MonoBehaviour
    {
        [Tooltip("Через сколько секунд после удара здоровье возвращается.")]
        [SerializeField] private float after = 0.5f;

        private Health health;
        private float healAt;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
        }

        /// <summary>
        /// Встать рядом с живым героем, уже в игре.
        ///
        /// Ставить манекен в сцене оказалось бесполезно: игра грузит
        /// сохранение, и герой появляется совсем в другом месте — а кукла
        /// остаётся там, где он стоял в редакторе, то есть над землёй.
        /// Три попытки подряд я чинил высоту, хотя ломалось не место, а
        /// момент: я считал позицию раньше, чем герой встал на своё.
        ///
        /// Здесь же мир уже загружен и герой на месте, поэтому и земля
        /// находится честно — лучом вниз, мимо собственных коллайдеров.
        /// </summary>
        private void Start()
        {
            var hero = Object.FindFirstObjectByType<IsoRPG.Player.PlayerInputRouter>();
            if (hero == null) return;

            Vector3 at = hero.transform.position + hero.transform.forward * 3f;

            transform.position = new Vector3(at.x, hero.transform.position.y, at.z);
            transform.rotation = Quaternion.LookRotation(-hero.transform.forward);

            settleUntil = Time.time + 2f;
        }

        /// <summary>
        /// Досаживать манекен на землю первые две секунды.
        ///
        /// Один замер в Start не годится: герой в этот миг ещё не осел —
        /// капсула опускает его несколько кадров после загрузки сохранения,
        /// и высота, взятая у него сразу, оказывается на ладонь выше земли.
        /// Павлон 04.09.2026: «персонаж на земле, а манекен над травой».
        ///
        /// Две секунды и раз в четверть секунды: это разовая посадка, а не
        /// работа в кадре — после неё компонент к земле не возвращается.
        /// </summary>
        private float settleUntil;
        private float nextSettle;

        private void Settle()
        {
            if (Time.time > settleUntil || Time.time < nextSettle) return;

            nextSettle = Time.time + 0.25f;

            var own = GetComponentsInChildren<Collider>(true);
            foreach (var c in own) c.enabled = false;

            bool found = Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
                                         out var hit, 8f, ~0, QueryTriggerInteraction.Ignore);

            foreach (var c in own) c.enabled = true;

            if (found) transform.position = hit.point;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(int amount, GameObject source) => healAt = Time.time + after;

        private void Update()
        {
            Settle();

            if (healAt <= 0f || Time.time < healAt) return;

            healAt = 0f;

            if (health != null) health.Heal(health.Max);
        }
    }
}
