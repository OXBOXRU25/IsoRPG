using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Player;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Боец дальнего боя, который держит дистанцию.
    ///
    /// Смысл не в том, чтобы бить издалека, а в том, чтобы вести себя иначе.
    /// Все наши противники до сих пор делали одно и то же: бежали и били.
    /// Лучник, который отходит между выстрелами, требует от игрока другого —
    /// догонять, разрывать дистанцию рывком, выбирать момент.
    ///
    /// Ритм такой:
    ///   • откат готов и цель в поле обстрела — останавливается и стреляет;
    ///   • во время выстрела стоит: стрелять на бегу он не умеет, и это
    ///     единственное окно, когда его можно догнать;
    ///   • на откате отходит, если игрок ближе комфортной дистанции;
    ///   • цель слишком далеко — подходит.
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    public sealed class RangedCombatant : MonoBehaviour, ICombatant
    {
        [Header("Дистанции")]
        [Tooltip("Дальше этого не стреляет — подходит.")]
        [SerializeField] private float attackRange = 9f;

        [Tooltip("Ближе этого чувствует себя неуютно и отходит между выстрелами.")]
        [SerializeField] private float comfortRange = 5f;

        [Tooltip("Насколько далеко отбегает за один отход.")]
        [SerializeField] private float retreatStep = 3.5f;

        [Header("Стрельба")]
        [Tooltip("Секунд между выстрелами.")]
        [SerializeField] private float attackInterval = 2.6f;

        [Tooltip("Задержка от начала анимации до схода стрелы.")]
        [SerializeField] private float releaseDelay = 0.55f;

        [Tooltip("Сколько стоит неподвижно, отправив стрелу. Это окно, в котором его догоняют.")]
        [SerializeField] private float rootedAfterShot = 0.35f;

        [SerializeField] private int damage = 9;
        [SerializeField] private float projectileSpeed = 14f;

        [Tooltip("Модель снаряда. Пусто — стрела будет невидимой.")]
        [SerializeField] private GameObject projectileModel;

        [Header("Броски")]
        [SerializeField] private float critChance = CombatMath.DefaultCritChance;
        [SerializeField] private float critMultiplier = CombatMath.DefaultCritMultiplier;
        [SerializeField] private float missChance = CombatMath.DefaultMissChance;
        [SerializeField] private float missMultiplier = CombatMath.DefaultMissMultiplier;

        [SerializeField] private float turnSpeed = 720f;

        private TargetSelector targets;
        private NavMeshAgent agent;
        private CharacterAnimatorDriver animDriver;
        private Targetable self;
        private StunReceiver stun;

        private float nextAttackTime;
        private float rootedUntil;
        private float pendingReleaseTime = -1f;
        private Targetable pendingVictim;

        public void Setup(GameObject projectile) => projectileModel = projectile;

        /// <summary>Мозг просит заняться выбранной целью.</summary>
        public void EngageTarget()
        {
            // Ничего специального: следующий же кадр Update разберётся сам.
            // Метод существует ради общего интерфейса с ближним боем.
        }

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            agent = GetComponent<NavMeshAgent>();
            animDriver = GetComponent<CharacterAnimatorDriver>();
            self = GetComponent<Targetable>();
            stun = GetComponent<StunReceiver>();
        }

        private void Update()
        {
            ResolvePendingShot();

            if (stun != null && stun.IsStunned) return;
            if (!targets.HasHostileTarget) return;

            var target = targets.Current;
            float distance = Vector3.Distance(transform.position, target.transform.position);

            // Пока стоим после выстрела — не двигаемся вообще. Это честное
            // окно уязвимости, ради него всё и затевалось.
            if (Time.time < rootedUntil)
            {
                Stop();
                FaceTarget(target);
                return;
            }

            // Стрелять сквозь стену нельзя. Если цель перекрыта — идём к ней
            // напрямую: навигационный агент обойдёт препятствие сам, и как
            // только цель откроется, стрелок остановится и выстрелит.
            bool canSee = HasLineOfFire(target);

            if (distance > attackRange || !canSee)
            {
                if (canSee) Approach(target);
                else MoveToSee(target);

                FaceMovement();
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                FaceTarget(target);
                Shoot(target);
                return;
            }

            // Откат не вышел, а игрок близко — отходим. Отходим именно на
            // откате: стрелок не убегает вечно, он выгадывает расстояние
            // ровно до следующего выстрела.
            //
            // На бегу смотрим ПО ХОДУ ДВИЖЕНИЯ, а не на цель. Пятиться,
            // не отрывая глаз от врага, можно шаг-другой; на трёх метрах это
            // читается как поломка анимации, а не как осторожность.
            if (distance < comfortRange)
            {
                Retreat(target);
                FaceMovement();
            }
            else
            {
                Stop();
                FaceTarget(target);
            }
        }

        // ------------------------------------------------------------------

        private void Shoot(Targetable target)
        {
            nextAttackTime = Time.time + attackInterval;
            rootedUntil = Time.time + releaseDelay + rootedAfterShot;

            Stop();

            if (animDriver != null) animDriver.PlayAttack();

            // Стрела сходит не в начале анимации, а когда рука отпускает
            // тетиву. Иначе выстрел «опережает» движение и читается фальшиво.
            pendingVictim = target;
            pendingReleaseTime = Time.time + releaseDelay;
        }

        private void ResolvePendingShot()
        {
            if (pendingReleaseTime < 0f || Time.time < pendingReleaseTime) return;

            pendingReleaseTime = -1f;

            if (pendingVictim == null || !pendingVictim.IsAlive)
            {
                pendingVictim = null;
                return;
            }

            int rolled = CombatMath.Roll(damage, critChance, critMultiplier,
                                         missChance, missMultiplier, out HitResult result);

            Vector3 from = transform.position + Vector3.up * 1.2f;
            Projectile.Spawn(projectileModel, from, pendingVictim, gameObject,
                             rolled, result, projectileSpeed);

            pendingVictim = null;
        }

        /// <summary>
        /// Видит ли стрелок цель, или между ними препятствие.
        ///
        /// Тела существ выстрел НЕ блокируют — ни свои, ни чужие. Иначе
        /// лучник замолкал бы каждый раз, когда перед ним пробегает союзник,
        /// и выглядело бы это как поломка, а не как тактика. Блокируют только
        /// препятствия: у них нет боевого компонента, по нему и различаем.
        /// </summary>
        private bool HasLineOfFire(Targetable target)
        {
            Vector3 from = transform.position + Vector3.up * 1.2f;
            Vector3 to = target.transform.position + Vector3.up * 1.0f;

            Vector3 direction = to - from;
            float distance = direction.magnitude;

            if (distance < 0.01f) return true;

            // Триггеры игнорируем: коллайдер игрока сделан триггером, чтобы
            // клик по земле проходил сквозь него, и стеной он быть не может.
            var hits = Physics.RaycastAll(from, direction.normalized, distance,
                                          ~0, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                // Свой собственный коллайдер и коллайдер цели пропускаем.
                if (hit.collider.transform.IsChildOf(transform)) continue;

                var other = hit.collider.GetComponentInParent<Targetable>();
                if (other != null) continue;   // живое тело — не стена

                return false;
            }

            return true;
        }

        /// <summary>
        /// Идти к цели, пока она не откроется.
        ///
        /// В отличие от подхода на дистанцию, тут идём прямо на цель: точка
        /// «в пяти метрах от игрока» может оказаться по ТУ ЖЕ сторону стены,
        /// и стрелок встанет там, продолжая её не видеть.
        /// </summary>
        private void MoveToSee(Targetable target)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.SetDestination(target.transform.position);
        }

        private void Approach(Targetable target)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            agent.isStopped = false;

            // Идём не вплотную, а на комфортную дистанцию: подойти ближе
            // значило бы тут же начать отходить.
            Vector3 direction = (transform.position - target.transform.position).normalized;
            Vector3 wanted = target.transform.position + direction * comfortRange;

            if (NavMesh.SamplePosition(wanted, out var hit, comfortRange, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        /// <summary>
        /// Отход от цели. Точку ищем по навигационной сетке, а не просто
        /// пятимся: упершись в стену, стрелок иначе замирает вплотную к
        /// игроку и превращается в мишень.
        /// </summary>
        private void Retreat(Targetable target)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            Vector3 away = (transform.position - target.transform.position).normalized;
            Vector3 wanted = transform.position + away * retreatStep;

            if (NavMesh.SamplePosition(wanted, out var hit, retreatStep, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return;
            }

            // Отступать некуда — прижали к стене. Тогда просто стоим и ждём
            // выстрела: это честное следствие того, что игрок загнал его в
            // угол, а не ошибка.
            Stop();
        }

        private void Stop()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        /// <summary>
        /// Развернуться туда, куда бежим.
        ///
        /// Берём фактическую скорость агента, а не желаемое направление:
        /// обходя препятствие, персонаж какое-то время движется вбок, и
        /// поворот на «куда хотел» смотрелся бы боком к собственному пути.
        /// </summary>
        private void FaceMovement()
        {
            if (agent == null) return;

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;

            // Почти стоим — не крутимся. Иначе на остановке персонаж дёргано
            // доворачивается по остаточной скорости.
            if (velocity.sqrMagnitude < 0.05f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(velocity.normalized),
                turnSpeed * Time.deltaTime);
        }

        private void FaceTarget(Targetable target)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                turnSpeed * Time.deltaTime);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.4f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = new Color(0.3f, 0.7f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, comfortRange);
        }
#endif
    }
}
