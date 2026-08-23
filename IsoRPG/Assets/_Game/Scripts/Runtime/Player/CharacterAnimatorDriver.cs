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
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int StealthKillHash = Animator.StringToHash("StealthKill");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int EatingHash = Animator.StringToHash("Eating");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        [Tooltip("Аниматор персонажа. Обычно на дочерней модели.")]
        [SerializeField] private Animator animator;

        [Tooltip("Сглаживание разгона. Без него при старте ноги дёргаются, потому что агент меняет скорость рывком.")]
        [SerializeField] private float speedSmooth = 8f;

        [Tooltip("Сглаживание остановки — намеренно резче разгона. Пока сглаженная скорость сползает вниз, аниматор считает персонажа бегущим и не даёт начаться удару.")]
        [SerializeField] private float stopSmooth = 22f;

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

            // Разгон сглаживаем мягко, торможение — резко. Асимметрия здесь не
            // косметика: пока «скорость» медленно сползает к нулю, аниматор
            // считает персонажа бегущим и отменяет начатый удар.
            float rate = speed < smoothedSpeed ? stopSmooth : speedSmooth;

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-rate * Time.deltaTime));

            // Дожимаем до нуля: остаточные сотые доли всё равно читаются
            // деревом смешивания как «чуть-чуть идёт», и стойка подрагивает.
            if (speed <= 0f && smoothedSpeed < 0.05f) smoothedSpeed = 0f;

            animator.SetFloat(SpeedHash, smoothedSpeed);
        }

        /// <summary>
        /// Подогнать длительность анимации удара под ритм боя.
        ///
        /// Скорость атаки — характеристика оружия, а не длина скачанного
        /// клипа. Поэтому клип растягивается или поджимается так, чтобы ровно
        /// заполнить интервал между ударами: без пауз стояния и без наложения
        /// одного замаха на другой.
        /// </summary>
        public void SetActionDuration(float seconds)
        {
            if (animator == null || seconds <= 0.01f) return;

            // В контроллере длительность действия задана постоянной, и скорость
            // считается относительно неё.
            const float baseDuration = 1.3f;

            animator.SetFloat(AttackSpeedHash, baseDuration / seconds);
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

        /// <summary>Прыжок. Разовый сигнал: состояние само возвращается в движение.</summary>
        public void PlayJump()
        {
            if (animator != null) animator.SetTrigger(JumpHash);
        }

        /// <summary>Еда: персонаж садится на землю и встаёт, когда закончил.</summary>
        public void SetEating(bool eating)
        {
            if (animator != null) animator.SetBool(EatingHash, eating);
        }
    }
}
