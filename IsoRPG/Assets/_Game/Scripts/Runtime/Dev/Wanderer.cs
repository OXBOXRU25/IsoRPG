using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Dev
{
    /// <summary>
    /// Бродит по округе: пошёл, постоял, пошёл дальше.
    ///
    /// Нужно для смотрин. Персонаж, стоящий столбом, ничего не говорит о
    /// том, годится ли он: половина впечатления от существа — как оно
    /// двигается. Стоячая фигура выглядит одинаково хорошо у любого набора,
    /// а идущая сразу показывает и походку, и пропорции, и то, не скользят
    /// ли ноги по земле.
    ///
    /// Это витринное поведение, а не игровое: ни боя, ни погони, ни реакции
    /// на игрока. Настоящий мозг монстра — отдельная работа, и делать её до
    /// того, как решено «берём», значит рисковать выкинуть её целиком.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Wanderer : MonoBehaviour
    {
        [Tooltip("Как далеко от места появления может отойти.")]
        public float Range = 12f;

        [Tooltip("Сколько стоит на месте между переходами, секунды.")]
        public Vector2 Rest = new Vector2(1.5f, 4f);

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 home;
        private float waitUntil;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();

            home = transform.position;

            // Разводим по времени: если все двинутся одновременно, толпа
            // читается как марш, а не как жизнь.
            waitUntil = Time.time + Random.Range(0f, Rest.y);
        }

        private void Update()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            if (animator != null)
                animator.SetFloat(SpeedHash, agent.velocity.magnitude, 0.12f, Time.deltaTime);

            if (agent.pathPending) return;

            bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.15f;

            if (!arrived) return;

            if (Time.time < waitUntil) return;

            // Новая точка в круге вокруг дома. Через SamplePosition, а не
            // напрямую: точка в кустах или за стеной заставила бы агента
            // упереться и стоять до конца времён.
            Vector2 offset = Random.insideUnitCircle * Range;
            Vector3 wanted = home + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(wanted, out var hit, 4f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            waitUntil = Time.time + Random.Range(Rest.x, Rest.y);
        }
    }
}
