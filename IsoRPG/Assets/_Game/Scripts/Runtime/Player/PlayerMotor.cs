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

        /// <summary>
        /// Снимает столкновение капсулы с телами существ.
        ///
        /// Указание Павла 01.09.2026: сквозь мобов, НПС и лошадей игрок должен
        /// проходить насквозь, «как в WoW». Физика мира при этом остаётся —
        /// камни, заборы и стены капсулу держат. Разводит тела не физика, а
        /// <c>BodySpace</c>: моб, который с тобой дерётся, отступает сам.
        ///
        /// ЦЕНА ЗДЕСЬ ВАЖНЕЕ КРАСОТЫ. Первая версия перебирала все коллайдеры
        /// сцены раз в две секунды и для каждого поднималась к корню мира,
        /// обходя 32 444 меша: игра встала колом, Windows показывал
        /// «приложение не отвечает». Теперь проход РАЗОВЫЙ и идёт по
        /// навигационным агентам — их 263 на всю сцену, а не по коллайдерам,
        /// которых 2077. Периодического перебора нет вовсе: новых существ
        /// приносит <see cref="NoticeCreature"/>, которого зовёт возрождение.
        ///
        /// Это ММО: всё, что выполняется повторно, умножается на число
        /// игроков. Разовая тысяча операций — ничто, две секунды на кадр —
        /// смерть.
        /// </summary>
        public void RefreshCreatureIgnores()
        {
            if (body == null) return;

            foreach (var creature in Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (creature == null || creature.transform == transform) continue;
                NoticeCreature(creature.gameObject);
            }
        }

        /// <summary>
        /// Пропускает игрока сквозь одно существо.
        ///
        /// Зовётся при возрождении: у ожившего моба коллайдер новый, и физике
        /// он незнаком — без этого игрок упирался бы в каждого, кто встал.
        /// Обходим только ветку самого существа, а не сцену.
        /// </summary>
        public void NoticeCreature(GameObject creature)
        {
            if (body == null || creature == null) return;

            foreach (var col in creature.GetComponentsInChildren<Collider>(true))
            {
                if (col == null || col.isTrigger) continue;
                Physics.IgnoreCollision(body, col, true);
            }
        }

        /// <summary>
        /// Живое ли это тело: агент навигации или скелетная сетка выше по дереву.


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

        private void Start()
        {
            // Один раз за сцену. Дальше новых существ приносит
            // возрождение через NoticeCreature — периодических
            // проходов в игре нет.
            RefreshCreatureIgnores();
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
