using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Ходьба на WASD — вторым способом, рядом с ходьбой по клику.
    ///
    /// Понадобилось ровно тогда, когда камера впервые встала за спину: в
    /// изометрии ходьба по клику естественна, а из-за плеча мозг ждёт
    /// клавиш, и первое же ощущение — «тормозит, неудобно». Дело не в
    /// скорости, а в том, что глазами ты уже внутри мира, а руками
    /// по-прежнему тыкаешь в землю.
    ///
    /// Направление считается ОТ КАМЕРЫ, а не от мира: «вперёд» — это туда,
    /// куда смотрит игрок, иначе при повёрнутой камере W ведёт вбок и
    /// управление ощущается сломанным.
    ///
    /// Движение идёт через <c>NavMeshAgent.Move</c>, а не присваиванием
    /// позиции. Так герой остаётся на навигационной сетке: не проходит
    /// сквозь стены, не сваливается с помоста и — важное — сохраняет
    /// <c>agent.velocity</c>, по которому наш аниматор выбирает шаг и бег.
    /// Присваивание позиции дало бы скольжение в позе стоя.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class KeyboardMove : MonoBehaviour
    {
        [Tooltip("Скорость доворота корпуса, градусов в секунду.")]
        public float TurnSpeed = 720f;

        private NavMeshAgent agent;
        private ClickToMoveController clicker;
        private bool steering;

        /// <summary>Ведёт ли сейчас управление клавишами.</summary>
        public bool IsSteering => steering;

        /// <summary>
        /// С какой скоростью герой идёт по клавишам.
        ///
        /// Нужно аниматору. Он выбирает шаг и бег по скорости агента, а при
        /// движении через Move агент считает свою скорость нулевой — и
        /// персонаж едет по земле в позе стоя. Спрашивать надо у того, кто
        /// реально двигает.
        /// </summary>
        public float Speed => steering && agent != null ? agent.speed : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            clicker = GetComponent<ClickToMoveController>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || agent == null || !agent.isOnNavMesh) return;

            float sideways = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

            if (Mathf.Abs(sideways) < 0.01f && Mathf.Abs(forward) < 0.01f)
            {
                Release();
                return;
            }

            var camera = Camera.main;
            if (camera == null) return;

            // Оси камеры, положенные на землю: наклон камеры не должен
            // уводить героя в небо или под землю.
            Vector3 ahead = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;

            Vector3 direction = (ahead * forward + right * sideways).normalized;
            if (direction.sqrMagnitude < 0.001f) return;

            Take();

            agent.Move(direction * agent.speed * Time.deltaTime);

            // Доворот корпуса. Персонаж всегда идёт лицом вперёд — тогда
            // хватает имеющихся клипов ходьбы и бега, и не нужны отдельные
            // анимации шага вбок и назад, которых у нас нет.
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                TurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Забирает управление у ходьбы по клику.
        ///
        /// Без сброса пути агент продолжает тянуть героя к последней точке,
        /// куда ткнули мышью, и получается перетягивание каната: клавиши
        /// ведут в одну сторону, старый приказ — в другую.
        /// </summary>
        private void Take()
        {
            if (steering) return;

            steering = true;

            agent.ResetPath();

            // Поворот берём на себя: агент разворачивает тело в сторону
            // своего пути, а пути сейчас нет — он бы просто замер.
            agent.updateRotation = false;
        }

        private void Release()
        {
            if (!steering) return;

            steering = false;
            agent.updateRotation = true;
        }
    }
}
