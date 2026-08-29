using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Не даёт телам залезать друг в друга.
    ///
    /// Расталкивание, встроенное в навигацию, работает только пока агент
    /// идёт по пути. А в бою происходит ровно обратное: монстр дошёл,
    /// остановился и бьёт — и с этого мгновения ничто их не разводит.
    /// Плюс ходьба на клавишах двигает героя напрямую, мимо навигации, и
    /// туда обход не заглядывает вовсе. В итоге герой и упырь стоят в одной
    /// точке, просвечивая друг сквозь друга.
    ///
    /// Здесь — простое телесное разведение: если чужой центр ближе суммы
    /// радиусов, обоих мягко отодвигает. Мягко и есть ключевое слово:
    /// жёсткий выталкивающий импульс дёргает камеру и сбивает удары, а
    /// плавное расхождение читается как «потеснились».
    ///
    /// Радиус берётся у навигационного агента, а он подогнан под фактический
    /// размер модели — иначе крупный упырь с радиусом мелкого скелета всё
    /// равно влезал бы в героя по пояс.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BodySpace : MonoBehaviour
    {
        [Tooltip("Насколько быстро тела расходятся, метров в секунду.")]
        public float PushSpeed = 2.2f;

        [Tooltip("Сколько соседей проверяем за раз.")]
        public int Neighbours = 8;

        private NavMeshAgent agent;
        private readonly Collider[] found = new Collider[16];

        /// <summary>
        /// Насколько охотно уступает дорогу.
        ///
        /// В WoW при столкновении отходит ПРОТИВНИК, а не игрок: героя
        /// никто не двигает, иначе теряется ощущение, что миром управляешь
        /// ты. Поэтому у игрока вес почти нулевой — он стоит, — а монстр
        /// отступает на шаг и продолжает бить с приличного расстояния.
        /// </summary>
        private float yielding = 1f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // Игрока узнаём по вводу: он единственный, кем управляют.
            bool isPlayer = GetComponent<IsoRPG.Player.PlayerInputRouter>() != null;
            yielding = isPlayer ? 0.15f : 1f;

            Fit();
        }

        /// <summary>
        /// Подгоняет радиус и высоту агента под фактическую модель.
        ///
        /// В сборщике они выписаны числами — 0.4 герою, 0.45 монстрам, — но
        /// монстры приходят разного размера, и у крупных радиус оказывался
        /// вдвое меньше туши. Замер по нарисованным границам знает правду о
        /// каждом.
        /// </summary>
        private void Fit()
        {
            if (agent == null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            // Половина ширины — но с запасом вниз: плечи и оружие в габариты
            // входят, а тесниться из-за них персонажи не должны.
            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.3f, 1.2f);

            agent.radius = radius;
            agent.height = Mathf.Clamp(bounds.size.y, 1f, 4f);
        }

        private void LateUpdate()
        {
            if (agent == null || !agent.isActiveAndEnabled) return;

            Vector3 self = transform.position;

            int count = Physics.OverlapSphereNonAlloc(
                self, agent.radius * 2.5f, found, ~0, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            int touched = 0;

            for (int i = 0; i < count && touched < Neighbours; i++)
            {
                var other = found[i];
                if (other == null || other.transform == transform) continue;

                var otherAgent = other.GetComponentInParent<NavMeshAgent>();
                if (otherAgent == null || otherAgent == agent) continue;

                Vector3 away = self - otherAgent.transform.position;
                away.y = 0f;

                float gap = agent.radius + otherAgent.radius;
                float distance = away.magnitude;

                // Совпали точно — расходимся в любую сторону, иначе
                // направление не вычислить и они останутся слипшимися.
                if (distance < 0.001f)
                {
                    away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
                    distance = 0.001f;
                }

                if (distance >= gap) continue;

                push += away.normalized * (gap - distance);
                touched++;
            }

            if (touched == 0) return;

            Vector3 step = Vector3.ClampMagnitude(push, 1f) * PushSpeed * yielding * Time.deltaTime;

            // Через агента, а не присваиванием позиции: иначе тело сойдёт с
            // навигационной сетки и провалится сквозь помост или стену.
            if (agent.isOnNavMesh) agent.Move(step);
        }
    }
}
