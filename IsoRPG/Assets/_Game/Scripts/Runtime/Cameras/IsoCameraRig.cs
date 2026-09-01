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

        [Tooltip("Насколько градусов поворачивается камера на пиксель движения мыши при зажатой правой.")]
        [SerializeField] private float rotateSensitivity = 0.22f;

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
        /// Поворот камеры вокруг героя зажатой правой кнопкой — по обеим осям.
        ///
        /// Правая кнопка свободна: ходьба и выбор цели живут на левой.
        /// </summary>
        private void ReadRotateInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isPlaying) return;

            if (!mouse.rightButton.isPressed) return;

            // Над окном интерфейса правая кнопка принадлежит окну: иначе
            // попытка закрыть меню утаскивала бы за собой весь мир.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 move = mouse.delta.ReadValue();

            desiredYaw += move.x * rotateSensitivity;

            tilt = Mathf.Clamp(tilt - move.y * tiltSensitivity, tiltMin, tiltMax);
            tiltTouched = true;
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
            bool nowFirstPerson = reach < 1.5f;

            if (nowFirstPerson != firstPerson)
            {
                firstPerson = nowFirstPerson;
                ShowHero(!firstPerson);
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

                // Тридцать пять сантиметров над травой: ниже камера начинает
                // резать землю ближней плоскостью отсечения.
                floor = Mathf.Min(floor, soil + 0.35f);
            }

            if (place.y < floor && !firstPerson)
            {
                place.y = floor;

                // Доворот обратно на героя — ТОЛЬКО когда камера упёрлась в
                // пол не по воле игрока.
                //
                // Здесь я вчера дважды исправил одно и то же место в разные
                // стороны. Опустить пол до земли было верно — камере стало
                // куда съезжать. Но доворот остался безусловным, и как
                // только камера садилась к траве, её разворачивало на грудь
                // героя: с уровня земли это почти горизонталь, и неба всё
                // равно не видно. Заказчик: «камера снова перестала
                // подниматься».
                //
                // Тянет вверх — отдаём его угол: герой уезжает к нижнему
                // краю кадра, и это правильно, он смотрит на небо, а не на
                // себя. Упёрлась сама (склон, обрыв) — доворачиваем, иначе
                // поднятая камера будет глядеть мимо.
                if (lookDown > 0f)
                {
                    Vector3 toTarget = lookAt - place;

                    if (toTarget.sqrMagnitude > 0.01f)
                        rotation = Quaternion.LookRotation(toTarget.normalized);
                }
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

            if (dodgeObstacles && reach > 0.1f)
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

                    if (Physics.SphereCast(eye, 0.35f, direction, out var hit, wanted,
                                           Blockers, QueryTriggerInteraction.Ignore))
                    {
                        // Отступаем от точки касания, иначе камера садится
                        // ровно в стену и та начинает мигать.
                        // Отступ увеличен: при 0.25 край кадра всё равно
                        // задевал камень — ближняя плоскость отсечения
                        // отрезает не по центру, а по всей рамке.
                        float safe = Mathf.Max(hit.distance - 0.3f, 0.8f);
                        place = eye + direction * safe;
                    }
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
