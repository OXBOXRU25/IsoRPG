using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Поведение монстра: заметить, догнать, драться, вернуться домой.
    ///
    /// Сам бой ведёт тот же MeleeCombatant, что и у игрока — здесь только
    /// решение, кого считать целью. Общий боевой компонент означает, что
    /// правила одинаковы для всех: никаких «монстр бьёт по-другому».
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    [RequireComponent(typeof(Health))]
    public sealed class MonsterBrain : MonoBehaviour
    {
        [Header("Агрессия")]
        [Tooltip("На каком расстоянии монстр замечает игрока.")]
        [SerializeField] private float aggroRadius = 7f;

        [Tooltip("Насколько далеко от дома монстр готов гнаться. Дальше — разворачивается и уходит обратно.")]
        [SerializeField] private float leashRange = 16f;

        [Tooltip("Как часто осматриваться, в секундах. Каждый кадр не нужно — это лишняя нагрузка при десятках монстров.")]
        [SerializeField] private float scanInterval = 0.25f;

        [Header("Возвращение")]
        [Tooltip("Восстанавливать здоровье, вернувшись домой. Как в WoW: сорвался с поводка — лечится.")]
        [SerializeField] private bool healOnReturn = true;

        private TargetSelector targets;
        private Health health;
        private NavMeshAgent agent;
        private MeleeCombatant combat;
        private Targetable self;

        private Vector3 homePosition;
        private float nextScanTime;
        private bool returningHome;

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            combat = GetComponent<MeleeCombatant>();
            self = GetComponent<Targetable>();

            homePosition = transform.position;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            if (!health.IsAlive) return;

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + scanInterval;
                Think();
            }

            if (returningHome) StepHome();
        }

        private void Think()
        {
            float fromHome = Vector3.Distance(transform.position, homePosition);

            // Ушли слишком далеко — бросаем цель и идём домой. Без поводка
            // монстры растаскиваются по всей карте и собираются толпой там,
            // куда убежал игрок.
            if (fromHome > leashRange)
            {
                GoHome();
                return;
            }

            if (returningHome) return;

            var victim = targets.Current;

            // Цель ещё жива и рядом — держимся за неё.
            if (victim != null && victim.IsAlive
                && Vector3.Distance(transform.position, victim.transform.position) <= aggroRadius * 1.6f)
            {
                return;
            }

            var found = FindNearestEnemy();
            if (found != null)
            {
                targets.Select(found);
                if (combat != null) combat.EngageTarget();
            }
            else if (victim != null)
            {
                targets.Clear();
                GoHome();
            }
        }

        private Targetable FindNearestEnemy()
        {
            var hits = Physics.OverlapSphere(transform.position, aggroRadius);

            Targetable best = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var candidate = hit.GetComponentInParent<Targetable>();
                if (candidate == null || candidate == self) continue;
                if (!candidate.IsAlive) continue;
                if (!candidate.IsHostileTo(targets.OwnFaction)) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnDamaged(int amount, GameObject source)
        {
            if (source == null || !health.IsAlive) return;

            // Ударили — разворачиваемся к обидчику, даже если он вне радиуса
            // обзора. Бить кого-то, кто тебя не замечает, было бы странно.
            var attacker = source.GetComponentInParent<Targetable>();
            if (attacker == null || !attacker.IsHostileTo(targets.OwnFaction)) return;

            returningHome = false;
            targets.Select(attacker);
            if (combat != null) combat.EngageTarget();
        }

        private void GoHome()
        {
            targets.Clear();
            returningHome = true;
        }

        private void StepHome()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            if (Vector3.Distance(transform.position, homePosition) < 0.4f)
            {
                returningHome = false;
                agent.ResetPath();

                if (healOnReturn) health.Heal(health.Max);
                return;
            }

            agent.SetDestination(homePosition);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, aggroRadius);

            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.25f);
            Vector3 home = Application.isPlaying ? homePosition : transform.position;
            Gizmos.DrawWireSphere(home, leashRange);
        }
#endif
    }
}
