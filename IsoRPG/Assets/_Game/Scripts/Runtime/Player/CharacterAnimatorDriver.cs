using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Player
{
    /// <summary>
    /// Связывает навигацию с анимацией: передаёт в контроллер текущую скорость,
    /// по которой дерево смешивания выбирает стойку, шаг или бег.
    ///
    /// Ключевая идея: движением командует NavMeshAgent, а анимация лишь
    /// отражает результат. Обратный порядок (когда двигает анимация) даёт
    /// скольжение и рассинхрон, и именно ради этого при скачивании с Mixamo
    /// ставится галочка In Place.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int StealthKillHash = Animator.StringToHash("StealthKill");
        private static readonly int DeadHash = Animator.StringToHash("Dead");

        [Tooltip("Аниматор персонажа. Обычно на дочерней модели.")]
        [SerializeField] private Animator animator;

        [Tooltip("Сглаживание скорости. Без него при старте и остановке ноги дёргаются, потому что агент меняет скорость рывком.")]
        [SerializeField] private float speedSmooth = 8f;

        [Tooltip("Ниже этой скорости считаем, что персонаж стоит. Спасает от подрагивания стойки, когда агент доезжает последние сантиметры.")]
        [SerializeField] private float idleThreshold = 0.1f;

        private NavMeshAgent agent;
        private float smoothedSpeed;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (animator == null) return;

            // Берём фактическую скорость, а не желаемую: при обходе препятствия
            // и на поворотах она отличается, и анимация должна следовать за тем,
            // что происходит на экране, а не за намерением.
            float speed = agent.velocity.magnitude;
            if (speed < idleThreshold) speed = 0f;

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-speedSmooth * Time.deltaTime));
            animator.SetFloat(SpeedHash, smoothedSpeed);
        }

        /// <summary>Обычная атака. Вызывается боевой системой.</summary>
        public void PlayAttack()
        {
            if (animator != null) animator.SetTrigger(AttackHash);
        }

        /// <summary>Удар из скрытности — тот самый эффектный добивающий.</summary>
        public void PlayStealthKill()
        {
            if (animator != null) animator.SetTrigger(StealthKillHash);
        }

        /// <summary>Смерть. Флаг, а не разовый сигнал: из этого состояния не выходят сами.</summary>
        public void SetDead(bool dead)
        {
            if (animator != null) animator.SetBool(DeadHash, dead);
        }
    }
}
