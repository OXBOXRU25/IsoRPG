using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Отшаг моба, в котором стоит игрок. Не расталкивание.
    ///
    /// Переписано 01.09.2026 по указанию Павла: «убери механику отталкивания
    /// вообще — я должен заходить в их текстуры, но мобы делают отшаг в
    /// сторону или назад; НПС и лошадей это не касается, через них я просто
    /// прохожу». Прежняя версия непрерывно разводила тела, и это ощущалось
    /// как сопротивление управлению: герой упирался в невидимую подушку
    /// вокруг каждого существа.
    ///
    /// Вовская схема, которую воспроизводим:
    ///
    /// - тела не сталкиваются вообще: сквозь любого можно пройти насквозь;
    /// - мирный моб, НПС и лошадь не двигаются ни при каких условиях —
    ///   через них проходишь как через дым;
    /// - но моб, который с тобой дерётся, не станет стоять внутри тебя: если
    ///   вы слиплись, он через секунду отступает на шаг.
    ///
    /// Секунда здесь не для красоты. Отход мгновенный читается как
    /// отталкивание — то самое, от которого уходим; пауза даёт игроку
    /// отойти самому, и тогда моб остаётся на месте.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BodySpace : MonoBehaviour
    {
        [Tooltip("Сколько секунд терпеть игрока внутри себя, прежде чем отступить.")]
        public float Patience = 1f;

        [Tooltip("Скорость отхода, метров в секунду. Быстрее — читается как толчок.")]
        public float StepSpeed = 1.8f;

        [Tooltip("Запас поверх суммы радиусов: с ним крупная морда выходит из игрока целиком, а не наполовину.")]
        public float Clearance = 0.4f;

        [Tooltip("Короче этого отшаг не читается как движение.")]
        public float MinStep = 0.5f;

        [Tooltip("Дальше этого моб уходил бы из боя.")]
        public float MaxStep = 2.5f;

        [Tooltip("Пауза после отшага, чтобы моб не пятился без остановки.")]
        public float Cooldown = 1.2f;

        private NavMeshAgent agent;
        private TargetSelector targets;
        private MonsterBrain brain;

        private float crowdedFor;
        private float stepLeft;
        private float bodyReach = 0.5f;
        private float restLeft;
        private Vector3 stepDirection;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            targets = GetComponent<TargetSelector>();
            brain = GetComponent<MonsterBrain>();

            // Встроенный обход агентов — выключить.
            //
            // Это и была «лошадь отталкивается», которую Павлон видел
            // 01.09.2026: у неё стоял HighQualityObstacleAvoidance, она
            // замечала агента героя и сама уступала дорогу. Игрок при
            // этом проходил сквозь неё — физика была ни при чём, и я
            // четыре захода чинил не ту систему.
            //
            // Расходиться существа должны ТОЛЬКО отшагом в бою (ниже),
            // как просил Павлон: «убери механику отталкивания вообще».
            if (agent != null) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            Fit();
        }

        /// <summary>
        /// Подгоняет радиус и высоту агента под фактическую модель.
        ///
        /// В сборщике они выписаны числами — 0.4 герою, 0.45 монстрам, — но
        /// монстры приходят разного размера, и у крупных радиус оказывался
        /// вдвое меньше туши. Замер по нарисованным границам знает правду о
        /// каждом. Нужен и здесь: расстояние «стоим друг в друге» считается
        /// по нему же.
        /// </summary>
        private void Fit()
        {
            if (agent == null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.3f, 1.2f);

            // Половина наибольшего горизонтального габарита. Радиус агента
            // для этого не годится: он вычисляется как 0.35 от размера и у
            // длинного тела заметно меньше полудлины — на столько моб и не
            // отходил, оставляя морду внутри игрока.
            bodyReach = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;

            agent.radius = radius;
            agent.height = Mathf.Clamp(bounds.size.y, 1f, 4f);
        }

        private void LateUpdate()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

            // Отшаг уже начат — доводим его до конца, не переспрашивая условия.
            // Отшаг меряем в МЕТРАХ, а не в секундах: длина зависит от
            // размера тела. Фиксированные 0.81 м не выводили из игрока
            // морду крупного босса — Павлон 01.09.2026: «кабан босс
            // отходит, а голова остаётся в персонаже».
            if (stepLeft > 0f)
            {
                float step = Mathf.Min(StepSpeed * Time.deltaTime, stepLeft);
                agent.Move(stepDirection * step);
                stepLeft -= step;
                if (stepLeft <= 0f) restLeft = Cooldown;
                return;
            }

            if (restLeft > 0f)
            {
                restLeft -= Time.deltaTime;
                return;
            }

            // Отходит только тот, кто дерётся с игроком. Нет боевого разума
            // или нет цели — стоим: это НПС, лошадь или мирный зверь, сквозь
            // которых игрок должен проходить насквозь.
            if (brain == null || targets == null) return;

            var target = targets.Current;
            if (target == null) { crowdedFor = 0f; return; }

            var player = target.GetComponentInParent<IsoRPG.Player.PlayerInputRouter>();
            if (player == null) { crowdedFor = 0f; return; }

            Vector3 away = transform.position - player.transform.position;
            away.y = 0f;

            float distance = away.magnitude;
            float touching = agent.radius + PlayerRadius(player);

            if (distance >= touching) { crowdedFor = 0f; return; }

            crowdedFor += Time.deltaTime;
            if (crowdedFor < Patience) return;

            // Совпали точка в точку — направление не вычислить, берём любое.
            if (distance < 0.01f)
                away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
            else
                away /= distance;

            // Вбок, а не строго назад: пятящийся моб выглядит испуганным, а
            // шаг в сторону — как «подвинулся, чтобы не мешать».
            Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -1f : 1f);

            stepDirection = (away + side * 0.7f).normalized;
            // Отходим ровно настолько, чтобы тела разошлись, плюс запас.
            stepLeft = Mathf.Clamp(bodyReach + PlayerRadius(player) - distance + Clearance, MinStep, MaxStep);
            crowdedFor = 0f;
        }

        /// <summary>Радиус тела игрока: у него теперь физическая капсула, у неё и спрашиваем.</summary>
        private static float PlayerRadius(Component player)
        {
            var body = player.GetComponentInParent<CharacterController>();
            if (body != null) return body.radius;

            var playerAgent = player.GetComponentInParent<NavMeshAgent>();
            return playerAgent != null ? playerAgent.radius : 0.4f;
        }
    }
}
