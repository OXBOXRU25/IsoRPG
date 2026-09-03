using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoRPG.Cameras
{
    /// <summary>
    /// Изометрическая камера: ортографическая проекция под фиксированным углом,
    /// плавно следует за целью, зум колесом мыши.
    ///
    /// Все углы и расстояния вынесены в инспектор и работают в режиме
    /// редактирования — можно крутить ползунки и сразу видеть картинку,
    /// не запуская игру.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [Header("Цель слежения")]
        [Tooltip("За кем ходит камера. Обычно игрок.")]
        [SerializeField] private Transform target;

        [Tooltip("Смещение точки, на которую смотрит камера. Поднимаем на половину роста персонажа, иначе он стоит в самом низу кадра.")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);

        [Header("Угол обзора")]
        [Tooltip("Поворот вокруг вертикали. 140 — стартовый вид, выбранный Павлоном 01.09.2026 по кадру из игры (было 50, замер с Albion).")]
        [Range(0f, 360f)]
        [SerializeField] private float yaw = 140f;

        [Tooltip("Насколько градусов поворачивается камера на пиксель движения мыши. Работают обе кнопки.")]
        [SerializeField] private float rotateSensitivity = 0.22f;

        [Tooltip("Как быстро герой доворачивается за камерой под правой кнопкой, градусов в секунду. Столько же у мотора при ходьбе — иначе поворот на бегу и на месте выглядел бы разной вещью.")]
        [SerializeField] private float heroTurnSpeed = 720f;

        [Tooltip("Скорость догона поворота. Больше — резче.")]
        [SerializeField] private float rotateSmooth = 14f;

        [Tooltip("Насколько градусов камера наклоняется на пиксель движения мыши по вертикали.")]
        [SerializeField] private float tiltSensitivity = 0.16f;

        [Tooltip("Куда можно доводить наклон вручную: вверх до неба, вниз почти на макушку.")]
        [SerializeField] private float tiltMin = -12f;
        [SerializeField] private float tiltMax = 55f;

        /// <summary>
        /// Взгляд один: из-за плеча, как в WoW.
        ///
        /// До этого здесь жили три проекции с переключением по V —
        /// изометрия, перспектива «как Diablo 4» и вот эта. Переключатель
        /// был нужен ровно на один вечер: выбрать глазами, а не спорить
        /// словами. Выбор сделан, остальные два убраны вместе с кодом.
        ///
        /// Так и надо поступать с любой примеркой: пока варианты живы, каждый
        /// расчёт в этом файле начинается с «а в каком мы режиме», и половина
        /// правок делается вслепую — не зная, в каком из трёх видов её
        /// проверят. Убрав отвергнутое, мы получили прямой код: одна
        /// дистанция, один наклон, одно правило упора в геометрию.
        /// </summary>
        [Header("Проекция")]
        [Tooltip("Угол обзора. 55–65 — привычный третьему лицу.")]
        [Range(35f, 80f)]
        [SerializeField] private float shoulderFov = 58f;

        [Header("Дистанция и зум")]
        [Tooltip("Дальняя плоскость отсечения считается отсюда. На масштаб не влияет — только на то, что попадает в зону отрисовки.")]
        [SerializeField] private float distance = 30f;

        [Tooltip("Половина высоты кадра в мировых единицах. Это и есть настоящий зум ортографической камеры.")]
        [SerializeField] private float orthoSize = 9f;

        /// <summary>
        /// Ближний предел зума. Отсюда считается дистанция: `зум × 1.6`.
        ///
        /// Стояло 5, то есть ближе восьми метров камера не подъезжала в
        /// принципе — а вид от первого лица включается при полутора. Порог
        /// был недостижим: код на него ссылался, а попасть туда было нельзя.
        /// Ноль-семь даёт дистанцию 1.12 — камера входит в голову, модель
        /// героя гаснет, и получается тот самый вид почти от первого лица.
        /// </summary>
        [SerializeField] private float minOrthoSize = 0.7f;
        [SerializeField] private float maxOrthoSize = 18f;

        [Tooltip("На сколько меняется зум за один щелчок колеса.")]
        [SerializeField] private float zoomStep = 1f;

        [Tooltip("Скорость догона зума. Больше — резче.")]
        [SerializeField] private float zoomSmooth = 12f;

        [Header("Следование")]
        [Tooltip("Скорость догона позиции. Больше — жёстче привязка к цели.")]
        [SerializeField] private float followSmooth = 10f;

        [Tooltip("Мгновенно ставить камеру на место при старте, без наезда из точки ноль.")]
        [SerializeField] private bool snapOnStart = true;

        /// <summary>
        /// Придвигаться ли к герою, когда между ним и камерой что-то есть.
        ///
        /// Выключено по решению Павлона 01.09.2026: «когда иду за деревьями,
        /// камера прыгает на полный зум к герою, хотя она просто не должна
        /// менять ракурс и зум». Замер щупом `cam-block` показал, почему это
        /// случалось так часто: у деревьев Synty коллайдер накрывает крону
        /// целиком — от 1.7 до 6.3 м в поперечнике, — и шаг в сторону заводил
        /// луч в листву.
        ///
        /// Приближение стояло не ради удобства, а чтобы камера не оказалась
        /// ВНУТРИ геометрии — это «камень на весь экран». Крона не страшна,
        /// а скалы и холмы автора сделаны мешами, и в них камера теперь
        /// может войти. Если это вылезет — правильный ответ не возвращать
        /// наезд, а растворять то, что закрыло героя, и включить силуэт
        /// (код силуэта лежит в SilhouetteSetup, проход рендера снят).
        /// </summary>
        [Tooltip("Придвигаться к герою, когда обзор закрыт. Выключено: камера держит дистанцию и ракурс.")]
        [SerializeField] private bool dodgeObstacles;

        private Camera cam;
        private float desiredOrthoSize;
        private float desiredYaw;
        private float tilt;
        private bool tiltTouched;
        private bool firstPerson;
        private Renderer[] heroRenderers;
        private Vector3 smoothedFocus;
        private bool focusInitialised;

        /// <summary>
        /// Что камера считает препятствием.
        ///
        /// Раньше здесь стояло `~0` — «всё подряд», и в препятствия попадали
        /// живые тела. Павлон 01.09.2026: проходишь сквозь вторую лошадь —
        /// камера делает полный зум. Она честно отрабатывала: между героем и
        /// камерой оказался коллайдер лошади, значит надо придвинуться.
        ///
        /// Отбираем СЛОЕМ, а не перебором: слой есть у каждого объекта, стоит
        /// один вызов и не зависит от того, вспомнил ли я всех, кого надо
        /// исключить. Это ММО — проверка идёт каждый кадр у каждого игрока.
        ///
        /// Не препятствие: живые (Characters), вода, небо, трава и мелочь
        /// (Detail), интерфейс и служебные слои. Препятствие — мир: камни,
        /// стволы, стены, земля.
        /// </summary>
        /// <summary>
        /// Ближе этого камера к герою не подъезжает, метры.
        ///
        /// Восемьдесят сантиметров — столько, чтобы ближняя плоскость
        /// отсечения не резала землю прямо перед объективом. Подъезд к герою
        /// нужен, когда игрок уводит камеру к самой траве: без предела она
        /// сошлась бы в точку взгляда и вывернулась наизнанку.
        /// </summary>
        private const float MinReach = 0.8f;

        /// <summary>
        /// С какой дистанции прячем модель героя, метры.
        ///
        /// Полтора — примерно там, где голова перестаёт помещаться в кадр.
        /// Число было вписано в условие прямо посреди кода; вынесено, потому
        /// что спрашивают его теперь в двух местах — до подъезда камеры к
        /// герою и после, и разъехаться им нельзя.
        /// </summary>
        private const float FirstPersonAt = 1.5f;

        /// <summary>
        /// С какой дистанции герой начинает таять, метры.
        ///
        /// Два с половиной — заметно раньше, чем модель закроет обзор.
        /// Растворение обязано УСПЕТЬ пройти: начатое вплотную читается как
        /// мигание, а не как в WoW, где персонаж тает плавно, пока камера
        /// опускается.
        /// </summary>
        private const float FadeFrom = 1.3f;

        /// <summary>
        /// Насколько камера держится выше грунта, метры.
        ///
        /// Здесь стояло 0.35 — «чтобы не резать землю ближней плоскостью», и
        /// для голой земли этого хватало. Но трава у нас по пояс герою, и
        /// камера на такой высоте оказывалась ВНУТРИ неё: Павлон 03.09.2026
        /// увёл камеру вниз и увидел травинки в полэкрана, прочитав это как
        /// «смотрю из-под земли». Замер из игры подтвердил числом: камера на
        /// -2.14 при поле 0.60, то есть ровно на разрешённой высоте.
        ///
        /// Метр двадцать — выше нашей травы и всё ещё низко: герой сверху
        /// остаётся в кадре.
        /// </summary>
        private const float OverGround = 1.2f;

        /// <summary>
        /// Как быстро камера отъезжает обратно, когда препятствие ушло.
        /// Четыре — примерно четверть секунды на возврат: глаз читает это
        /// как движение, а не как скачок.
        /// </summary>
        private const float DodgeReturn = 4f;

        /// <summary>
        /// Дистанция, на которую камера придвинута препятствием.
        /// Ноль и меньше — ещё не считали.
        /// </summary>
        private float dodgedReach;

        /// <summary>Текущая прозрачность героя. Отрицательная — ещё не считали.</summary>
        private float heroAlpha = -1f;

        private MaterialPropertyBlock heroBlock;

        /// <summary>Родные материалы рендереров героя и их прозрачные копии.</summary>
        private Dictionary<Renderer, Material[]> solidMaterials;
        private Dictionary<Renderer, Material[]> fadeMaterials;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        /// <summary>Когда снова можно доложить, что камера ушла ниже пола.</summary>
        private float nextFloorReport;

        private static int blockers = -1;

        private static int Blockers
        {
            get
            {
                if (blockers == -1)
                {
                    blockers = ~(LayerMask.GetMask("Characters", "Water", "Sky", "Detail",
                                                   "UI", "TransparentFX", "Ignore Raycast",
                                                   "Preview", "FX Block Zone"));
                }

                return blockers;
            }
        }

        /// <summary>Назначить цель из кода — нужно сборщику сцены и при смене персонажа.</summary>
        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap) focusInitialised = false;
        }

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            desiredOrthoSize = orthoSize;
            desiredYaw = yaw;
            ApplyProjection();
        }

        private void Start()
        {
            if (snapOnStart) focusInitialised = false;

            // Наклон сбрасываем на автоматический: он зависит от отдаления,
            // и запуск игры должен начинаться с расчётного угла, а не с того,
            // на котором человек бросил камеру в прошлый раз.
            tiltTouched = false;
            tilt = 0f;

            // Пределы зума задаём принудительно.
            //
            // Поля сериализованы, и Unity берёт их ИЗ СЦЕНЫ, а не из кода:
            // поправил значение по умолчанию — в игре ничего не изменилось.
            // На этом мы уже обожглись со сменой взгляда; здесь та же
            // ловушка, и она молчаливая.
            minOrthoSize = 0.7f;
            maxOrthoSize = 18f;

            // Пределы наклона — там же и по той же причине.
            //
            // Вверх было всего 12°, то есть чуть выше горизонта: на дерево в
            // восемьдесят метров посмотреть нельзя, а под ноги — пожалуйста.
            // Даём вверх сорок пять.
            tiltMin = -45f;
            tiltMax = 55f;

            desiredOrthoSize = Mathf.Clamp(desiredOrthoSize, minOrthoSize, maxOrthoSize);
        }

        private void LateUpdate()
        {
            // Пока сцена грузится или выгружается, ходить по иерархии нельзя:
            // Unity бросает InvalidOperationException прямо из LateUpdate.
            // Ловилось на выходе из игры — камера в этот момент доезжала до
            // порога вида от первого лица и звала ShowHero по уже
            // разбираемому герою.
            if (quitting) return;
            if (target != null && !target.gameObject.scene.isLoaded) return;

            if (Application.isPlaying) { ReadZoomInput(); ReadRotateInput(); ReadCameraKeys(); }

            ApplyProjection();
            UpdatePlacement(Application.isPlaying ? Time.deltaTime : 0f);
        }

        private bool quitting;

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        /// <summary>
        /// Поворот камеры вокруг героя — обеими кнопками, но по-разному.
        ///
        /// Раскладка WoW, разобранная Павлоном 03.09.2026: <b>левая</b> —
        /// свободный осмотр, камера едет вокруг героя, а он стоит как стоял,
        /// и его можно разглядеть в лицо. <b>Правая</b> — поворот вместе с
        /// героем: он доворачивается вслед за камерой и остаётся к игроку
        /// спиной, сколько ни крути. У нас до сих пор было вращение только на
        /// правой и без доворота, то есть ровно наоборот: правой можно было
        /// зайти герою в лицо, а левая не делала ничего.
        ///
        /// Разница не косметическая. Левой игрок осматривается, не сбивая
        /// направление бега; правой — целится всем телом сразу, потому что
        /// герой всегда смотрит туда же, куда камера.
        ///
        /// Выбору цели левая кнопка не мешает: цель берётся по нажатию, а
        /// вращение живёт на протяжке — как в образце.
        /// </summary>
        private void ReadRotateInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isPlaying) return;

            bool orbit = mouse.leftButton.isPressed;
            bool steer = mouse.rightButton.isPressed;

            if (!orbit && !steer) return;

            // Над окном интерфейса кнопки принадлежат окну: иначе перетаскивание
            // окна или предмета утаскивало бы за собой весь мир.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 move = mouse.delta.ReadValue();

            desiredYaw += move.x * rotateSensitivity;

            tilt = Mathf.Clamp(tilt - move.y * tiltSensitivity, tiltMin, tiltMax);
            tiltTouched = true;

            // Герой за камерой — только под правой кнопкой.
            //
            // Доворачиваем по фактическому углу камеры, а не по желаемому:
            // желаемый бежит впереди сглаживания, и герой крутился бы чуть
            // быстрее вида, обгоняя его на резком движении мышью. Поворот
            // камеры и есть поворот героя — тот же yaw, потому что камера
            // стоит у него за спиной и смотрит туда же.
            if (!steer || target == null) return;

            target.rotation = Quaternion.RotateTowards(
                target.rotation,
                Quaternion.Euler(0f, yaw, 0f),
                heroTurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Управление камерой с клавиатуры — раскладкой WoW.
        ///
        /// Home/End наклоняют, Insert/Delete вращают, PageUp/PageDown
        /// приближают. У Blizzard это продумано не от щедрости: в бою мышь
        /// занята интерфейсом и целями, а камеру всё равно надо крутить.
        /// Раскладка привычна любому, кто играл, — поэтому берём её как
        /// есть, а не выдумываем свою.
        /// </summary>
        private void ReadCameraKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isPlaying) return;

            float step = 60f * Time.deltaTime;

            if (keyboard.insertKey.isPressed) desiredYaw -= step;
            if (keyboard.deleteKey.isPressed) desiredYaw += step;

            if (keyboard.pageUpKey.isPressed)
                desiredOrthoSize = Mathf.Clamp(desiredOrthoSize - step * 0.15f, minOrthoSize, maxOrthoSize);

            if (keyboard.pageDownKey.isPressed)
                desiredOrthoSize = Mathf.Clamp(desiredOrthoSize + step * 0.15f, minOrthoSize, maxOrthoSize);

            if (keyboard.homeKey.isPressed)
            {
                tilt = Mathf.Clamp(tilt - step, tiltMin, tiltMax);
                tiltTouched = true;
            }

            if (keyboard.endKey.isPressed)
            {
                tilt = Mathf.Clamp(tilt + step, tiltMin, tiltMax);
                tiltTouched = true;
            }
        }

        private void ReadZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            // Над окном интерфейса колесо принадлежит окну.
            //
            // Камера читает колесо напрямую у мыши, а не через систему
            // событий, поэтому список в лавке не может «съесть» прокрутку:
            // крутишь товары — и заодно отъезжает весь мир. Спрашиваем сами,
            // не стоит ли указатель над интерфейсом.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Колесо отдаёт 120 за щелчок на большинстве мышей, но не на всех —
            // поэтому берём только знак, иначе зум прыгает на разном железе.
            desiredOrthoSize = Mathf.Clamp(
                desiredOrthoSize - Mathf.Sign(scroll) * zoomStep,
                minOrthoSize,
                maxOrthoSize);
        }

        private void ApplyProjection()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            cam.orthographic = false;
            cam.fieldOfView = shoulderFov;

            if (Application.isPlaying)
            {
                orthoSize = Mathf.Lerp(orthoSize, desiredOrthoSize, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));
            }
            else
            {
                // В редакторе ползунок работает напрямую, без сглаживания.
                desiredOrthoSize = orthoSize;
            }

            // orthoSize остался нашим «зумом»: колесо крутит его, а из него
            // считается отдаление камеры. Имя историческое — от ортографии,
            // которой больше нет; смысл теперь «половина высоты кадра у ног
            // героя», и по нему же выведены доли каскадов теней.
            cam.nearClipPlane = 0.1f;

            // Дальняя плоскость должна вмещать КУПОЛ НЕБА, а не только землю.
            //
            // Стояло `distance * 3`, то есть девяносто метров. Для земли
            // хватало с запасом, и число выглядело разумным — пока небо было
            // заливкой цвета. А купол неба это сфера в сотни метров радиусом:
            // при девяноста метрах он отсекается целиком, и вместо неба
            // видно фон камеры. Ровно это заказчик и назвал «серое однотонное
            // небо» — небо было не серым, его не было вовсе.
            //
            // Полторы тысячи с запасом: купол мы ставим примерно на восьмистах.
            cam.farClipPlane = Mathf.Max(distance * 3f, 1500f);
        }

        private void UpdatePlacement(float deltaTime)
        {
            Vector3 focus = target != null
                ? target.position + lookOffset
                : transform.position + transform.forward * distance;

            if (!focusInitialised || deltaTime <= 0f)
            {
                smoothedFocus = focus;
                focusInitialised = true;
            }
            else
            {
                // Экспоненциальное сглаживание: не зависит от частоты кадров,
                // в отличие от наивного Lerp со скоростью на кадр.
                smoothedFocus = Vector3.Lerp(smoothedFocus, focus, 1f - Mathf.Exp(-followSmooth * deltaTime));
            }

            // Догоняем желаемый поворот, а не прыгаем к нему: рывок камеры
            // читается как подёргивание мира, особенно на ортографии, где
            // нет перспективных подсказок о движении.
            if (Application.isPlaying && deltaTime > 0f)
            {
                yaw = Mathf.LerpAngle(yaw, desiredYaw, 1f - Mathf.Exp(-rotateSmooth * deltaTime));
            }
            else
            {
                // В редакторе ползунок угла работает напрямую.
                desiredYaw = yaw;
            }

            // Отдаление и наклон.
            //
            // Наклон зависит от отдаления — как в WoW. Замер по скриншотам:
            // вблизи (≈7 м) камера смотрит почти горизонтально, 10–15°; на
            // максимальном отдалении (≈26 м) задирается до 35–40°.
            // Постоянный наклон ощущается неправильно на обоих концах:
            // вблизи смотришь герою в затылок сверху, вдали — утыкаешься в
            // землю перед ним.
            float reach = Mathf.Clamp(orthoSize * 1.6f, 0f, 26f);

            float lookDown = tiltTouched
                ? tilt
                : Mathf.Lerp(12f, 38f, Mathf.InverseLerp(5f, 26f, reach));

            // Вид от первого лица — когда камера подъехала вплотную.
            //
            // В WoW при полном приближении камера входит в голову, и модель
            // прячется; без этого она закрыла бы весь экран изнутри. Порог
            // в полтора метра — примерно там, где голова героя перестаёт
            // помещаться в кадр.
            bool nowFirstPerson = reach < FirstPersonAt;

            if (nowFirstPerson != firstPerson)
            {
                firstPerson = nowFirstPerson;
                // Видимость ведёт FadeHero: он растворяет героя плавно,
                // а не выключает разом. Флаг оставлен — по нему считается пол.
            }

            Quaternion rotation = Quaternion.Euler(lookDown, yaw, 0f);

            // Куда смотрит центр экрана.
            //
            // Подъём над ногами героя задан ДВАЖДЫ: полем `lookOffset` (1 м)
            // и вот этой константой. Они складывались в 2.6 м — то есть
            // камера целилась на восемьдесят сантиметров выше макушки, и
            // герой уезжал под центр кадра. На дальнем зуме это десятая доля
            // экрана и почти незаметно, на ближнем — половина полуэкрана,
            // потому что кадр там всего полтора метра в высоту.
            //
            // В WoW при том же приближении центр приходится на затылок, а
            // макушка стоит выше центра. Полтора метра суммарно дают ровно
            // это. Правку держать здесь: увеличишь `lookOffset` — поднимется
            // ещё и пол, от которого камера не пускается под землю.
            Vector3 lookAt = smoothedFocus + Vector3.up * 0.5f;

            Vector3 place = lookAt - rotation * Vector3.forward * reach;

            // Камера не ныряет под землю.
            //
            // Наклон вверх водит камеру по дуге вниз, и на близкой
            // дистанции она уходила под поверхность: мир становился виден
            // снизу, сквозь землю. В WoW этого не бывает — там камера
            // упирается в грунт. Прижимаем её к уровню ног героя и
            // доворачиваем, чтобы она всё равно смотрела на него: иначе
            // поднятая камера продолжала бы глядеть мимо.
            // Пол камеры — ЗЕМЛЯ под нею, а не грудь героя.
            //
            // Было `smoothedFocus.y + 0.5`, то есть камере запрещалось
            // опускаться ниже героя. А взгляд вверх работает именно так:
            // камера идёт по дуге ВНИЗ, и чем ниже она встала, тем выше
            // смотрит. С полом на высоте груди опускаться было некуда —
            // отсюда «вниз смотреть можно, вверх нельзя». Вниз, наоборот,
            // уводит камеру по дуге ВВЕРХ, там запрета нет вовсе.
            //
            // В WoW камера в этом положении съезжает почти к траве и
            // продолжает держать героя в кадре: смотришь на него снизу, за
            // спиной небо. Повторяем это: опускаем предел до грунта, а
            // доворот на героя оставляем — он и есть та самая «вовская»
            // хватка за персонажа.
            float floor = smoothedFocus.y + 0.5f;

            var under = Terrain.activeTerrain;

            if (under != null)
            {
                float soil = under.SampleHeight(place) + under.transform.position.y;

                floor = Mathf.Min(floor, soil + OverGround);
            }

            // Пол считаем и по ФИЗИКЕ, а не только по рельефу.
            //
            // Рельеф — это террейн, а стоит герой обычно не на нём: скалы
            // автора, каменные плиты, мостки — отдельные меши со своими
            // коллайдерами. У точки, где Павлон спрыгнул с горы, таких
            // объектов 113, и коллайдеры есть у всех 113 — то есть физика
            // мир знает, а камера её не спрашивала и честно проваливалась
            // сквозь камень к террейну далеко внизу.
            //
            // Один луч на кадр у своего игрока, не поиск по сцене: в ММО
            // это та цена, которую платить можно.
            if (Physics.Raycast(place + Vector3.up * 6f, Vector3.down, out var below,
                                12f, Blockers, QueryTriggerInteraction.Ignore))
            {
                floor = Mathf.Max(floor, below.point.y + OverGround);
            }

            // Замер: пишем в журнал, когда камера всё-таки оказалась ниже
            // пола. Павлон 03.09.2026 на горе (-96, 49) приблизил камеру и
            // смотрел на героя снизу, сквозь землю, — то есть защита не
            // сработала, а причин у этого три и они равновероятны: луч
            // стартовал внутри коллайдера и потому его не увидел; пол взят
            // по террейну, а стоит герой на камне; защита выключена целиком,
            // потому что камера сочлась первым лицом. Числа скажут, какая.
            if (place.y < floor - 0.2f && Time.time > nextFloorReport)
            {
                nextFloorReport = Time.time + 2f;

                Debug.Log("[IsoRPG] камера ниже пола: камера Y " + place.y.ToString("0.00") +
                          ", пол " + floor.ToString("0.00") +
                          ", герой Y " + lookAt.y.ToString("0.00") +
                          ", дистанция " + reach.ToString("0.00") +
                          ", первое лицо " + firstPerson +
                          ", луч вниз " + (Physics.Raycast(place + Vector3.up * 6f, Vector3.down,
                                              12f, Blockers, QueryTriggerInteraction.Ignore)
                                           ? "попал" : "НЕ попал"));
            }

            if (place.y < floor && !firstPerson)
            {
                // Камера не поднимается над своей дугой, а ПОДЪЕЗЖАЕТ к герою.
                //
                // Так это устроено в WoW, и Павлон показал механику кадрами
                // 03.09.2026: «доходит до земли, но не проваливается, а если
                // крутить ещё ниже — начинает приближаться к персонажу».
                //
                // Прежде мы вместо этого задирали камеру по высоте, оставляя
                // её далеко. Угол от такого становится почти горизонтальным,
                // герой уползает за нижний край кадра, и на экране остаётся
                // одно небо — ровно тот снимок, с которого начался разбор.
                // Подъезд же сохраняет угол, который задал игрок: меняется
                // только расстояние.
                //
                // Считается точно, а не подбором. Камера стоит на луче из
                // точки взгляда: place = lookAt - dir * reach, значит
                // place.y = lookAt.y - dir.y * reach. Приравняв высоту к полу,
                // получаем ровно то расстояние, на котором камера садится на
                // пол и ни сантиметром ниже.
                Vector3 dir = rotation * Vector3.forward;

                if (dir.y > 0.001f)
                {
                    float allowed = (lookAt.y - floor) / dir.y;

                    reach = Mathf.Clamp(allowed, MinReach, reach);
                    place = lookAt - dir * reach;
                }
                else
                {
                    // Камера смотрит горизонтально или вверх — подъехать
                    // некуда, дуга здесь почти не меняет высоту. Остаётся
                    // прежний запасной путь: прижать по высоте.
                    place.y = floor;
                }

                // Подъехав к герою вплотную, прячем модель.
                //
                // Решение о первом лице принималось выше, по дистанции ДО
                // подъезда, — и о нём не знало. Камера у самой травы стоит в
                // полуметре от спины: без этой строки экран закрывает
                // затылок, и это именно то, из-за чего в WoW персонаж на
                // близкой камере растворяется.
                firstPerson = reach < FirstPersonAt;
            }

            // Камера упирается в мир и придвигается к герою.
            //
            // Без этого взгляд из-за плеча ломается на первом же дереве:
            // камера залезает внутрь ствола, и вместо игры видишь изнанку
            // геометрии. В WoW камера в такой ситуации подъезжает ближе —
            // и это не удобство, а условие работоспособности вида от
            // третьего лица.
            //
            // Луч толстый (SphereCast), а не тонкий: тонкий проскакивает
            // между ветками и стойками, и камера всё равно оказывается
            // внутри кроны.

            if (reach > 0.1f)
            {
                // Стреляем от ИСТИННОГО положения героя, а не от сглаженной
                // точки взгляда.
                //
                // Здесь и сидела «камера проваливается в камень». Сглаженный
                // фокус догоняет героя с задержкой: прыгнул на камень — герой
                // уже наверху, а точка ещё внизу, ВНУТРИ камня. Физика Unity
                // засчитывает попадание при входе в коллайдер, изнутри его не
                // видит — луч выходил наружу молча, и защита слепла ровно в
                // тот кадр, когда была нужна. Настоящее положение героя внутри
                // камня оказаться не может: он на нём стоит.
                //
                // Две прошлые попытки (двигать точку взгляда; выталкивать
                // камеру наружу циклом CheckSphere) лечили следствие и не
                // помогли — обе сняты.
                Vector3 eye = target != null ? target.position + lookOffset + Vector3.up * 0.5f : lookAt;

                Vector3 back = (place - eye);
                float wanted = back.magnitude;

                if (wanted > 0.01f)
                {
                    Vector3 direction = back / wanted;

                    float want = wanted;

                    if (Physics.SphereCast(eye, 0.35f, direction, out var hit, wanted,
                                           Blockers, QueryTriggerInteraction.Ignore))
                    {
                        // Отступаем от точки касания, иначе камера садится
                        // ровно в стену и та начинает мигать.
                        // Отступ увеличен: при 0.25 край кадра всё равно
                        // задевал камень — ближняя плоскость отсечения
                        // отрезает не по центру, а по всей рамке.
                        want = Mathf.Max(hit.distance - 0.3f, MinReach);
                    }

                    // Придвигаемся мгновенно, отъезжаем плавно.
                    //
                    // Это и есть «камера скользит по стволу», которое Павлон
                    // разобрал по кадрам WoW 03.09.2026. Мгновенно — потому
                    // что запаздывание здесь означает кадр внутри дерева.
                    // Плавно назад — потому что резкий отъезд читается как
                    // рывок: обзор открылся, а картинка прыгнула.
                    //
                    // Без сглаживания механизм и был снят в прошлый раз: за
                    // деревьями камера моталась к герою и обратно каждый шаг.
                    if (dodgedReach <= 0f || want < dodgedReach) dodgedReach = want;
                    else dodgedReach = Mathf.Lerp(dodgedReach, want,
                                                  1f - Mathf.Exp(-DodgeReturn * deltaTime));

                    dodgedReach = Mathf.Min(dodgedReach, wanted);

                    place = eye + direction * dodgedReach;
                }
            }

            // Выталкивания камеры циклом здесь больше нет.
            //
            // Оно стояло как страховка от провала в камень и НЕ помогало:
            // `CheckSphere` проверяет пересечение с поверхностью, а у камней
            // Synty сеточные коллайдеры — «внутри» у треугольной сетки нет,
            // и сфера в пустоте между гранями не считается пересечением.
            // Плюс десять проверок физики в каждом кадре у каждого игрока.
            // Причина оказалась в точке старта луча, и лечится она выше.

            // Растворение считаем от ИТОГОВОЙ дистанции — после подъезда к
            // герою и после обхода препятствий. Считать по желаемой значило
            // бы растворять героя тогда, когда камера до него ещё не
            // добралась, и держать плотным, когда она уже упёрлась в спину.
            FadeHero(Vector3.Distance(place, lookAt));

            transform.SetPositionAndRotation(place, rotation);
        }

        /// <summary>
        /// Прячет и возвращает модель героя.
        ///
        /// Отключаем рендереры, а не сам объект: на объекте висят агент,
        /// бой и ввод, и выключение утащило бы за собой управление —
        /// приближение камеры внезапно парализовало бы персонажа.
        /// </summary>
        private void ShowHero(bool visible)
        {
            if (target == null) return;

            if (heroRenderers == null || heroRenderers.Length == 0)
                heroRenderers = target.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in heroRenderers)
            {
                if (renderer == null) continue;

                // Полоски и подписи над головой не трогаем: они рисуются
                // холстом и от первого лица всё равно не видны.
                if (renderer is SpriteRenderer) continue;

                renderer.enabled = visible;
            }
        }

        /// <summary>
        /// Растворить героя по мере приближения камеры.
        ///
        /// Павлон 03.09.2026 показал кадрами из WoW: камера, опускаясь, не
        /// упирается в спину персонажа — тот постепенно становится
        /// прозрачным, и сквозь него видно мир. Резкое «выключить модель»,
        /// которое было у нас, читается как рывок и оставляет промежуток,
        /// где затылок занимает весь экран.
        ///
        /// Прозрачные копии материалов делаем ОДИН раз и держим: у героя
        /// материалы непрозрачные, а прозрачность в URP — это режим
        /// шейдера, а не одно число, и переключать его каждый кадр значило
        /// бы пересобирать материал по сорок раз в секунду.
        ///
        /// Саму же альфу гоняем блоком свойств: он не создаёт новых
        /// материалов вовсе и потому дёшев — это ММО, у каждого игрока своя
        /// камера и свой герой.
        /// </summary>
        private void FadeHero(float distance)
        {
            if (target == null) return;

            if (heroRenderers == null || heroRenderers.Length == 0)
                heroRenderers = target.GetComponentsInChildren<Renderer>(true);

            heroBlock ??= new MaterialPropertyBlock();

            // Ближе MinReach — героя нет вовсе, дальше FadeFrom — он целый.
            float alpha = Mathf.Clamp01((distance - MinReach) / (FadeFrom - MinReach));

            if (Mathf.Abs(alpha - heroAlpha) < 0.01f) return;

            heroAlpha = alpha;

            bool solid = alpha > 0.99f;

            foreach (var renderer in heroRenderers)
            {
                if (renderer == null || renderer is SpriteRenderer) continue;

                // Совсем прозрачного не рисуем: пустая отрисовка стоит
                // столько же, сколько полная.
                renderer.enabled = alpha > 0.01f;

                if (!renderer.enabled) continue;

                // Оружие не растворяем, а гасим порогом.
                //
                // Прозрачный режим выключает запись глубины, и клинки в нём
                // начинают тонуть за телом: Павлон 03.09.2026 поймал ракурс,
                // где кинжалы есть, а от поворота на сантиметр пропадают.
                // Тело — скелетный меш, оружие — обычный, и это надёжный
                // признак: он не зависит от того, вспомнил ли я все имена
                // клинков, луков и щитов, которые появятся потом.
                if (!(renderer is SkinnedMeshRenderer))
                {
                    renderer.enabled = alpha > 0.5f;
                    continue;
                }

                SetFadeMode(renderer, solid);

                if (solid) continue;

                renderer.GetPropertyBlock(heroBlock);
                heroBlock.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
                renderer.SetPropertyBlock(heroBlock);
            }
        }

        /// <summary>
        /// Переключить рендерер между родными материалами и прозрачными.
        ///
        /// Копии заводятся при первом затухании и живут дальше: создавать их
        /// в Awake значило бы платить за то, чем большинство игроков ни разу
        /// не воспользуется.
        /// </summary>
        private void SetFadeMode(Renderer renderer, bool solid)
        {
            if (solidMaterials == null)
                solidMaterials = new Dictionary<Renderer, Material[]>();

            if (!solidMaterials.TryGetValue(renderer, out var original))
            {
                original = renderer.sharedMaterials;
                solidMaterials[renderer] = original;
            }

            if (solid)
            {
                if (renderer.sharedMaterials != original)
                {
                    renderer.sharedMaterials = original;
                    renderer.SetPropertyBlock(null);
                }

                return;
            }

            if (fadeMaterials == null)
                fadeMaterials = new Dictionary<Renderer, Material[]>();

            if (!fadeMaterials.TryGetValue(renderer, out var faded))
            {
                faded = new Material[original.Length];

                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;

                    var copy = new Material(original[i]);

                    // Полный набор переключателей прозрачного режима URP.
                    // Одного _Surface мало: без ключевого слова и очереди
                    // материал остаётся непрозрачным, и альфа не действует
                    // вовсе — это выглядит как «затухание не работает».
                    copy.SetFloat(SurfaceId, 1f);                 // Transparent
                    copy.SetFloat(BlendId, 0f);                   // Alpha
                    copy.SetFloat(ZWriteId, 0f);
                    copy.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    copy.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    copy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    faded[i] = copy;
                }

                fadeMaterials[renderer] = faded;
            }

            if (renderer.sharedMaterials != faded) renderer.sharedMaterials = faded;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minOrthoSize > maxOrthoSize) minOrthoSize = maxOrthoSize;
            orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
            desiredOrthoSize = orthoSize;
            distance = Mathf.Max(1f, distance);
        }
#endif
    }
}
