using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Player;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Ближний бой: подойти к выбранной цели и бить её автоатакой.
    ///
    /// Работает по вовской схеме: цель выбрана — персонаж сам держит
    /// дистанцию и бьёт по кулдауну, не требуя клика на каждый удар.
    /// Игрок в это время думает о способностях, а не о кликанье.
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    public sealed class MeleeCombatant : MonoBehaviour
    {
        [Header("Удар")]
        [Tooltip("Дальность удара сверх радиусов тел. Кинжал — короткая.")]
        [SerializeField] private float reach = 0.9f;

        [Tooltip("Секунд между ударами. Скорость оружия.")]
        [SerializeField] private float attackInterval = 2f;

        [Tooltip("Задержка урона от начала замаха — момент касания в анимации. Без неё урон срабатывает раньше, чем клинок дошёл, и это видно.")]
        [SerializeField] private float impactDelay = 0.5f;

        [SerializeField] private int damage = 12;

        [Header("Преследование")]
        [Tooltip("Догонять ли цель, если она отошла.")]
        [SerializeField] private bool chaseTarget = true;

        [Tooltip("Насколько цель должна отойти, чтобы мы тронулись за ней. Без запаса персонаж дёргается на месте.")]
        [SerializeField] private float chaseTolerance = 0.35f;

        [Header("Разворот")]
        [SerializeField] private float turnSpeed = 720f;

        private TargetSelector targets;
        private NavMeshAgent agent;
        private ClickToMoveController movement;
        private CharacterAnimatorDriver animDriver;
        private Targetable self;

        private float nextAttackTime;
        private float pendingImpactTime = -1f;
        private Targetable pendingVictim;

        // Игрок приказал идти. Пока приказ в силе, за целью не бежим:
        // ручное управление всегда главнее автоматики, иначе персонаж
        // возвращается к врагу, хотя его явно уводят.
        private bool manualMoveOverride;

        /// <summary>Отменить преследование. Зовётся, когда игрок кликнул по земле.</summary>
        public void CancelChase()
        {
            manualMoveOverride = true;
        }

        /// <summary>Взяться за цель. Зовётся при клике по врагу — снимает запрет на погоню.</summary>
        public void EngageTarget()
        {
            manualMoveOverride = false;
        }

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            agent = GetComponent<NavMeshAgent>();
            movement = GetComponent<ClickToMoveController>();
            animDriver = GetComponent<CharacterAnimatorDriver>();
            self = GetComponent<Targetable>();
        }

        private void Update()
        {
            ResolvePendingImpact();

            if (!targets.HasHostileTarget) return;

            var target = targets.Current;
            float distance = Vector3.Distance(transform.position, target.transform.position);
            float attackDistance = AttackDistanceTo(target);

            if (distance > attackDistance + chaseTolerance)
            {
                // Пока игрок ведёт персонажа сам — не вмешиваемся. Приказ
                // считается выполненным, когда персонаж остановился.
                if (manualMoveOverride)
                {
                    if (movement != null && !movement.IsMoving) manualMoveOverride = false;
                    return;
                }

                if (chaseTarget) ChaseTo(target, attackDistance);
                return;
            }

            // Дошли до цели сами — ручной приказ больше не действует.
            manualMoveOverride = false;

            StopMoving();
            FaceTarget(target);

            if (Time.time >= nextAttackTime) Attack(target);
        }

        private float AttackDistanceTo(Targetable target)
        {
            float ownRadius = self != null ? self.BodyRadius : 0.5f;
            return reach + ownRadius + target.BodyRadius;
        }

        private void ChaseTo(Targetable target, float attackDistance)
        {
            if (movement == null) return;

            // Идём не в саму цель, а в точку перед ней: иначе агент упирается
            // в её тело, толкается и не может доехать до «места назначения».
            Vector3 toSelf = (transform.position - target.transform.position);
            toSelf.y = 0f;

            Vector3 direction = toSelf.sqrMagnitude > 0.001f
                ? toSelf.normalized
                : transform.forward;

            Vector3 standPoint = target.transform.position + direction * (attackDistance * 0.8f);
            movement.MoveTo(standPoint);
        }

        private void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
        }

        private void FaceTarget(Targetable target)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }

        private void Attack(Targetable target)
        {
            nextAttackTime = Time.time + attackInterval;

            if (animDriver != null) animDriver.PlayAttack();

            // Урон отложен: он должен совпасть с моментом, когда клинок
            // достаёт цель, а не с началом замаха.
            pendingVictim = target;
            pendingImpactTime = Time.time + impactDelay;
        }

        private void ResolvePendingImpact()
        {
            if (pendingImpactTime < 0f || Time.time < pendingImpactTime) return;

            pendingImpactTime = -1f;

            // Цель могла умереть или уйти, пока летел удар — это нормально,
            // просто промахиваемся.
            if (pendingVictim == null || !pendingVictim.IsAlive) return;

            float distance = Vector3.Distance(transform.position, pendingVictim.transform.position);
            if (distance > AttackDistanceTo(pendingVictim) + 1f) return;

            if (pendingVictim.Health != null)
                pendingVictim.Health.TakeDamage(damage, gameObject);

            pendingVictim = null;
        }
    }
}

