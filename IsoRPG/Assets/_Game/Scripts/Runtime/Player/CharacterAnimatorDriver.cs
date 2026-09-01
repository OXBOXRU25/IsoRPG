using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Player
{
    /// <summary>Каким жестом играется способность.</summary>
    public enum CastKind
    {
        /// <summary>Направленный удар по одной цели.</summary>
        Attack,

        /// <summary>Удар по площади.</summary>
        Area,

        /// <summary>Усиление себя.</summary>
        Buff,
    }

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

        /// <summary>Множитель скорости приземления: на бегу оно должно пройти быстрее.</summary>
        private static readonly int LandSpeedHash = Animator.StringToHash("LandSpeed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int StealthKillHash = Animator.StringToHash("StealthKill");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int EatingHash = Animator.StringToHash("Eating");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        // Оживление боя: номер удара в серии, вздрагивание, уклонение и
        // жесты способностей. Параметров может не быть у старых контроллеров
        // (у зверей их нет вовсе) — поэтому всё через проверку Has ниже.
        private static readonly int AttackVariantHash = Animator.StringToHash("AttackVariant");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        private static readonly int CastAttackHash = Animator.StringToHash("CastAttack");
        private static readonly int CastAOEHash = Animator.StringToHash("CastAOE");
        private static readonly int CastBuffHash = Animator.StringToHash("CastBuff");
        private static readonly int SneakingHash = Animator.StringToHash("Sneaking");
        private static readonly int AirborneHash = Animator.StringToHash("InAir");

        [Tooltip("Аниматор персонажа. Обычно на дочерней модели.")]
        [SerializeField] private Animator animator;

        [Tooltip("Сглаживание разгона. Без него при старте ноги дёргаются, потому что агент меняет скорость рывком.")]
        [SerializeField] private float speedSmooth = 8f;

        /// <summary>Здоровье владельца — источник вздрагиваний.</summary>
        private IsoRPG.Combat.Health health;

        /// <summary>Скрытность владельца — меняет походку.</summary>
        private IsoRPG.Combat.StealthState stealth;

        [Tooltip("Сглаживание остановки — намеренно резче разгона. Пока сглаженная скорость сползает вниз, аниматор считает персонажа бегущим и не даёт начаться удару.")]
        [SerializeField] private float stopSmooth = 22f;

        [Tooltip("Ниже этой скорости считаем, что персонаж стоит. Спасает от подрагивания стойки, когда агент доезжает последние сантиметры.")]
        [SerializeField] private float idleThreshold = 0.1f;

        private NavMeshAgent agent;
        private PlayerMotor motor;
        private float smoothedSpeed;
        private KeyboardMove keys;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            motor = GetComponent<PlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            health = GetComponent<IsoRPG.Combat.Health>();
        }

        /// <summary>
        /// Вздрагивание вешаем здесь, а не в бою.
        ///
        /// Бьющих много — ближний бой, стрелы, ловушки, яд, — и подписывать
        /// каждого значит однажды забыть одного. Здоровье же знает про урон
        /// в единственном месте, откуда бы он ни пришёл.
        /// </summary>
        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;

            // Скрытность меняет саму походку, поэтому слушаем её здесь же.
            if (stealth == null) stealth = GetComponent<IsoRPG.Combat.StealthState>();

            if (stealth != null)
            {
                stealth.StealthChanged += SetSneaking;
                SetSneaking(stealth.IsStealthed);
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (stealth != null) stealth.StealthChanged -= SetSneaking;
        }

        private void OnDamaged(int amount, GameObject source)
        {
            // Мёртвые не вздрагивают: поверх падения это выглядит как
            // судорога, а не как удар.
            if (health != null && !health.IsAlive) return;

            PlayHit();
        }

        private void Update()
        {
            if (animator == null) return;

            // Берём фактическую скорость, а не желаемую: при обходе препятствия
            // и на поворотах она отличается, и анимация должна следовать за тем,
            // что происходит на экране, а не за намерением.
            // С физической капсулой скорость спрашиваем у неё: агент
            // при выключенном updatePosition своей velocity не ведёт,
            // и герой ехал бы по земле в позе стоя.
            float speed = motor != null ? motor.Speed : agent.velocity.magnitude;

            // Клавиши двигают героя мимо пути агента, и его собственная
            // скорость при этом остаётся нулевой. Спрашиваем у того, кто
            // ведёт, иначе на WASD персонаж скользит по земле стоя.
            if (keys == null) keys = GetComponent<KeyboardMove>();
            if (keys != null && keys.IsSteering) speed = keys.Speed;
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

            // Приземление на бегу играем быстрее, а не обрезаем.
            //
            // Клип рассчитан на остановку: присед и разгибание занимают
            // почти секунду, и всё это время герой едет по земле. Обрезать
            // нельзя — пропадает разгибание. Ускоряем: обе фазы на месте,
            // просто проходят вдвое быстрее.
            animator.SetFloat(LandSpeedHash, smoothedSpeed > 0.15f ? 2f : 1f);
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
        /// <summary>
        /// Удар номер N в серии. Ноль — «как раньше», одним состоянием.
        ///
        /// Номер выставляем ДО триггера: переход читает оба условия в один
        /// кадр, и при обратном порядке первый удар всегда уходил бы в
        /// вариант из прошлого замаха.
        /// </summary>
        public void PlayAttack(int variant)
        {
            if (animator == null) return;

            if (variant > 0 && Has(AttackVariantHash, AnimatorControllerParameterType.Int))
                animator.SetInteger(AttackVariantHash, variant);

            animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// В воздухе или нет. Держит фазу зависания, пока герой летит.
        ///
        /// Без флага зависание кончалось по длине клипа, а не по длине
        /// прыжка — и герой в воздухе начинал перебирать ногами.
        /// </summary>
        public void SetAirborne(bool airborne)
        {
            if (animator != null && Has(AirborneHash, AnimatorControllerParameterType.Bool))
                animator.SetBool(AirborneHash, airborne);
        }

        /// <summary>Крадётся или нет. Скрытность — состояние, а не действие.</summary>
        public void SetSneaking(bool sneaking)
        {
            if (animator != null && Has(SneakingHash, AnimatorControllerParameterType.Bool))
                animator.SetBool(SneakingHash, sneaking);
        }

        /// <summary>Вздрогнуть от попадания.</summary>
        public void PlayHit()
        {
            if (animator != null && Has(HitHash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(HitHash);
        }

        /// <summary>Уклониться.</summary>
        public void PlayDodge()
        {
            if (animator != null && Has(DodgeHash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(DodgeHash);
        }

        /// <summary>
        /// Жест способности. Разные по смыслу: направленный удар, площадь,
        /// усиление. Игрок должен видеть, что нажал разные кнопки.
        /// </summary>
        public void PlayCast(CastKind kind)
        {
            if (animator == null) return;

            int hash = kind switch
            {
                CastKind.Area => CastAOEHash,
                CastKind.Buff => CastBuffHash,
                _ => CastAttackHash,
            };

            if (Has(hash, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hash);
        }

        /// <summary>
        /// Есть ли такой параметр у контроллера.
        ///
        /// Проверка обязательна: этот же компонент висит на волках, кабанах и
        /// лошади, а у их контроллеров боевых параметров нет. Unity на
        /// отсутствующий параметр ругается в консоль каждый кадр — и лог
        /// становится нечитаемым ровно тогда, когда он нужнее всего.
        /// </summary>
        private bool Has(int hash, AnimatorControllerParameterType type)
        {
            if (animator.runtimeAnimatorController == null) return false;

            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == type) return true;

            return false;
        }

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
