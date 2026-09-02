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

        [Header("Прогулка")]
        [Tooltip("Как далеко от дома монстр отходит, гуляя. Ноль — стоит на месте.")]
        [SerializeField] private float patrolRadius = 4.5f;

        [Tooltip("Сколько стоит на месте между переходами, от и до.")]
        [SerializeField] private float patrolPauseMin = 2f;
        [SerializeField] private float patrolPauseMax = 6f;

        [Tooltip("Скорость прогулки. Заметно ниже боевой: гуляют шагом, гонятся бегом.")]
        [SerializeField] private float patrolSpeed = 1.3f;

        [Header("Возвращение")]
        [Tooltip("Восстанавливать здоровье, вернувшись домой. Как в WoW: сорвался с поводка — лечится.")]
        [SerializeField] private bool healOnReturn = true;

        private TargetSelector targets;
        private Health health;
        private NavMeshAgent agent;
        // Тип бойца намеренно не уточняется: мозг решает, кого бить, а
        // как именно — дело компонента. Лучник и мечник ведут себя
        // по-разному, но выбирают цель одинаково.
        private ICombatant combat;
        private Targetable self;
        private IdleBehaviour idle;

        private Vector3 homePosition;
        private float chaseSpeed;
        private float nextPatrolTime;
        private float nextScanTime;
        private bool returningHome;

        [Header("Голос")]
        [Tooltip("Рычит ли этот монстр при виде врага. Обычно только главарь.")]
        [SerializeField] private bool roars;

        [Tooltip("Чей голос: 0 главарь, 1 волк, 2 кабан. Ставится сборщиком по виду зверя.")]
        [SerializeField] private int voiceKind;

        [Tooltip("Сколько секунд молчит после рыка.")]
        [SerializeField] private float roarCooldown = 12f;

        private float nextRoar;

        /// <summary>Включить голос — зовётся из сборщика сцены для главаря.</summary>
        public void GiveVoice(float cooldown = 12f)
        {
            roars = true;
            roarCooldown = cooldown;
        }

        /// <summary>
        /// Голос по виду зверя: 0 главарь, 1 волк, 2 кабан.
        ///
        /// Павлон 02.09.2026 спросил, есть ли у мелких кабанов, волков и
        /// гриба свои звуки. Своих не было ни у кого: молчали все, кроме
        /// главаря и скелетов. Голоса он к тому времени уже нагенерил —
        /// оставалось раздать.
        /// </summary>
        public void GiveVoice(int kind, float cooldown)
        {
            roars = true;
            voiceKind = kind;
            roarCooldown = cooldown;
        }

        private void Awake()
        {
            // Разведение тел — каждому монстру.
            //
            // Ставится в коде, а не в сборщике сцены: сборщик строит
            // песочницу с нуля, и ради одного компонента пришлось бы
            // пересобирать всё, что мы в сцену поставили руками.
            if (GetComponent<BodySpace>() == null) gameObject.AddComponent<BodySpace>();

            targets = GetComponent<TargetSelector>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            combat = GetComponent<ICombatant>();
            self = GetComponent<Targetable>();

            // Праздное поведение: пока зверь занят своим делом, прогулку
            // держим. Ссылку берём один раз — это ММО, GetComponent в кадре
            // нам не по карману.
            idle = GetComponent<IdleBehaviour>();

            homePosition = transform.position;

            // Боевую скорость запоминаем до того, как её подменит прогулка.
            // Иначе после первого же патруля монстр начнёт гоняться шагом.
            chaseSpeed = agent != null ? agent.speed : 3.4f;
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

            // Цель ушла в тень прямо во время боя — теряем её.
            if (victim != null)
            {
                var vanished = victim.GetComponent<StealthState>();
                if (vanished != null && vanished.IsStealthed)
                {
                    targets.Clear();
                    GoHome();
                    return;
                }
            }

            // Цель ещё жива и рядом — держимся за неё.
            if (victim != null && victim.IsAlive
                && Vector3.Distance(transform.position, victim.transform.position) <= aggroRadius * 1.6f)
            {
                return;
            }

            var found = FindNearestEnemy();
            if (found != null)
            {
                // Возвращаем боевую скорость: гнаться прогулочным шагом —
                // это не погоня, а сопровождение.
                if (agent != null) agent.speed = chaseSpeed;

                targets.Select(found);
                if (combat != null) combat.EngageTarget();

                Roar();
            }
            else if (victim != null)
            {
                targets.Clear();
                GoHome();
            }
            else
            {
                Patrol();
            }
        }

        /// <summary>
        /// Рык при заходе в бой — только у главаря и только изредка.
        ///
        /// Звук на две секунды, и он должен читаться как событие: главарь вас
        /// заметил. Если играть его при каждом захвате цели, за один бой он
        /// прозвучит пять раз подряд — цель теряется и берётся заново на
        /// каждом шаге погони, — и вместо угрозы получится заевшая пластинка.
        /// Поэтому держим паузу и молчим, пока она не вышла.
        /// </summary>
        private void Roar()
        {
            if (!roars) return;
            if (Time.time < nextRoar) return;

            nextRoar = Time.time + roarCooldown;

            switch (voiceKind)
            {
                case 1: IsoRPG.Audio.Sfx.WolfSnarl(transform.position); break;
                case 2: IsoRPG.Audio.Sfx.BoarGrunt(transform.position); break;
                default: IsoRPG.Audio.Sfx.BossRoar(transform.position); break;
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

                // Скрытых не видим. Это и есть смысл скрытности: подойти к
                // тому, кто тебя не ищет, и ударить первым.
                var hidden = candidate.GetComponent<StealthState>();
                if (hidden != null && hidden.IsStealthed) continue;

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

        /// <summary>
        /// Прогулка вокруг дома, пока никого нет рядом.
        ///
        /// Нужна не для красоты: неподвижный противник читается как декорация,
        /// и мир вокруг кажется выключенным. Достаточно медленного шага
        /// туда-сюда, чтобы место стало обитаемым.
        ///
        /// Ходим вокруг ДОМА, а не вокруг текущей точки: иначе монстр
        /// случайными шагами уползает всё дальше и однажды оказывается в
        /// соседнем лагере.
        /// </summary>
        private void Patrol()
        {
            if (agent == null || !agent.isOnNavMesh || patrolRadius <= 0.01f) return;

            // Занят своим делом — не гоняем гулять.
            //
            // Без этого прогулка обрывала покой через полсекунды после
            // начала: зверь ложился и тут же вставал, потому что получал
            // новую точку маршрута. Павлон 02.09.2026 увидел это первым.
            if (idle != null && idle.Resting) return;

            // Путь ещё считается или ещё идём — не мешаем.
            if (agent.pathPending) return;
            if (agent.remainingDistance > agent.stoppingDistance + 0.3f) return;

            // Пришли. Стоим положенную паузу: монстр, ходящий без остановок,
            // выглядит заведённой игрушкой.
            if (Time.time < nextPatrolTime) return;

            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            Vector3 wanted = homePosition + new Vector3(offset.x, 0f, offset.y);

            // Точку проверяем по навигационной сетке: случайная может попасть
            // в стену или за край земли, и тогда монстр упрётся и застрянет.
            if (NavMesh.SamplePosition(wanted, out var hit, patrolRadius, NavMesh.AllAreas))
            {
                agent.speed = patrolSpeed;
                agent.SetDestination(hit.position);
                nextPatrolTime = Time.time + Random.Range(patrolPauseMin, patrolPauseMax);
            }
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

            agent.speed = chaseSpeed;
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
