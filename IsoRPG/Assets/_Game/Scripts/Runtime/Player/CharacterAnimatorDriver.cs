using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Player
{
    /// <summary>Каким жестом играется способность.</summary>
    public enum CastKind
    {
        /// <summary>Направленный удар по одной цели.</summary>
        Attack,

        /// <summary>Удар по площади.</summary>
        Area,

        /// <summary>Усиление себя.</summary>
        Buff,
    }

    /// <summary>
    /// Связывает навигацию с анимацией: передаёт в контроллер текущую скорость,
    /// по которой дерево смешивания выбирает стойку, шаг или бег.
    ///
    /// Ключевая идея: движением командует NavMeshAgent, а анимация лишь
    /// отражает результат. Обратный порядок (когда двигает анимация) даёт
    /// скольжение и рассинхрон, и именно ради этого при скачивании с Mixamo
    /// ставится галочка In Place.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        /// <summary>Множитель скорости приземления: на бегу оно должно пройти быстрее.</summary>
        private static readonly int LandSpeedHash = Animator.StringToHash("LandSpeed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int StealthKillHash = Animator.StringToHash("StealthKill");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int EatingHash = Animator.StringToHash("Eating");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        // Оживление боя: номер удара в серии, вздрагивание, уклонение и
        // жесты способностей. Параметров может не быть у старых контроллеров
        // (у зверей их нет вовсе) — поэтому всё через проверку Has ниже.
        private static readonly int AttackVariantHash = Animator.StringToHash("AttackVariant");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        private static readonly int CastAttackHash = Animator.StringToHash("CastAttack");
        private static readonly int CastAOEHash = Animator.StringToHash("CastAOE");
        private static readonly int CastBuffHash = Animator.StringToHash("CastBuff");
        private static readonly int SneakingHash = Animator.StringToHash("Sneaking");
        private static readonly int AirborneHash = Animator.StringToHash("InAir");

        // Состояние вздрагивания зовётся «GetHit» у всех: и у героя, и у
        // зверей — так его называет задание combat-anims.
        private static readonly int FlinchStateHash = Animator.StringToHash("GetHit");

        // Богатые наборы (босс-кабан, гриб) умеют больше обычного зверя:
        // сторона вздрагивания, боевая стойка, боковое смещение. У кого таких
        // параметров нет — вызовы уходят в пустоту, это проверяет Has.
        private static readonly int HitDirHash = Animator.StringToHash("HitDir");
        private static readonly int InCombatHash = Animator.StringToHash("InCombat");
        private static readonly int StrafeHash = Animator.StringToHash("Strafe");

        // Скорость разворота, градусы в секунду со знаком. Нужна зверям, у
        // которых набор принёс повороты на месте: без неё кабан разворачивался
        // голым скольжением — Павлон это видел («мелкие кабаны в бою
        // разворачиваются»), и лечили мы тогда не то.
        private static readonly int TurnHash = Animator.StringToHash("Turn");

        // Какой смертью умирать, когда их у набора несколько. У гриба этот же
        // параметр значит другое (2 — расплющивание тяжёлым), поэтому жеребьёвка
        // включается только полем deathVariants, а не самим наличием параметра.
        private static readonly int DeathVariantHash = Animator.StringToHash("DeathVariant");

        /// <summary>
        /// Потолок серии ударов. Больше, чем у самого богатого набора: у
        /// босса-кабана семь, у гриба пять, у волка три. Список нужен и для
        /// опознания «идёт свой замах», и для подсчёта, сколько вариантов
        /// вообще есть у этого существа.
        /// </summary>
        private const int MaxAttackVariants = 12;

        // Состояния удара: у простых зверей одно «Attack», у богатых наборов
        // серия «Attack_1»… «Attack_N» (их заводит то же задание).
        private static readonly int[] AttackStateHashes = BuildAttackHashes();

        private static int[] BuildAttackHashes()
        {
            var hashes = new int[MaxAttackVariants + 1];
            hashes[0] = Animator.StringToHash("Attack");

            for (int i = 1; i <= MaxAttackVariants; i++)
                hashes[i] = Animator.StringToHash("Attack_" + i);

            return hashes;
        }

        /// <summary>
        /// Пауза между вздрагиваниями.
        ///
        /// 1.2 с при реакции в 0.41 с: зверь дёргается примерно на каждый
        /// второй удар и остаётся в реакции пятую часть боя, а не всю его.
        /// Было 0.35 — этого не хватило, потому что мешала не частота
        /// триггера, а длина клипа (замер 01.09.2026: реакция 1.42 с при
        /// ударе героя раз в 1.4 с).
        /// </summary>
        private const float FlinchGap = 1.2f;

        private float nextHitTime;

        [Tooltip("Аниматор персонажа. Обычно на дочерней модели.")]
        [SerializeField] private Animator animator;

        [Tooltip("Сглаживание разгона. Без него при старте ноги дёргаются, потому что агент меняет скорость рывком.")]
        [SerializeField] private float speedSmooth = 8f;

        /// <summary>Здоровье владельца — источник вздрагиваний.</summary>
        private IsoRPG.Combat.Health health;

        /// <summary>Скрытность владельца — меняет походку.</summary>
        private IsoRPG.Combat.StealthState stealth;

        [Tooltip("Сглаживание остановки — намеренно резче разгона. Пока сглаженная скорость сползает вниз, аниматор считает персонажа бегущим и не даёт начаться удару.")]
        [SerializeField] private float stopSmooth = 22f;

        [Tooltip("Ниже этой скорости считаем, что персонаж стоит. Спасает от подрагивания стойки, когда агент доезжает последние сантиметры.")]
        [SerializeField] private float idleThreshold = 0.1f;

        private NavMeshAgent agent;
        private PlayerMotor motor;
        private float smoothedSpeed;
        private KeyboardMove keys;

        // Есть ли у этого аниматора богатые параметры. Считаем ОДИН раз при
        // включении: проверять список параметров каждый кадр у каждого
        // существа — это ММО, так делать нельзя.
        private IsoRPG.Combat.TargetSelector targets;
        private bool hasCombatFlag;
        private bool hasStrafe;
        private bool hasTurn;

        [Tooltip("Сколько равнозначных смертей у набора. 0 и 1 — жеребьёвки нет. У кабана Malbers их две.")]
        [SerializeField] private int deathVariants;

        /// <summary>Прошлый курс и сглаженная скорость разворота — для параметра Turn.</summary>
        private float lastYaw;
        private float turnRate;

        /// <summary>
        /// Сколько ударов в серии умеет ИМЕННО этот персонаж.
        ///
        /// Считаем по состояниям контроллера, а не константой в бою: у героя
        /// шесть, у босса семь, у кабана четыре, у волка три. Раньше боевой код
        /// слал номер от 1 до 6 всем подряд — у кабана с четырьмя ударами
        /// номера 5 и 6 не совпадали ни с одним переходом, и зверь бил вообще
        /// без анимации каждый третий раз.
        ///
        /// Ноль значит «серии нет»: контроллер обойдётся одним «Attack».
        ///
        /// Число ставит задание сборки — оно знает его точно, из своего же
        /// кода. Если не поставлено, считаем сами по состояниям контроллера.
        /// </summary>
        [Tooltip("Сколько ударов в серии. 0 — посчитать самим по состояниям Attack_N.")]
        [SerializeField] private int attackVariants;

        public int AttackVariants => attackVariants;

        /// <summary>Сколько ударов в серии. Ставит задание сборки контроллера.</summary>
        public void SetAttackVariants(int count) => attackVariants = count;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            motor = GetComponent<PlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            health = GetComponent<IsoRPG.Combat.Health>();
        }

        /// <summary>
        /// Вздрагивание вешаем здесь, а не в бою.
        ///
        /// Бьющих много — ближний бой, стрелы, ловушки, яд, — и подписывать
        /// каждого значит однажды забыть одного. Здоровье же знает про урон
        /// в единственном месте, откуда бы он ни пришёл.
        /// </summary>
        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;

            // Один раз узнаём, что умеет этот аниматор: у босса-кабана есть
            // боевая стойка и боковое смещение, у рядового зверя — нет.
            hasCombatFlag = Has(InCombatHash, AnimatorControllerParameterType.Bool);
            hasStrafe = Has(StrafeHash, AnimatorControllerParameterType.Float);
            hasTurn = Has(TurnHash, AnimatorControllerParameterType.Float);

            CountAttackVariants();

            lastYaw = transform.eulerAngles.y;

            // Скрытность меняет саму походку, поэтому слушаем её здесь же.
            if (stealth == null) stealth = GetComponent<IsoRPG.Combat.StealthState>();

            if (stealth != null)
            {
                stealth.StealthChanged += SetSneaking;
                SetSneaking(stealth.IsStealthed);
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (stealth != null) stealth.StealthChanged -= SetSneaking;
        }

        /// <summary>
        /// Пересчитать серию ударов по состояниям контроллера. Один раз при
        /// включении: спрашивать это в бою значило бы перебирать состояния на
        /// каждом замахе у каждого зверя.
        /// </summary>
        private void CountAttackVariants()
        {
            // Задание уже сказало точное число — верим ему, а не опросу.
            if (attackVariants > 0) return;

            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!Has(AttackVariantHash, AnimatorControllerParameterType.Int)) return;

            // Считаем подряд с первого: дыра в нумерации значила бы, что
            // задание собрало контроллер криво, и продолжать счёт за ней
            // нельзя — номер всё равно попал бы в пустоту.
            for (int i = 1; i <= MaxAttackVariants; i++)
            {
                if (!animator.HasState(0, AttackStateHashes[i])) break;
                attackVariants = i;
            }
        }

        private void OnDamaged(int amount, GameObject source)
        {
            // Мёртвые не вздрагивают: поверх падения это выглядит как
            // судорога, а не как удар.
            if (health != null && !health.IsAlive) return;

            AimFlinch(source);
            PlayHit();
        }

        /// <summary>
        /// С какой стороны прилетело: 0 спереди, 1 сзади, 2 слева, 3 справа.
        ///
        /// У босса-кабана на каждую сторону свой клип — набор их рисовал, а мы
        /// до 02.09.2026 не пользовались вовсе. У кого таких клипов нет,
        /// параметра тоже нет, и вызов уходит в пустоту без вреда.
        /// </summary>
        private void AimFlinch(GameObject source)
        {
            if (animator == null || source == null) return;
            if (!Has(HitDirHash, AnimatorControllerParameterType.Int)) return;

            Vector3 from = source.transform.position - transform.position;
            from.y = 0f;

            if (from.sqrMagnitude < 0.01f) return;

            from.Normalize();

            float ahead = Vector3.Dot(transform.forward, from);
            float side = Vector3.Dot(transform.right, from);

            int direction;

            // Спереди и сзади считаем по продольной оси, и только если удар
            // пришёл ближе к оси, чем к борту: иначе зверь вздрагивал бы
            // «назад» от удара сбоку-сзади, а это читается как промах.
            if (Mathf.Abs(ahead) >= Mathf.Abs(side)) direction = ahead > 0f ? 0 : 1;
            else direction = side < 0f ? 2 : 3;

            animator.SetInteger(HitDirHash, direction);
        }

        private void Update()
        {
            if (animator == null) return;

            // Берём фактическую скорость, а не желаемую: при обходе препятствия
            // и на поворотах она отличается, и анимация должна следовать за тем,
            // что происходит на экране, а не за намерением.
            // С физической капсулой скорость спрашиваем у неё: агент
            // при выключенном updatePosition своей velocity не ведёт,
            // и герой ехал бы по земле в позе стоя.
            float speed = motor != null ? motor.Speed : agent.velocity.magnitude;

            // Упёрся в препятствие — всё равно бежит.
            //
            // Павлон 03.09.2026 разобрал это по WoW: там герой, упершись в
            // камень, продолжает перебирать ногами, а стоит чуть повернуть
            // камеру — бежит вдоль преграды. У нас же он замирал столбом:
            // агент не может двигаться, фактическая скорость ноль, дерево
            // движения показывает стойку.
            //
            // Спрашиваем ЖЕЛАЕМУЮ скорость: агент продолжает хотеть идти,
            // пока путь не пройден, и это желание и есть то, что должны
            // показывать ноги. Берём наибольшее из двух — тогда на поворотах
            // и обходах анимация по-прежнему следует за настоящим движением,
            // а замирает только там, где герой и правда никуда не собрался.
            // Условие «мотора нет» стояло здесь по недосмотру и обнуляло всю
            // правку: мотор у героя ЕСТЬ с 01.09.2026, физическая капсула —
            // и ветка не выполнялась ни разу. Отсюда доклад Павлона
            // 03.09.2026: «бег при упоре не заработал, герой замирает».
            // Спрашивать желаемую скорость надо как раз при моторе: это он
            // упирается в коллайдер и гасит фактическую до нуля.
            if (agent.hasPath && !agent.isStopped)
                speed = Mathf.Max(speed, agent.desiredVelocity.magnitude);

            // Клавиши двигают героя мимо пути агента, и его собственная
            // скорость при этом остаётся нулевой. Спрашиваем у того, кто
            // ведёт, иначе на WASD персонаж скользит по земле стоя.
            if (keys == null) keys = GetComponent<KeyboardMove>();
            if (keys != null && keys.IsSteering) speed = keys.Speed;
            if (speed < idleThreshold) speed = 0f;

            // Разгон сглаживаем мягко, торможение — резко. Асимметрия здесь не
            // косметика: пока «скорость» медленно сползает к нулю, аниматор
            // считает персонажа бегущим и отменяет начатый удар.
            float rate = speed < smoothedSpeed ? stopSmooth : speedSmooth;

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-rate * Time.deltaTime));

            // Дожимаем до нуля: остаточные сотые доли всё равно читаются
            // деревом смешивания как «чуть-чуть идёт», и стойка подрагивает.
            if (speed <= 0f && smoothedSpeed < 0.05f) smoothedSpeed = 0f;

            animator.SetFloat(SpeedHash, smoothedSpeed);

            // Приземление на бегу играем быстрее, а не обрезаем.
            //
            // Клип рассчитан на остановку: присед и разгибание занимают
            // почти секунду, и всё это время герой едет по земле. Обрезать
            // нельзя — пропадает разгибание. Ускоряем: обе фазы на месте,
            // просто проходят вдвое быстрее.
            animator.SetFloat(LandSpeedHash, smoothedSpeed > 0.15f ? 2f : 1f);

            FeedRichAnimator();
        }

        /// <summary>
        /// Кормит параметры, которые понимают только богатые наборы.
        ///
        /// Боевая стойка и боковое смещение есть у босса-кабана — набор их
        /// рисовал, а мы до 02.09.2026 не пользовались. Павлон: «что-то и у
        /// кабана не увидел новых анимаций» — так и было: контроллер их умел,
        /// а игра ему об этом не сообщала.
        ///
        /// У кого таких параметров нет — проверка `Has` возвращает false, и
        /// весь метод стоит одного сравнения на кадр.
        /// </summary>
        private void FeedRichAnimator()
        {
            if (hasCombatFlag)
            {
                if (targets == null) targets = GetComponent<IsoRPG.Combat.TargetSelector>();

                bool fighting = targets != null && targets.Current != null;
                animator.SetBool(InCombatHash, fighting);
            }

            // Скорость разворота. Считается по собственному курсу, а не по
            // агенту: зверь доворачивается и стоя на месте, а у агента при
            // нулевой скорости и вектор нулевой.
            if (hasTurn)
            {
                float yaw = transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(lastYaw, yaw);
                lastYaw = yaw;

                float instant = Time.deltaTime > 0.0001f ? delta / Time.deltaTime : 0f;

                // Сглаживаем: без этого параметр дёргается на каждом кадре, и
                // поворот на месте мигает между левым и правым клипом.
                turnRate = Mathf.Lerp(turnRate, instant, 1f - Mathf.Exp(-8f * Time.deltaTime));

                animator.SetFloat(TurnHash, turnRate);
            }

            if (!hasStrafe) return;

            // Боковая доля скорости: положительная — идёт вправо от себя.
            // Из неё контроллер выбирает наклон в беге и кружение вокруг цели.
            Vector3 velocity = agent != null && agent.isActiveAndEnabled ? agent.velocity : Vector3.zero;
            velocity.y = 0f;

            float side = velocity.sqrMagnitude > 0.01f
                ? Vector3.Dot(transform.right, velocity)
                : 0f;

            animator.SetFloat(StrafeHash, side);
        }

        /// <summary>
        /// Подогнать длительность анимации удара под ритм боя.
        ///
        /// Скорость атаки — характеристика оружия, а не длина скачанного
        /// клипа. Поэтому клип растягивается или поджимается так, чтобы ровно
        /// заполнить интервал между ударами: без пауз стояния и без наложения
        /// одного замаха на другой.
        /// </summary>
        public void SetActionDuration(float seconds)
        {
            if (animator == null || seconds <= 0.01f) return;

            // В контроллере длительность действия задана постоянной, и скорость
            // считается относительно неё.
            const float baseDuration = 1.3f;

            animator.SetFloat(AttackSpeedHash, baseDuration / seconds);
        }

        /// <summary>Обычная атака. Вызывается боевой системой.</summary>
        /// <summary>
        /// Удар номер N в серии. Ноль — «как раньше», одним состоянием.
        ///
        /// Номер выставляем ДО триггера: переход читает оба условия в один
        /// кадр, и при обратном порядке первый удар всегда уходил бы в
        /// вариант из прошлого замаха.
        /// </summary>
        public void PlayAttack(int variant)
        {
            if (animator == null) return;

            if (variant > 0 && Has(AttackVariantHash, AnimatorControllerParameterType.Int))
                animator.SetInteger(AttackVariantHash, variant);

            animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// В воздухе или нет. Держит фазу зависания, пока герой летит.
        ///
        /// Без флага зависание кончалось по длине клипа, а не по длине
        /// прыжка — и герой в воздухе начинал перебирать ногами.
        /// </summary>
        public void SetAirborne(bool airborne)
        {
            if (animator != null && Has(AirborneHash, AnimatorControllerParameterType.Bool))
                animator.SetBool(AirborneHash, airborne);
        }

        /// <summary>Крадётся или нет. Скрытность — состояние, а не действие.</summary>
        public void SetSneaking(bool sneaking)
        {
            if (animator != null && Has(SneakingHash, AnimatorControllerParameterType.Bool))
                animator.SetBool(SneakingHash, sneaking);
        }

        /// <summary>
        /// Вздрогнуть от попадания. Не чаще, чем длится сама реакция.
        ///
        /// Павлон 01.09.2026: «мелкие кабаны в бою разворачиваются, а босс
        /// после атаки стоит замерев» — и добавил решающее: ломается ТОЛЬКО
        /// когда бьёшь в ответ. То есть виновата не навигация и не кольцо
        /// боя, а вот этот триггер.
        ///
        /// Что происходило. Серия ударов кинжалом идёт чаще, чем длится клип
        /// вздрагивания. Вход в GetHit стоит из Any State с запретом перехода
        /// в себя — но триггер при этом НЕ расходуется, он копится и стреляет
        /// сразу на выходе. Зверь возвращался в GetHit кадр в кадр: босс
        /// вечно проигрывал первые кадры реакции (со стороны — «замер»), у
        /// мелких не играл бег, и разворот шёл голым скольжением.
        ///
        /// Лечится здесь, а не в контроллере: место одно на всех — героя,
        /// зверей, НПС, — и всё, что мы добавим потом. Пауза чуть короче
        /// клипа, чтобы частые попадания всё же читались как серия.
        /// </summary>
        public void PlayHit()
        {
            if (animator == null || !Has(HitHash, AnimatorControllerParameterType.Trigger)) return;

            if (Time.time < nextHitTime) return;

            // Реакция уже идёт — не ставим триггер вовсе. Именно поставленный
            // «в никуда» триггер и копился: перейти в себя состояние не может,
            // а параметр остаётся взведённым до первой возможности.
            if (InFlinch()) return;

            // Свой замах не отменяем.
            //
            // Павлон 01.09.2026 разобрал сам: «моя атака прерывает анимацию
            // его атаки, и он в этот момент замирает». Так и есть — вход во
            // вздрагивание стоит из Any State и сносит любое состояние,
            // включая начатый удар. У босса удар длится 1.27 с и почти
            // никогда не доигрывал.
            //
            // В больших РПГ удар доводится до конца: бойца бьют — он
            // продолжает бить. Вздрог пропускаем, он не важнее замаха.
            if (InAttack()) return;

            nextHitTime = Time.time + FlinchGap;
            animator.SetTrigger(HitHash);
        }

        /// <summary>Играет ли сейчас вздрагивание (или переход в него).</summary>
        private bool InFlinch()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == FlinchStateHash) return true;

            return animator.IsInTransition(0) &&
                   animator.GetNextAnimatorStateInfo(0).shortNameHash == FlinchStateHash;
        }

        /// <summary>Идёт ли собственный удар. У зверей состояние одно, у героя серия из шести.</summary>
        private bool InAttack()
        {
            int now = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            foreach (int hash in AttackStateHashes)
                if (hash == now) return true;

            return false;
        }

        /// <summary>Уклониться.</summary>
        public void PlayDodge()
        {
            if (animator != null && Has(DodgeHash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(DodgeHash);
        }

        /// <summary>
        /// Жест способности. Разные по смыслу: направленный удар, площадь,
        /// усиление. Игрок должен видеть, что нажал разные кнопки.
        /// </summary>
        public void PlayCast(CastKind kind)
        {
            if (animator == null) return;

            int hash = kind switch
            {
                CastKind.Area => CastAOEHash,
                CastKind.Buff => CastBuffHash,
                _ => CastAttackHash,
            };

            if (Has(hash, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hash);
        }

        /// <summary>
        /// Есть ли такой параметр у контроллера.
        ///
        /// Проверка обязательна: этот же компонент висит на волках, кабанах и
        /// лошади, а у их контроллеров боевых параметров нет. Unity на
        /// отсутствующий параметр ругается в консоль каждый кадр — и лог
        /// становится нечитаемым ровно тогда, когда он нужнее всего.
        /// </summary>
        private bool Has(int hash, AnimatorControllerParameterType type)
        {
            if (animator.runtimeAnimatorController == null) return false;

            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == type) return true;

            return false;
        }

        public void PlayAttack()
        {
            if (animator != null) animator.SetTrigger(AttackHash);
        }

        /// <summary>Удар из скрытности — тот самый эффектный добивающий.</summary>
        public void PlayStealthKill()
        {
            if (animator != null) animator.SetTrigger(StealthKillHash);
        }

        /// <summary>
        /// Смерть. Флаг, а не разовый сигнал: из этого состояния не выходят сами.
        ///
        /// Если у набора несколько равнозначных падений — бросаем жребий. У
        /// кабана Malbers их два, и одно и то же падение на десяти кабанах
        /// подряд читается как повтор ролика, а не как смерть.
        /// </summary>
        public void SetDead(bool dead)
        {
            if (animator == null) return;

            if (dead && deathVariants > 1 && Has(DeathVariantHash, AnimatorControllerParameterType.Int))
                animator.SetInteger(DeathVariantHash, Random.Range(1, deathVariants + 1));

            animator.SetBool(DeadHash, dead);
        }

        /// <summary>Сколько равнозначных смертей у этого набора. Ставит задание сборки.</summary>
        public void SetDeathVariants(int count) => deathVariants = count;

        /// <summary>Прыжок. Разовый сигнал: состояние само возвращается в движение.</summary>
        public void PlayJump()
        {
            if (animator != null) animator.SetTrigger(JumpHash);
        }

        /// <summary>Еда: персонаж садится на землю и встаёт, когда закончил.</summary>
        public void SetEating(bool eating)
        {
            if (animator != null) animator.SetBool(EatingHash, eating);
        }
    }
}
