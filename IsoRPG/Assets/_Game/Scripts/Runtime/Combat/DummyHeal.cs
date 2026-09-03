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

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(int amount, GameObject source) => healAt = Time.time + after;

        private void Update()
        {
            if (healAt <= 0f || Time.time < healAt) return;

            healAt = 0f;

            if (health != null) health.Heal(health.Max);
        }
    }
}
