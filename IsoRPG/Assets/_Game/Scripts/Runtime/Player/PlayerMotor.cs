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
