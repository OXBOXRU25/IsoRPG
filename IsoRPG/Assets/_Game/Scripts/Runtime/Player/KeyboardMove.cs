using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Ходьба на WASD раскладкой WoW.
    ///
    /// Задача Павла 04.09.2026: «делаем один в один как в ВОВ». Там раскладка
    /// такая: <b>W</b> — вперёд по курсу ГЕРОЯ, <b>S</b> — назад не
    /// разворачиваясь, <b>A</b> и <b>D</b> — поворот на месте с переступанием,
    /// <b>Q</b> и <b>E</b> — боковой ход. А под зажатой правой кнопкой мыши
    /// A и D сами становятся боковым ходом, потому что курс в этот момент
    /// задаёт камера.
    ///
    /// Ключевое отличие от прежней схемы: направление считается от КУРСА
    /// ГЕРОЯ, а не от камеры. Раньше «вперёд» означало «туда, куда смотрит
    /// камера», и герой доворачивался за движением. В WoW наоборот: корпус
    /// поворачивают только A/D и правая кнопка, а ноги идут туда, куда корпус
    /// уже смотрит. Отсюда и берётся боковой ход как отдельная пластика —
    /// иначе он невозможен в принципе, герой всегда шёл бы лицом вперёд.
    ///
    /// Двигает по-прежнему <see cref="PlayerMotor"/> — физическая капсула:
    /// она упирается в коллайдеры мира и скользит вдоль них. Агент навигации
    /// остаётся только считать пути для ходьбы по клику.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class KeyboardMove : MonoBehaviour
    {
        [Tooltip("Скорость доворота корпуса за направлением, градусов в секунду. Для схемы без мотора.")]
        public float TurnSpeed = 720f;

        /// <summary>
        /// Скорость поворота вида по A и D, градусов в секунду.
        ///
        /// Число не выбрано, а СНЯТО с набора. У Synty два темпа поворота:
        /// доворот на 90° идёт 93 град/с, разворот на 180° — 142. Сперва
        /// стояло 142, и Павлон 04.09.2026 в игре сказал: «слишком большая
        /// скорость, надо медленнее примерно на 1/3». Треть от 142 — это 95,
        /// то есть его глаз попал ровно в медленный клип автора.
        ///
        /// На 93 клип шёл как нарисован, но Павлон в игре сказал: «ещё
        /// медленнее на 1/2, очень быстро, голова кружится». Половина от 93 —
        /// 47 град/с, полный оборот за 7.7 секунды.
        ///
        /// Медленнее нарисованного набор не умеет, поэтому клип идёт на
        /// половинном темпе. Это осознанный размен: замедление читается мягко,
        /// в отличие от ускорения, которое выглядит перемоткой. Ноги при этом
        /// с землёй совпадают — темп клипа привязан к этому же числу.
        ///
        /// По ней же сборщик дерева выбирает клип и подгоняет темп: разойдись
        /// эти числа — ноги поедут по земле.
        /// </summary>
        public const float TurnInPlaceDefault = 47f;

        /// <summary>
        /// Поворот вида по A и D, градусов в секунду.
        ///
        /// НЕ поле, а константа — и это лечение конкретной поломки.
        /// 04.09.2026 я трижды правил скорость (142 → 93 → 47), сборка каждый
        /// раз печатала новое число, а Павлон в игре крутился на 180: значение
        /// жило сериализованным полем в сцене, и Unity брал сохранённое, а не
        /// моё. Журнал при этом не врал — он читал константу, которую я и
        /// менял. Проверка была зелёной на пустом множестве.
        ///
        /// Настройка в инспекторе тут не нужна: Unity я гоняю сам, а вот
        /// разъехаться значению между сценой и кодом больше нельзя.
        /// </summary>
        public float TurnInPlaceSpeed => TurnInPlaceDefault;

        /// <summary>
        /// Доли скорости при ходе назад и вбок — константами, потому что их
        /// знает не только игра.
        ///
        /// Вбок — полная скорость: Павлон 04.09.2026 отобрал клипы поимённо
        /// (`Run_FwdStrafeF/L/R` у Synty), и вперёд с боками у него один
        /// набор, значит и скорость одна.
        ///
        /// Назад медленнее: там его выбор — `Relax-Walk-Backward`, а это
        /// ходьба, а не бег. На полной скорости она превратилась бы в
        /// перемотку.
        ///
        /// По ним же расставлены пороги дерева хода: сборщик
        /// <c>HeroMoveKit</c> ставит кольцо бега ровно на боковую скорость,
        /// иначе клип не совпадёт с землёй. Это то самое «одно число в двух
        /// местах», на котором мы горели: пусть второе место СПРАШИВАЕТ
        /// первое, а не повторяет его по памяти.
        /// </summary>
        public const float SideFactorDefault = 1f;

        /// <inheritdoc cref="SideFactorDefault"/>
        public const float BackFactorDefault = 0.55f;

        /// <summary>
        /// Доли скорости — свойствами, а не полями, по той же причине, что и
        /// поворот: сериализованное поле в сцене помнит своё значение и
        /// молча побеждает правку в коде.
        /// </summary>
        public float BackFactor => BackFactorDefault;

        /// <inheritdoc cref="BackFactor"/>
        public float SideFactor => SideFactorDefault;

        private NavMeshAgent agent;
        private PlayerMotor motor;
        private ClickToMoveController clicker;
        private IsoRPG.Cameras.IsoCameraRig rig;
        private bool steering;

        /// <summary>Ведёт ли сейчас управление клавишами.</summary>
        public bool IsSteering => steering;

        /// <summary>
        /// Куда герой ХОЧЕТ идти в своих координатах: X вправо, Y вперёд,
        /// длина — единица, ноль в покое.
        ///
        /// Это и есть то, что кормит двумерное дерево хода. Именно желание, а
        /// не фактическая скорость: упёршись в камень, герой в WoW продолжает
        /// перебирать ногами в ту сторону, куда жмёт игрок, а капсула сама
        /// решает, поедет он или нет.
        /// </summary>
        public Vector2 Wish { get; private set; }

        /// <summary>Во сколько раз желаемая скорость меньше полной: назад и вбок медленнее.</summary>
        private float wishScale = 1f;

        /// <summary>
        /// Скорость, с которой герой идёт по клавишам.
        ///
        /// Пока клавиша нажата, ноги бегут — даже если герой упёрся в камень
        /// и никуда не едет. Фактическая скорость капсулы в упоре ровно ноль,
        /// и герой замирал столбом; в WoW он топчется на месте, а стоит
        /// повернуть камеру — идёт вдоль преграды. Скольжение вдоль даёт сама
        /// капсула, от нас нужны только ноги.
        /// </summary>
        public float Speed => !steering ? 0f
                             : agent != null ? agent.speed * wishScale
                             : motor != null ? motor.Speed : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            clicker = GetComponent<ClickToMoveController>();
            motor = GetComponent<PlayerMotor>();
        }

        /// <summary>
        /// Держит ли игрок правую кнопку — то есть курс задаёт камера.
        ///
        /// Спрашиваем у камеры, а не считаем сами: там это условие уже есть
        /// вместе с проверкой «нажали по миру, а не по окну», и вторая копия
        /// разошлась бы с первой на первой же правке. Ссылку берём один раз и
        /// держим: это ММО, поиск по сцене каждый кадр здесь недопустим.
        /// </summary>
        private bool CameraSteers => rigForTurn != null && rigForTurn.SteeringHero;

        /// <summary>Камера, найденная один раз и запомненная. Поиск по сцене каждый кадр это ММО не переживёт.</summary>
        private IsoRPG.Cameras.IsoCameraRig rigForTurn
        {
            get
            {
                if (rig == null)
                {
                    var camera = Camera.main;
                    if (camera != null) rig = camera.GetComponentInParent<IsoRPG.Cameras.IsoCameraRig>();
                    if (rig == null) rig = Object.FindFirstObjectByType<IsoRPG.Cameras.IsoCameraRig>();
                }

                return rig;
            }
        }

        /// <summary>
        /// Крутит ли игрок вид клавишами: −1 влево, +1 вправо, 0 — нет.
        ///
        /// Нужно аниматору: при повороте на месте герой должен переступать
        /// ногами, а не ехать вокруг оси. Спрашивать фактический доворот
        /// корпуса нельзя — он идёт и от мыши, и от ходьбы по клику.
        /// </summary>
        public float Turning => turning;

        private float turning;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || agent == null || !agent.isOnNavMesh) return;

            bool byCamera = CameraSteers;

            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float leftRight = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);

            // Q и E — боковой ход всегда, как в WoW. A и D становятся им
            // только под правой кнопкой; без неё они поворачивают корпус.
            float sideways = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            float turn = 0f;

            if (byCamera) sideways += leftRight;
            else turn = leftRight;

            sideways = Mathf.Clamp(sideways, -1f, 1f);

            // Поворот на месте. Крутится КАМЕРА, а герой идёт за ней.
            //
            // Поправка Павла 04.09.2026: «A и D не просто персонажа
            // поворачивают, а поворачивают камеру, а персонаж поворачивается
            // за ней». Первая версия крутила корпус напрямую, и вид оставался
            // висеть — герой уходил из кадра боком.
            //
            // Идёт и на ходу: тогда герой заворачивает дугой, ровно как в WoW
            // при беге с зажатой A.
            if (Mathf.Abs(turn) > 0.01f)
            {
                float degrees = turn * TurnInPlaceSpeed * Time.deltaTime;

                if (rigForTurn != null) rigForTurn.TurnFromKeys(degrees);
                else transform.Rotate(0f, degrees, 0f);

                turning = Mathf.Abs(turn) > 0.01f ? turn : 0f;
            }
            else
            {
                turning = 0f;
            }

            if (Mathf.Abs(sideways) < 0.01f && Mathf.Abs(forward) < 0.01f)
            {
                Release();
                return;
            }

            // Направление в СВОИХ координатах героя. Ось Y — вперёд, X —
            // вправо: ровно то, что понимает двумерное дерево хода.
            Vector2 wish = new Vector2(sideways, forward);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // Ход назад и вбок медленнее переднего — иначе кольцо клипов
            // приходится гнать вдвое. Доли смешиваем по вкладу каждой оси,
            // чтобы на диагонали не было ступеньки.
            float pace = Mathf.Lerp(1f, BackFactor, Mathf.Max(0f, -wish.y));
            pace = Mathf.Lerp(pace, SideFactor, Mathf.Abs(wish.x) * (1f - Mathf.Max(0f, -wish.y)));

            Wish = wish;
            wishScale = pace;

            Vector3 direction = transform.right * wish.x + transform.forward * wish.y;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                Release();
                return;
            }

            direction.Normalize();

            Take();

            // Есть физическая капсула — ведём ею: она упирается в коллайдеры
            // мира, а agent.Move прошёл бы сквозь них.
            //
            // Корпус мотору доворачивать ЗАПРЕЩЕНО: в этой схеме его крутят
            // только A/D и правая кнопка. Доворот за направлением движения
            // сделал бы боковой ход невозможным — герой на первом же кадре
            // развернулся бы лицом туда, куда идёт.
            if (motor != null)
            {
                motor.Move(direction * agent.speed * pace, turnToward: false);
                return;
            }

            agent.Move(direction * agent.speed * pace * Time.deltaTime);
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
            Wish = Vector2.zero;
            wishScale = 1f;
            // Поворот на месте здесь НЕ гасим: он живёт своей клавишей и
            // продолжается, даже когда герой никуда не идёт. Ровно ради этого
            // случая и нужен клип переступания.

            if (!steering) return;

            steering = false;
            agent.updateRotation = true;
        }
    }
}
