using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace IsoRPG.Player
{
    /// <summary>
    /// Прыжок по пробелу.
    ///
    /// Ничего не даёт и не должен: в игре с кликом по земле прыгать некуда,
    /// препятствия обходит навигация. Но прыжок — первое, что человек жмёт,
    /// когда берёт мышь и клавиатуру, и его отсутствие читается как
    /// незаконченная игра. Это жест, а не механика.
    ///
    /// Персонажа поднимает код, а не анимация: движением заведует агент
    /// навигации, он держит героя на поверхности и любое смещение по высоте
    /// из анимации съедает. Поэтому вверх едет модель внутри персонажа —
    /// агент при этом спокойно продолжает вести его по земле, и прыгать
    /// можно на бегу.
    /// </summary>
    public sealed class JumpGesture : MonoBehaviour
    {
        [Tooltip("Насколько высоко подпрыгивает, в метрах.")]
        [SerializeField] private float height = 1.7f;

        [Tooltip("Высота настоящего прыжка с физической капсулой, метров. Отдельно от height: тот задавал дугу переноса, а это подъём тела.")]
        [SerializeField] private float jumpHeight = 1.45f;

        [Tooltip("Сколько длится прыжок, вместе со взлётом и приземлением.")]
        //
        // Замер по WoW: там персонаж висит в воздухе около секунды и
        // поднимается примерно на свой рост. У нас было 0.55 м за 0.75 с —
        // и прыжок читался как клевок, будто герой споткнулся. Разница
        // именно в ощущении веса: короткий низкий прыжок делает персонажа
        // лёгким и суетливым.
        //
        // Клип Jump_Full_Short короче секунды, поэтому его скорость
        // подгоняется под длительность — иначе персонаж успевает приземлиться
        // анимацией, вися при этом в воздухе.
        [SerializeField] private float duration = 1.0f;

        private CharacterAnimatorDriver animation;
        private IsoRPG.Combat.Health health;
        private IsoRPG.Items.FoodConsumer food;
        private Transform model;

        private float startTime = -99f;
        private float baseHeight;

        /// <summary>Летит ли сейчас. Дублирует IsJumping, чтобы флаг в аниматор уходил один раз на смену, а не каждый кадр.</summary>
        private bool airborne;

        [Tooltip("На сколько метров вперёд переносит прыжок через преграду.")]
        [SerializeField] private float hopDistance = 2.6f;

        [Tooltip("На сколько выше себя герой готов запрыгнуть, в метрах.")]
        [SerializeField] private float maxRise = 0.9f;

        [Tooltip("На сколько ниже готов спрыгнуть.")]
        [SerializeField] private float maxDrop = 1.6f;

        private UnityEngine.AI.NavMeshAgent agent;
        private Vector3 hopFrom;
        private Vector3 hopTo;
        private bool hopping;
        private PlayerMotor motor;

        /// <summary>
        /// Летит ли герой. С физической капсулой спрашиваем её саму:
        /// прыжок кончается приземлением, а не истёкшим таймером, —
        /// иначе анимация вставала на ноги, пока тело ещё в воздухе.
        /// Первые полтораста миллисекунд считаем полётом всегда: на
        /// кадре толчка капсула ещё касается земли.
        /// </summary>
        public bool IsJumping => motor != null
                                 ? Time.time < startTime + 0.15f || !motor.IsGrounded
                                 : Time.time < startTime + duration;

        private void Awake()
        {
            animation = GetComponent<CharacterAnimatorDriver>();
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            health = GetComponent<IsoRPG.Combat.Health>();
            food = GetComponent<IsoRPG.Items.FoodConsumer>();
            motor = GetComponent<PlayerMotor>();

            // Поднимаем ту же ветку, в которой живёт аниматор: это и есть
            // видимая модель, всё остальное — логика и коллайдер.
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) model = animator.transform;

            if (model != null) baseHeight = model.localPosition.y;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TryJump();

            Lift();
        }

        private void TryJump()
        {
            if (IsJumping) return;
            if (health != null && !health.IsAlive) return;

            // Прыжок со стула — это вставание. Еда прерывается своим же
            // правилом «встал с места», просто скажем это вслух.
            if (food != null && food.IsEating) food.Interrupt("подпрыгнул");

            startTime = Time.time;
            if (animation != null) animation.PlayJump();

            TryHop();
        }

        /// <summary>
        /// Перенос через низкое препятствие.
        ///
        /// Почему это вообще нужно объяснять: героя ведёт навигационный
        /// агент по сетке, и всё, чего на сетке нет, для него не существует.
        /// Каменная стенка по колено — не «низкая», её просто нет на карте
        /// проходимости, и упереться в неё можно бесконечно. Павлон трижды
        /// написал «не перепрыгивает» — и был прав: анимация прыжка над этим
        /// не властна.
        ///
        /// Порядок здесь важнее самого прыжка:
        /// 1) смотрим, КУДА герой смотрит или бежит;
        /// 2) ищем на сетке площадку впереди — если её нет, прыгаем на месте;
        /// 3) спрашиваем сетку, можно ли туда ДОЙТИ. Если можно — прыгать
        ///    незачем, там нет преграды, и перенос выглядел бы телепортом;
        /// 4) переносим только когда дойти нельзя, а встать есть куда.
        ///
        /// Пункт 3 и делает это перепрыгиванием, а не рывком сквозь стены:
        /// проверка ровно та же, которой пользуется сам агент.
        /// </summary>
        private void TryHop()
        {
            // Физическая капсула — прыгаем по-настоящему.
            //
            // Никакого поиска площадки: толчок вверх, а горизонталь даёт то
            // же управление, что и на бегу. Куда долетел — там и приземлился,
            // столкновения по дороге считает сама капсула.
            //
            // Прежний перебор точек и был причиной обеих жалоб Павла: он
            // искал, КУДА перенести героя, и потому то перелетал низкий
            // камень (ближайшая годная точка оказывалась за ним), то
            // затаскивал на высокий, с которого не слезть — наверху нет
            // навигационной сетки, и обратный поиск не находил ничего.
            if (motor != null)
            {
                if (motor.Jump(jumpHeight)) startTime = Time.time;
                return;
            }

            if (agent == null || !agent.isOnNavMesh) return;

            Vector3 direction = transform.forward;

            // Бежит — прыгаем туда, куда бежит, а не куда смотрит модель.
            if (agent.velocity.sqrMagnitude > 0.05f)
                direction = agent.velocity.normalized;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            direction.Normalize();

            Vector3 from = transform.position;

            // Ищем БЛИЖАЙШУЮ площадку за преградой, а не самую дальнюю.
            //
            // Первая версия всегда мерила 2.6 м, и герой перелетал низкий
            // камень, чтобы приземлиться на высокий в нескольких метрах —
            // Павлон 01.09.2026: «стою перед небольшим камнем, а прыгаю
            // намного дальше». Шагаем от короткого прыжка к длинному и
            // берём первое, что подошло.
            bool found = false;
            NavMeshHit landing = default;

            for (float distance = 1.1f; distance <= hopDistance + 0.01f; distance += 0.5f)
            {
                Vector3 wanted = from + direction * distance;

                if (!NavMesh.SamplePosition(wanted, out var candidate, 0.8f, NavMesh.AllAreas)) continue;

                // Перепад высот. Без него прыжок затаскивал на валун, с
                // которого потом не слезть: наверху сетки нет, и агент там
                // просто не работает. По колено вверх и по пояс вниз —
                // столько, сколько человек и правда перепрыгивает.
                float rise = candidate.position.y - from.y;
                if (rise > maxRise || rise < -maxDrop) continue;

                // Дойти можно и без прыжка — значит преграды нет, и перенос
                // читался бы телепортом.
                if (!NavMesh.Raycast(from, candidate.position, out _, NavMesh.AllAreas)) continue;

                landing = candidate;
                found = true;
                break;
            }

            if (!found) return;

            hopFrom = from;
            hopTo = landing.position;
            hopping = true;

            // Агент на время полёта выключаем: он держит героя на сетке и
            // любое смещение за её край отменяет. Ходьба на клавишах при
            // выключенном агенте сама останавливается — она его и спрашивает.
            agent.enabled = false;
        }

        /// <summary>
        /// Парабола от нуля до нуля. Считается каждый кадр, а не хранится:
        /// так прыжок сам заканчивается ровно на земле, чем бы его ни
        /// прервали.
        /// </summary>
        private void Lift()
        {
            // Фаза полёта держится флагом, а не длиной клипа: иначе зависание
            // кончалось раньше самого прыжка, и герой перебирал ногами в
            // воздухе. Сообщаем и когда прыжок кончился — по этому же флагу
            // играется приземление.
            if (animation != null && airborne != IsJumping)
            {
                airborne = IsJumping;
                animation.SetAirborne(airborne);
            }

            // С физической капсулой тело летит по-настоящему: ни переносить
            // его, ни поднимать модель не надо, иначе к настоящему полёту
            // добавится второй, нарисованный.
            if (motor != null) return;

            // Перенос через преграду. Идёт по земле, а модель поверх этого
            // поднимается своей параболой — вместе выходит дуга.
            if (hopping)
            {
                float k = Mathf.Clamp01((Time.time - startTime) / duration);

                transform.position = Vector3.Lerp(hopFrom, hopTo, k);

                if (k >= 1f)
                {
                    hopping = false;

                    // Возвращаем агента ровно на сетку. Warp, а не
                    // присваивание: иначе агент считает, что он всё ещё в
                    // точке старта, и первым же кадром дёрнет героя назад.
                    if (agent != null)
                    {
                        agent.enabled = true;
                        if (agent.isOnNavMesh) agent.Warp(hopTo);
                    }
                }
            }

            if (model == null) return;

            float lift = 0f;

            if (IsJumping)
            {
                float t = (Time.time - startTime) / duration;
                lift = 4f * height * t * (1f - t);

                // Переводим метры в локальные единицы модели.
                //
                // Подъём применяется к дочернему объекту, а он
                // отмасштабирован — KayKit ужимался под наш рост. Записанные
                // сюда метры превращались в мире в заметно меньшую высоту, и
                // прыжок читался как «еле оторвался от земли», хотя в числах
                // выглядел правильным.
                float scale = model.lossyScale.y;
                if (scale > 0.001f) lift /= scale;
            }

            var local = model.localPosition;

            // Сравнение с запасом: писать в transform каждый кадр, когда
            // персонаж просто стоит, незачем.
            if (Mathf.Abs(local.y - (baseHeight + lift)) < 0.0005f) return;

            local.y = baseHeight + lift;
            model.localPosition = local;
        }
    }
}
