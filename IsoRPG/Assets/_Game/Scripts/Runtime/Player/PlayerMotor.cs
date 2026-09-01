using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Player
{
    /// <summary>
    /// Физическое движение героя: капсула, гравитация, столкновения.
    ///
    /// Заведён 01.09.2026, когда замер показал, что мир физику имеет — на
    /// арене автора 2077 рабочих коллайдеров, из них 1983 сеточных, — а герой
    /// её не замечает. Он ходил через <c>NavMeshAgent.Move</c>, то есть по
    /// навигационной сетке, и коллайдеры в этом пути не участвуют вовсе.
    /// Отсюда шли невидимые стены (край сетки), застревание на камнях (дыра
    /// в сетке вокруг камня) и странный прыжок (перенос по сетке, а не полёт).
    ///
    /// Теперь путь по-прежнему считает агент — он умеет обходить препятствия
    /// и знает, где земля проходима, — но ДВИГАЕТ героя
    /// <c>CharacterController</c>: капсула упирается в коллайдеры, падает
    /// под своим весом и перешагивает мелочь. Ровно та схема, что в больших
    /// РПГ: навигация советует, физика решает.
    ///
    /// Мобы остаются на чистой навигации: им физика не нужна, а тысяча
    /// капсул, толкающих друг друга, стоит кадров.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Tooltip("Сила тяжести. Земная 9.81 ощущается ватной: герой долго опускается после ступеньки.")]
        public float Gravity = 22f;

        [Tooltip("Скорость доворота корпуса, градусов в секунду.")]
        public float TurnSpeed = 720f;

        [Tooltip("Прижим к земле на склоне. Без него герой на спуске отрывается и сыплется ступеньками.")]
        public float StickToGround = 4f;

        private CharacterController body;
        private NavMeshAgent agent;
        private float fallSpeed;
        private Vector3 lastMove;
        private bool movedThisFrame;

        /// <summary>Как часто пересматривать список существ, секунд.</summary>
        private const float RescanEvery = 2f;

        private float rescanAt;

        /// <summary>
        /// Снимает столкновение капсулы с телами существ.
        ///
        /// Указание Павла 01.09.2026: сквозь мобов, НПС и лошадей игрок должен
        /// проходить насквозь, «как в WoW». Физика мира при этом остаётся —
        /// камни, заборы и стены капсулу держат, — а вот живые тела для неё
        /// проницаемы. Разводит их не физика, а <c>BodySpace</c>: моб,
        /// который с тобой дерётся, сам отступает на шаг через секунду.
        ///
        /// Существо узнаём по ДВУМ признакам, а не по одному. Сперва я брал
        /// только навигационного агента — и Павлон тут же нашёл дыру: первая
        /// лошадь в лагере агента не носит, стоит декорацией, и осталась для
        /// игрока стеной, пока НПС и вторая лошадь пропускали насквозь.
        /// Правило обязано быть общим, иначе каждое новое существо надо
        /// вспоминать поимённо. Второй признак — скелетная сетка: она есть у
        /// всего живого и не бывает у камня, забора и стены.
        ///
        /// Пересматриваем список по таймеру: мобы возрождаются, и коллайдер
        /// возрождённого физике незнаком — без этого игрок упирался бы в
        /// каждого, кто ожил после смерти.
        /// </summary>
        private void RefreshCreatureIgnores()
        {
            if (body == null) return;

            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (col == null || col.isTrigger || col.transform == transform) continue;
                if (col.GetComponentInParent<CharacterController>() == body) continue;

                if (!IsCreature(col)) continue;

                Physics.IgnoreCollision(body, col, true);
            }
        }

        /// <summary>
        /// Живое ли это тело: агент навигации или скелетная сетка выше по дереву.
        ///
        /// Скелетную ищем от владельца коллайдера вверх и затем вниз по всей
        /// его ветке: у Synty коллайдер нередко висит на корне, а сама модель
        /// с костями лежит ребёнком.
        /// </summary>
        private static bool IsCreature(Component col)
        {
            if (col.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) return true;

            var root = col.transform;
            while (root.parent != null && root.parent.GetComponent<Canvas>() == null) root = root.parent;

            return root.GetComponentInChildren<SkinnedMeshRenderer>() != null;
        }


        /// <summary>Фактическая скорость по земле. Аниматор выбирает по ней шаг и бег.</summary>
        public float Speed { get; private set; }

        /// <summary>Стоит ли герой на земле. Нужно прыжку и падению.</summary>
        public bool IsGrounded => body != null && body.isGrounded;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();

            if (agent != null)
            {
                // Агент теперь только считает путь. Позицию и поворот он не
                // трогает — иначе он и капсула тянули бы героя порознь, и
                // получилось бы перетягивание каната с дрожанием на месте.
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
        }

        /// <summary>
        /// Ведёт героя с заданной скоростью по земле.
        ///
        /// Вызывать из Update того, кто управляет: клавиш или ходьбы по клику.
        /// Вертикаль мотор считает сам.
        /// </summary>
        /// <param name="velocity">Желаемая скорость в мире, м/с. Вертикаль игнорируется.</param>
        /// <param name="turnToward">Доворачивать ли корпус по направлению движения.</param>
        public void Move(Vector3 velocity, bool turnToward = true)
        {
            if (body == null || !body.enabled) return;

            velocity.y = 0f;
            lastMove = velocity;
            movedThisFrame = true;

            if (body.isGrounded)
            {
                // Небольшой прижим вниз: ровно ноль оставляет капсулу
                // «висящей» на границе, и isGrounded начинает мигать.
                if (fallSpeed < 0f) fallSpeed = -StickToGround;
            }
            else
            {
                fallSpeed -= Gravity * Time.deltaTime;
            }

            Vector3 step = velocity;
            step.y = fallSpeed;

            body.Move(step * Time.deltaTime);

            Speed = new Vector3(body.velocity.x, 0f, body.velocity.z).magnitude;

            if (turnToward && velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(velocity.normalized),
                    TurnSpeed * Time.deltaTime);
            }

            // Агент должен знать, где герой оказался на самом деле, иначе
            // следующий путь он посчитает от старой точки.
            if (agent != null && agent.isOnNavMesh) agent.nextPosition = transform.position;
        }

        /// <summary>
        /// Падение, когда героем никто не управляет.
        ///
        /// Гравитацию считает Move, а зовут его только клавиши и ходьба по
        /// клику. Отпустил управление — и без этой страховки герой замирает
        /// в воздухе ровно там, где под ним кончилась земля.
        /// </summary>
        private void LateUpdate()
        {
            if (Time.time >= rescanAt)
            {
                rescanAt = Time.time + RescanEvery;
                RefreshCreatureIgnores();
            }

            if (!movedThisFrame) Move(Vector3.zero, false);
            movedThisFrame = false;
        }

        /// <summary>Останавливает героя, сохраняя падение. Нужно, когда управление отпущено.</summary>
        public void Halt()
        {
            Move(Vector3.zero, false);
        }

        /// <summary>Подбрасывает героя на заданную высоту. Возвращает false, если он не на земле.</summary>
        public bool Jump(float height)
        {
            if (body == null || !body.isGrounded) return false;

            fallSpeed = Mathf.Sqrt(2f * Gravity * Mathf.Max(0.1f, height));
            return true;
        }
    }
}
