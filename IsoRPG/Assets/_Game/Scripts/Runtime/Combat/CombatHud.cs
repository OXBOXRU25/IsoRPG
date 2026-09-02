using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Localization;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Боевой интерфейс: панель игрока слева вверху и панель выбранной цели
    /// рядом с ней. Строит себя целиком в коде — префаб не нужен.
    ///
    /// Почему кодом: интерфейс на этом этапе меняется каждую итерацию, и
    /// править числа в одном файле быстрее, чем ковыряться в дереве объектов.
    /// Когда вид устоится, часть можно будет вынести в префаб.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        // --- Палитра. Взята с наших замеров: тёмная подложка, тёплый акцент ---
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xD0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        // Вовская конвенция: своё здоровье зелёное, чужое красное. Цвет
        // говорит «свой или враг» раньше, чем игрок успевает прочитать имя.
        private static readonly Color AllyHealthColor = new Color32(0x4E, 0xA8, 0x3C, 0xFF);
        private static readonly Color AllyHealthBack = new Color32(0x16, 0x24, 0x12, 0xFF);
        private static readonly Color HealthColor = new Color32(0xB8, 0x3A, 0x32, 0xFF);

        /// <summary>
        /// Тонировка полоски врага.
        ///
        /// Красим ЖЁЛТУЮ картинку, а не зелёную. Павлон 01.09.2026: «цвет хп
        /// какой-то тёмный, а не красный» — так и вышло: в зелёной заливке
        /// красного канала почти нет, и умножение на красный давало бурый.
        /// В жёлтой красный канал полный, под этой тонировкой она становится
        /// чистым красным и сохраняет стеклянный блик.
        /// </summary>
        private static readonly Color EnemyFill = new Color32(0xFF, 0x46, 0x38, 0xFF);

        /// <summary>Чем залито гнездо портрета, когда портрета нет.</summary>
        private static readonly Color PortraitEmpty = new Color32(0x1A, 0x18, 0x15, 0xFF);
        private static readonly Color HealthBack = new Color32(0x2A, 0x14, 0x12, 0xFF);
        private static readonly Color PortraitColor = new Color32(0x4A, 0x42, 0x36, 0xFF);
        private static readonly Color EnergyColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        /// <summary>
        /// Насколько заливка утоплена в жёлоб подложки.
        ///
        /// Четыре точки: стенка жёлоба в картинке около двенадцати пикселей
        /// при высоте 124, то есть примерно десятая часть; наши полоски
        /// высотой около сорока, десятая — четыре.
        /// </summary>
        private const float BarInset = 4f;
        private static readonly Color EnergyBack = new Color32(0x2C, 0x24, 0x10, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color TextDim = new Color32(0xA8, 0xA0, 0x90, 0xFF);

        // --- Геометрия. Отступ от края одинаковый везде: один токен, не три ---
        private const float ScreenMargin = 18f;
        /// <summary>
        /// Размер панели — ОДИН на все три.
        ///
        /// Раньше у каждой был свой: 300 у героя, 420 у врага, 400 у мирного.
        /// Так вышло потому, что картинки пришли разной пропорции, и ширину
        /// подбирали, чтобы круги портретов совпали. На экране это читалось
        /// сразу: три панели рядом, и все разной величины.
        ///
        /// Новые картинки нарезаны в один размер — 1689 на 706, — поэтому и
        /// панель теперь одна. Высота выводится из пропорции: задавать её
        /// отдельно нельзя, рамка растянется и торцы поплывут.
        /// </summary>
        /// Триста четыре — это прежние 380 минус пятая часть. На 380 панели
        /// съедали верх экрана и спорили с полосой умений; всё внутри них —
        /// круг портрета, полосы, плашка имени — задано ДОЛЯМИ от этой
        /// ширины, поэтому уменьшение идёт одним числом и разъехаться нечему.
        private const float PanelWidth = 304f;
        private const float PanelHeight = PanelWidth * 706f / 1689f;

        /// <summary>
        /// Где внутри рамки лежат гнёзда — доли от её размера.
        ///
        /// Сняты замером с самих картинок и проверены наложением: цветной
        /// прямоугольник рисуется поверх панели, и если он лёг мимо жёлоба,
        /// это видно. Прежние числа снимались на глаз по сетке и промахивались
        /// на несколько процентов — полосы висели над жёлобами, а не в них.
        /// </summary>
        private const float PortraitCenterX = 0.208f;
        private const float PortraitCenterY = 0.491f;
        private const float PortraitDiameter = 0.236f;   // от ширины рамки

        /// <summary>
        /// Насколько рамка выступает за портрет — ровно на свою кромку с
        /// каждой стороны, в долях ширины панели.
        ///
        /// Держим в долях, потому что от этого числа считается, где начинаются
        /// полоски: рамка станет тоньше — полоски подойдут ближе сами.
        /// </summary>
        private const float PortraitFrameOut = PortraitWall / PanelWidth;

        /// <summary>Зазор между рамкой портрета и полосками, в долях ширины панели.</summary>
        private const float PortraitGap = 0.028f;

        /// <summary>
        /// Три блока справа от портрета — имя, здоровье, выносливость —
        /// стоят единым столбцом ровно по высоте рамки портрета.
        ///
        /// Павлон 01.09.2026: «надо, чтобы три бара справа были равны по
        /// ширине, с одинаковыми небольшими отступами, и параллельны верху и
        /// низу рамки с портретом». Раньше высоты и отступы стояли числами
        /// порознь, и столбец не совпадал с рамкой ни сверху, ни снизу.
        ///
        /// Считаем от самой рамки: она занимает по вертикали половину своего
        /// размера в каждую сторону от центра портрета. Поменяется портрет —
        /// столбец подстроится сам.
        /// </summary>
        private const float PortraitFrameHalf =
            (PanelWidth * PortraitDiameter * 0.5f + PortraitWall) / PanelHeight;

        private const float BlockTop = PortraitCenterY - PortraitFrameHalf;
        private const float BlockBottom = PortraitCenterY + PortraitFrameHalf;

        /// <summary>Зазор между блоками — четыре точки, одинаковый для обоих.</summary>
        private const float BlockGap = 4f / PanelHeight;

        private const float BlockHeight = (BlockBottom - BlockTop - BlockGap * 2f) / 3f;

        private const float NamePlateFrom = BlockTop;
        private const float NamePlateTo = BlockTop + BlockHeight;

        /// <summary>Имя героя. Задал Павлон 01.09.2026.</summary>
        private const string HeroName = "Шико";

        /// <summary>
        /// Цвет имени — вовский жёлтый, один на героя, мобов и мирных.
        ///
        /// Один цвет на всех намеренно: жёлтый здесь означает «имя», а не
        /// отношение к игроку. Кто свой, кто чужой, говорит цвет полоски.
        /// </summary>
        private static readonly Color NameColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        /// <summary>
        /// Толщина каменной кромки рамки портрета на экране.
        ///
        /// В картинке она 26 точек, а рисуется ужатой втрое (как у автора
        /// набора) — отсюда девять. По этому числу портрет разворачивается во
        /// всё внутреннее окно рамки: Павлон 01.09.2026 «рамки большие, больше
        /// портретов» — портрет сидел в своём прежнем размере и оставлял
        /// вокруг себя пустое поле.
        /// </summary>
        private const float PortraitWall = 26f / PortraitSlice;

        /// <summary>
        /// Во сколько раз ужата рамка портрета. Больше, чем у гнёзд приёмов:
        /// Павлон 01.09.2026 «немного не дотянул рамку, надо ещё уже».
        /// При четырёх кромка выходит в шесть с половиной точек.
        /// </summary>
        private const float PortraitSlice = 4f;

        /// <summary>Насколько портрет крупнее своего гнезда, чтобы прилегать к кромке рамки.</summary>
        private const float PortraitInner = 1.05f;

        /// <summary>
        /// Полоски начинаются ЗА рамкой портрета, а не поверх неё.
        ///
        /// Павлон 01.09.2026: «бары заходят на портрет, надо отодвинуть». Так
        /// и было: правый край портрета приходился на 0.326 ширины, а полоски
        /// начинались с 0.300 — наезд в восемь точек. Теперь начало считается
        /// от портрета и его рамки, а не задаётся числом: поменяется размер
        /// портрета — полоски отойдут сами.
        /// </summary>
        private const float BarsFrom =
            PortraitCenterX + PortraitDiameter * 0.5f + PortraitFrameOut + PortraitGap;

        private const float BarsTo = 0.980f;
        /// <summary>
        /// Полоски героя: здоровье сверху, выносливость под ним.
        ///
        /// Между ними зазор в четыре сотых высоты — примерно пять точек.
        /// Было восемнадцать: полоски стояли по краям панели, которой больше
        /// нет, и висели порознь. Павлон 01.09.2026: «бары хп и стамины
        /// сдвинь ближе друг к другу» — вдвоём они читаются как один блок
        /// рядом с портретом.
        /// </summary>
        /// Полоски — второй и третий блоки того же столбца, что и имя.
        private const float TopBarFrom = NamePlateTo + BlockGap;
        private const float TopBarTo = TopBarFrom + BlockHeight;
        private const float LowBarFrom = TopBarTo + BlockGap;
        private const float LowBarTo = LowBarFrom + BlockHeight;

        /// <summary>Панель цели: полоса одна, лежит правее портрета.</summary>
        private const float EnemyPanelWidth = PanelWidth;
        private const float EnemyPanelHeight = PanelHeight;

        private const float EnemyPortraitCenterX = 0.202f;
        private const float EnemyPortraitCenterY = 0.469f;
        private const float EnemyPortraitDiameter = 0.237f;

        /// <summary>
        /// Панель мирного: вместо жёлоба табличка под имя.
        /// </summary>
        private const float NeutralPanelWidth = PanelWidth;
        private const float NeutralPanelHeight = PanelHeight;

        private const float NeutralPortraitCenterX = 0.206f;
        private const float NeutralPortraitCenterY = 0.496f;
        private const float NeutralPortraitDiameter = 0.228f;

        private const float NeutralPlateFrom =
            NeutralPortraitCenterX + NeutralPortraitDiameter * 0.5f + PortraitFrameOut + PortraitGap;

        private const float NeutralPlateTo = 0.980f;
        private const float NeutralPlateTop = 0.330f;
        private const float NeutralPlateBottom = 0.640f;

        private const float EnemyBarFrom =
            EnemyPortraitCenterX + EnemyPortraitDiameter * 0.5f + PortraitFrameOut + PortraitGap;

        private const float EnemyBarTo = 0.980f;

        /// <summary>
        /// Столбец у цели устроен как у героя: три блока по высоте рамки
        /// портрета. Только третий занят не выносливостью, а комбо.
        ///
        /// Павлон 01.09.2026: «блок с именем и хп должны быть такого же
        /// размера, как у героя, и выровнены по верху рамки моба, а под ними
        /// вместо бара со стаминой — комбо-поинты».
        /// </summary>
        private const float EnemyFrameHalf =
            (PanelWidth * EnemyPortraitDiameter * 0.5f + PortraitWall) / PanelHeight;

        private const float EnemyBlockTop = EnemyPortraitCenterY - EnemyFrameHalf;

        private const float EnemyNameFrom = EnemyBlockTop;
        private const float EnemyNameTo = EnemyBlockTop + BlockHeight;

        /// <summary>Комбо — третий блок столбца, там же, где у героя выносливость.</summary>
        private const float EnemyComboFrom = EnemyNameTo + BlockGap * 2f + BlockHeight;
        private const float EnemyComboTo = EnemyComboFrom + BlockHeight;
        /// <summary>Полоска цели — второй блок столбца.</summary>
        private const float EnemyBarTop = EnemyNameTo + BlockGap;
        private const float EnemyBarBottom = EnemyBarTop + BlockHeight;
        private const float PanelGap = 12f;
        private const float BarHeight = 20f;      // толще: в WoW полоска — главный элемент панели
        private const float BarGap = 4f;
        private const float InnerPad = 7f;

        // Портрет квадратный и занимает высоту панели за вычетом полей.
        // Без него панель выглядит отладочной плашкой, а не интерфейсом игры.
        private const float PortraitSize = PanelHeight - InnerPad * 2f;
        private const float BarsLeft = InnerPad * 2f + PortraitSize;

        // Панель способностей
        /// <summary>Общая плашка под иконками способностей.</summary>
        private static readonly Color SlotPlate = new Color32(0x2E, 0x2A, 0x22, 0xF0);

        /// <summary>
        /// Гнездо приёма. Пятьдесят восемь вместо сорока восьми.
        ///
        /// Павлон 01.09.2026: «рамки широкие и маленькие». Так и выходило:
        /// у каменной оправы кромка 26 точек с каждой стороны, а гнездо было
        /// 48 — углы почти смыкались, и от рисунка внутри не оставалось
        /// ничего. Гнездо крупнее, а сама оправа ужата множителем (см.
        /// <see cref="SlotSlice"/>), и кромка выходит примерно в девять точек.
        /// </summary>
        private const float SlotSize = 58f;
        private const float SlotGap = 6f;

        /// <summary>Во сколько раз ужата каменная оправа гнезда. В нормативе Synty множители 1.5–3.</summary>
        private const float SlotSlice = 3f;

        /// <summary>
        /// На сколько подложка гнезда меньше самого гнезда.
        ///
        /// У подложки скруглённые углы чуть шире, чем внутреннее окно рамки,
        /// и в четырёх углах она вылезала наружу тёмными язычками. Павлон
        /// 01.09.2026: «подложку уменьшить буквально на 2%». Два процента от
        /// 58 — чуть больше точки с каждой стороны.
        /// </summary>
        private const float SlotBackInset = SlotSize * 0.02f;

        private static readonly Color ComboEmpty = new Color32(0x2E, 0x2A, 0x22, 0xFF);
        private static readonly Color ComboFull = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        /// <summary>
        /// Ширина панели по наведению — 240 против 304 у боевых.
        ///
        /// Меньше намеренно: эта панель ходит за курсором и живёт полсекунды,
        /// поэтому обязана закрывать собой как можно меньше поля боя. Высота,
        /// как и у остальных, выводится из пропорции картинки 1689 на 706 —
        /// задавать её отдельно нельзя, рамка растянется.
        /// </summary>
        private const float HoverPanelWidth = 240f;
        private const float HoverPanelHeight = HoverPanelWidth * 706f / 1689f;

        /// <summary>Отступ панели от кончика курсора.</summary>
        private const float HoverCursorGap = 18f;

        /// <summary>Цвет строки-подсказки под панелью. Тусклее имени.</summary>
        private static readonly Color HintColor = new Color32(0xC8, 0xB0, 0x78, 0xFF);
        private static readonly Color CooldownVeil = new Color(0f, 0f, 0f, 0.65f);

        // Полоска опыта: тонкая, в самом низу экрана. Она растёт часами и
        // не должна отвлекать от боя.
        private const float ExpBarHeight = 10f;
        private static readonly Color ExpColor = new Color32(0x7A, 0x5A, 0xB8, 0xFF);
        private static readonly Color ExpBack = new Color32(0x1A, 0x16, 0x22, 0xFF);

        [SerializeField] private TargetSelector targets;
        [SerializeField] private Health playerHealth;
        [SerializeField] private ResourcePool playerEnergy;
        [SerializeField] private ComboPoints combo;
        [SerializeField] private AbilityBook abilities;
        [SerializeField] private DefenseStats playerDefense;
        [SerializeField] private Experience playerExperience;

        private Font font;

        private RectTransform playerHealthFill;
        private Text playerHealthText;
        private Text playerNameText;
        private Image playerPortrait;
        private Image targetPortrait;
        private GameObject neutralPanel;
        private Image neutralPortrait;
        private Text neutralNameText;

        private GameObject hoverPanel;
        private RectTransform hoverPanelRect;
        private Image hoverFrame;
        private Image hoverPortrait;
        private Text hoverNameText;
        private Text hoverHintText;
        private RectTransform hoverHealthFill;
        private Text hoverHealthText;
        private GameObject hoverHealthHost;
        private Canvas hudCanvas;

        private RectTransform playerEnergyFill;
        private Text playerEnergyText;

        private GameObject targetPanel;
        private RectTransform targetHealthFill;
        private Text targetHealthText;
        private Text targetNameText;

        private RectTransform expFill;
        private Text expText;

        private Health boundTargetHealth;

        private readonly System.Collections.Generic.List<Image> comboDots =
            new System.Collections.Generic.List<Image>();

        /// <summary>Корень интерфейса и текущая панель — нужны, чтобы
        /// пересобрать её при смене режима.</summary>
        private RectTransform hudRoot;
        private GameObject abilityBarObject;

        /// <summary>Полоса поедания: корень, заливка и подпись.</summary>
        private GameObject eatBarObject;
        private RectTransform eatFill;
        private Text eatLabel;

        private IsoRPG.Items.FoodConsumer food;

        private readonly System.Collections.Generic.List<AbilitySlot> slots =
            new System.Collections.Generic.List<AbilitySlot>();

        /// <summary>Одна кнопка на панели способностей.</summary>
        private struct AbilitySlot
        {
            public AbilityDefinition ability;
            public Image icon;
            public Image cooldownVeil;   // тёмная плашка поверх иконки во время отката
            public Text cooldownText;
        }

        private void Awake()
        {
            if (targets == null) targets = GetComponentInParent<TargetSelector>();
            if (playerHealth == null) playerHealth = GetComponentInParent<Health>();
            if (playerEnergy == null) playerEnergy = GetComponentInParent<ResourcePool>();
            if (combo == null) combo = GetComponentInParent<ComboPoints>();
            if (abilities == null) abilities = GetComponentInParent<AbilityBook>();
            if (playerDefense == null) playerDefense = GetComponentInParent<DefenseStats>();
            if (playerExperience == null) playerExperience = GetComponentInParent<Experience>();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildHud();
        }

        private void OnEnable()
        {
            // Имя героя с уровнем собрано из шаблона и числа, поэтому само
            // себя не переведёт: пересобираем при смене языка.
            Loc.Changed += RelabelPlayer;

            if (playerHealth != null)
            {
                playerHealth.Changed += OnPlayerHealthChanged;
                OnPlayerHealthChanged(playerHealth.Current, playerHealth.Max);
            }

            if (playerEnergy != null)
            {
                playerEnergy.Changed += OnEnergyChanged;
                OnEnergyChanged(playerEnergy.Current, playerEnergy.Max);
            }

            if (combo != null)
            {
                combo.Changed += OnComboChanged;
                OnComboChanged(combo.Points, combo.MaxPoints);
            }

            if (playerExperience != null)
            {
                playerExperience.Changed += OnExperienceChanged;
                playerExperience.LevelUp += OnLevelUp;
                OnExperienceChanged(playerExperience.Current, playerExperience.ToNextLevel);
                ShowPlayerLevel(playerExperience.Level);
            }

            if (targets != null)
            {
                targets.TargetChanged += OnTargetChanged;
                OnTargetChanged(targets.Current);
            }

            if (abilities != null) abilities.BarChanged += OnBarChanged;

            // Портрет игрока не меняется за игру, поэтому ставится один раз.
            //
            // Только если рисованного нет: этот код выполняется ПОСЛЕ сборки
            // панели и раньше затирал нарисованное лицо плоской иконкой из
            // компонента существа. Симптом был обманчивый — в панели висел
            // старый значок, хотя и файл на месте, и загружался он исправно,
            // и в логе ни слова. У целей приоритет расставлен верно
            // (Portraits.For ?? target.Portrait), а здесь стояли два
            // присваивания подряд, и побеждало второе.
            var self = GetComponent<Targetable>();

            if (self != null && playerPortrait != null && self.Portrait != null
                && playerPortrait.sprite == null)
            {
                playerPortrait.sprite = self.Portrait;
                playerPortrait.enabled = true;
            }
        }

        /// <summary>
        /// Панель сменилась — пересобираем кнопки.
        ///
        /// Вход в скрытность меняет не одну кнопку, а весь набор доступного,
        /// и показать это надо так же: игрок должен видеть ровно то, чем
        /// может воспользоваться сейчас.
        /// </summary>
        private void OnBarChanged()
        {
            if (hudRoot == null) return;

            BuildAbilityBar(hudRoot);
            BuildEatBar(hudRoot);

            // Состояния кнопок (откаты, нехватка энергии) обновляются каждый
            // кадр — достаточно дать им один проход сразу, чтобы новая панель
            // не мигнула активной, если приём на самом деле не готов.
            UpdateCooldowns();
        }

        private void OnDisable()
        {
            Loc.Changed -= RelabelPlayer;
            if (abilities != null) abilities.BarChanged -= OnBarChanged;
            if (playerHealth != null) playerHealth.Changed -= OnPlayerHealthChanged;
            if (playerEnergy != null) playerEnergy.Changed -= OnEnergyChanged;
            if (combo != null) combo.Changed -= OnComboChanged;
            if (playerExperience != null)
            {
                playerExperience.Changed -= OnExperienceChanged;
                playerExperience.LevelUp -= OnLevelUp;
            }
            if (targets != null) targets.TargetChanged -= OnTargetChanged;
            BindTargetHealth(null);
        }

        private void Update()
        {
            UpdateCooldowns();
            UpdateEatBar();
        }

        /// <summary>
        /// Показывает, сколько осталось есть.
        ///
        /// Без полосы поедание выглядит как зависание: персонаж сел, ничего
        /// не происходит, и непонятно, идёт ли процесс и когда кончится.
        /// Полоса ставится над панелью способностей — там же, где игрок
        /// и так следит за откатами.
        /// </summary>
        private void UpdateEatBar()
        {
            if (eatBarObject == null) return;

            if (food == null) food = GetComponentInParent<IsoRPG.Items.FoodConsumer>();

            bool eating = food != null && food.IsEating;

            if (eatBarObject.activeSelf != eating) eatBarObject.SetActive(eating);
            if (!eating) return;

            if (eatFill != null)
            {
                // Через масштаб, а не ширину: у полосы точка опоры слева,
                // и масштабирование не заставляет пересобирать раскладку
                // каждый кадр.
                eatFill.localScale = new Vector3(food.Progress, 1f, 1f);
            }

            if (eatLabel != null)
            {
                string name = food.Current != null ? food.Current.displayName : "Ест";
                eatLabel.text = Loc.T(name) + "  " + Loc.F("{0} с", food.Remaining.ToString("0.0"));
            }
        }

        // ------------------------------------------------------------------
        // Сборка
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            var canvasGo = new GameObject("CombatHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas = canvas;

            // Масштабирование по ширине: интерфейс должен занимать одну и ту же
            // долю экрана и на ноутбуке, и на большом мониторе.
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Тянемся за шириной, а не за средним между шириной и высотой.
            //
            // При среднем масштаб выходит дробным на любом экране, который не
            // 16:9: на 1920x1200 это 1.054, и шрифт растеризуется между
            // пикселями — надписи выглядят размытыми, особенно мелкие.
            // По ширине на том же экране масштаб ровно 1.0, и текст чёткий.
            scaler.matchWidthOrHeight = 0f;

            var root = (RectTransform)canvasGo.transform;

            float barsWidth = PanelWidth - BarsLeft - InnerPad;
            float nameY = -InnerPad;
            float healthY = nameY - 17f;
            float secondY = healthY - BarHeight - BarGap;

            // --- Панель игрока ---
            var player = CreateFramedPanel(root, "PlayerPanel", "UI/Frame_Player",
                new Vector2(ScreenMargin, -ScreenMargin));

            playerPortrait = CreateSlotPortrait(player);

            var heroFace = Portraits.For("Разбойник");

            if (heroFace != null)
            {
                playerPortrait.sprite = heroFace;
                playerPortrait.enabled = true;
            }

            // Имя героя на своей плашке — по образцу WoW.
            //
            // Павлон 01.09.2026: «у героя ника вообще нет, должен быть Шико».
            // Раньше имя не показывали намеренно: игрок и так знает, кем
            // играет. Но рядом с панелью цели, где имя есть, пустое место
            // читается как недоделка, а не как решение.
            playerNameText = CreateNamePlate(player, PanelWidth, PanelHeight,
                                             BarsFrom, BarsTo, NamePlateFrom, NamePlateTo);
            playerNameText.text = HeroName;

            // Имени героя над панелью нет намеренно.
            //
            // Оно висело отдельной строкой НАД рамкой, ни к чему не привязанной
            // — и читалось не частью панели, а подписью, забытой поверх неё.
            // Игрок и так знает, кто он; уровень виден в окне персонажа.
            //
            // Поле оставлено: обновление уровня проверяет его на null и молча
            // пропускает, так что ломать там нечего.

            var healthBar = CreateGrooveBar(player, "Health", AllyHealthColor,
                                            TopBarFrom, TopBarTo, fillSprite: "UI/Bar_Fill_Health");
            playerHealthFill = healthBar.fill;
            playerHealthText = healthBar.label;

            var energyBar = CreateGrooveBar(player, "Energy", EnergyColor,
                                            LowBarFrom, LowBarTo, fillSprite: "UI/Bar_Fill_Stamina");
            playerEnergyFill = energyBar.fill;
            playerEnergyText = energyBar.label;

            // Цифры на энергии белые, как и на здоровье.
            //
            // Тёмными они стояли под плоскую жёлтую заливку: на ней светлый
            // текст пропадал. У стеклянной картинки середина насыщенная и
            // тёмная по краям, и белый на ней читается — а два разных цвета
            // цифр на соседних полосках выглядели как ошибка. Павлон
            // 01.09.2026: «где бар стамины, сделай цифры тоже белого цвета».
            playerEnergyText.color = TextColor;

            // --- Панель цели: справа от панели игрока, скрыта без цели ---
            var target = CreateFramedPanel(root, "TargetPanel", "UI/Frame_Enemy",
                new Vector2(ScreenMargin + PanelWidth + PanelGap, -ScreenMargin),
                EnemyPanelWidth, EnemyPanelHeight);
            targetPanel = target.gameObject;

            targetPortrait = CreateSlotPortrait(target, EnemyPanelWidth, EnemyPanelHeight,
                                                EnemyPortraitCenterX, EnemyPortraitCenterY,
                                                EnemyPortraitDiameter);

            // Имя цели — на такой же плашке, как у героя, прямо над полоской.
            // Раньше висело в воздухе над панелью, которой больше нет.
            targetNameText = CreateNamePlate(target, EnemyPanelWidth, EnemyPanelHeight,
                                             EnemyBarFrom, EnemyBarTo,
                                             EnemyNameFrom, EnemyNameTo);

            // Полоска цели — та же картинка, что у героя, покрашенная в
            // красный. Решение Павлона 01.09.2026: «пока подумаем, как
            // перекрасить нормально в красный, не теряя текстуру». Плоский
            // прямоугольник рядом со стеклянной полоской героя выглядел
            // заглушкой, так что временно лучше тонировка.
            var targetBar = CreateGrooveBar(target, "Health", EnemyFill,
                                            EnemyBarTop, EnemyBarBottom,
                                            EnemyBarFrom, EnemyBarTo,
                                            fillSprite: "UI/Bar_Fill_Stamina",
                                            keepTint: true);
            targetHealthFill = targetBar.fill;
            targetHealthText = targetBar.label;

            // Комбо — третий блок столбца, ровно там, где у героя стоит
            // выносливость. Точки центрируются по высоте блока.
            BuildComboDots(target,
                           new Vector2(EnemyPanelWidth * EnemyBarFrom,
                                       -EnemyPanelHeight * (EnemyComboFrom + EnemyComboTo) * 0.5f),
                           EnemyPanelWidth * (EnemyBarTo - EnemyBarFrom),
                           EnemyPanelHeight * (EnemyComboTo - EnemyComboFrom));

            targetPanel.SetActive(false);

            // --- Панель мирного: там же, где панель цели ---
            var neutral = CreateFramedPanel(root, "NeutralPanel", "UI/Frame_Neutral",
                new Vector2(ScreenMargin + PanelWidth + PanelGap, -ScreenMargin),
                NeutralPanelWidth, NeutralPanelHeight);
            neutralPanel = neutral.gameObject;

            neutralPortrait = CreateSlotPortrait(neutral,
                NeutralPanelWidth, NeutralPanelHeight,
                NeutralPortraitCenterX, NeutralPortraitCenterY,
                NeutralPortraitDiameter);

            // Имя внутри таблички, а не над рамкой: у мирного нет полосы,
            // и табличка существует ровно ради имени.
            // У мирного полоски нет, поэтому плашка с именем стоит одна — на
            // той же высоте, где у бойцов начинается полоска здоровья.
            neutralNameText = CreateNamePlate(neutral, NeutralPanelWidth, NeutralPanelHeight,
                                              NeutralPlateFrom, NeutralPlateTo,
                                              NeutralPlateTop, NeutralPlateBottom);

            neutralPanel.SetActive(false);

            BuildAbilityBar(root);
            BuildExperienceBar(root);

            // Последней — значит поверх всего остального: панель по наведению
            // выскакивает у курсора и обязана перекрывать то, над чем он
            // оказался, а не прятаться под полосой умений.
            BuildHoverPanel(root);
        }

        /// <summary>
        /// Панель, которая выскакивает у курсора при наведении на существо
        /// или предмет.
        ///
        /// Тем же артом, что панель мирного, и той же долевой геометрией —
        /// поэтому уменьшение до 240 не требует ни новых замеров, ни новых
        /// картинок. Врагу подменяется только спрайт рамки: панели героя,
        /// цели и мирного нарезаны в один размер, поэтому доли остаются
        /// верны при любой из них.
        ///
        /// У курсора, а не в общем ряду наверху. Смысл наведения в том, что
        /// человек уже смотрит на кончик указателя; панель в другом углу
        /// экрана заставила бы переводить взгляд туда и обратно — то есть
        /// делала бы ровно то, от чего избавляет.
        /// </summary>
        private void BuildHoverPanel(RectTransform root)
        {
            hoverPanelRect = CreateFramedPanel(root, "HoverPanel", "UI/Frame_Neutral",
                                               Vector2.zero, HoverPanelWidth, HoverPanelHeight);

            hoverFrame = hoverPanelRect.GetComponent<Image>();
            hoverPanel = hoverPanelRect.gameObject;

            hoverPortrait = CreateSlotPortrait(hoverPanelRect,
                                               HoverPanelWidth, HoverPanelHeight,
                                               NeutralPortraitCenterX, NeutralPortraitCenterY,
                                               NeutralPortraitDiameter);

            // Имя — на плашке мирного: там для него и вырезано место.
            hoverNameText = CreateText(hoverPanelRect, "Name", "", 12, TextColor,
                                       Vector2.zero, Vector2.zero);

            var nameRect = (RectTransform)hoverNameText.transform;
            nameRect.anchorMin = new Vector2(NeutralPlateFrom, 1f - NeutralPlateBottom);
            nameRect.anchorMax = new Vector2(NeutralPlateTo, 1f - NeutralPlateTop);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            hoverNameText.alignment = TextAnchor.MiddleCenter;

            // Полоса жизни — в том же жёлобе, что у панели цели. Живёт
            // отдельным объектом, чтобы её можно было спрятать целиком:
            // у мирных её нет намеренно — она обещала бы, что их можно бить.
            var bar = CreateGrooveBar(hoverPanelRect, "Health", HealthColor,
                                      EnemyBarTop, EnemyBarBottom,
                                      EnemyBarFrom, EnemyBarTo);
            hoverHealthFill = bar.fill;
            hoverHealthText = bar.label;

            // Прячем полосу за её общий узел: заполнение и подпись оба лежат
            // в нём, поэтому одним переключателем уходит вся полоса, а не
            // только цветная часть.
            hoverHealthHost = bar.fill != null && bar.fill.parent != null
                ? bar.fill.parent.gameObject
                : null;

            // Подсказка про действие — под панелью, а не внутри: внутри места
            // нет, там уже имя и полоса, а под рамкой строка не спорит ни с
            // чем и читается первой после имени.
            hoverHintText = CreateText(hoverPanelRect, "Hint", "", 11, HintColor,
                                       new Vector2(0f, -HoverPanelHeight - 2f),
                                       new Vector2(HoverPanelWidth, 16f));
            hoverHintText.alignment = TextAnchor.UpperCenter;

            hoverPanel.SetActive(false);
        }

        /// <summary>
        /// Полоска опыта в самом низу во всю ширину — как в любой игре жанра.
        /// Место выбрано не случайно: это единственный показатель, который
        /// растёт часами, и он не должен отвлекать от боя.
        /// </summary>
        private void BuildExperienceBar(RectTransform root)
        {
            var bar = new GameObject("ExperienceBar", typeof(Image));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, ExpBarHeight);

            var back = bar.GetComponent<Image>();
            back.color = ExpBack;
            back.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(rect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = ExpColor;
            fillImage.raycastTarget = false;
            expFill = fillRect;

            expText = CreateText(rect, "Label", "", 10, TextDim,
                Vector2.zero, new Vector2(200f, ExpBarHeight));
            expText.alignment = TextAnchor.MiddleCenter;
            Stretch((RectTransform)expText.transform);
        }

        /// <summary>
        /// Комбо-очки точками под панелью цели.
        ///
        /// Именно под целью, а не под игроком: очки живут на конкретном враге,
        /// и место на экране должно об этом напоминать. Переключился — точки
        /// погасли вместе со сменой имени над ними.
        /// </summary>
        private void BuildComboDots(RectTransform parent, Vector2 position, float width,
                                    float blockHeight = 0f)
        {
            comboDots.Clear();

            // Вдвое крупнее прежних одиннадцати точек — решение Павлона
            // 01.09.2026. Комбо читается на бегу и с дальней камеры, а в
            // одиннадцать точек оно превращалось в пунктир под панелью.
            int count = combo != null ? combo.MaxPoints : 5;

            // Точка занимает высоту блока целиком, если блок задан: комбо —
            // такой же блок столбца, как имя и полоска, и должно заполнять
            // свою строку, а не болтаться в ней.
            float size = blockHeight > 1f ? blockHeight : 22f;
            float gap = 6f;
            float totalWidth = count * size + (count - 1) * gap;
            float startX = position.x + (width - totalWidth) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Combo" + (i + 1), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                // Позиция задаёт ЦЕНТР строки комбо, поэтому поднимаем точку
                // на половину её высоты: у RectTransform опорная точка сверху.
                rect.anchoredPosition = new Vector2(startX + i * (size + gap),
                                                    position.y + size * 0.5f);
                rect.sizeDelta = new Vector2(size, size);

                var image = go.GetComponent<Image>();

                // Нарисованное гнездо, если оно есть. Цвет при этом белый:
                // любой оттенок приглушил бы металл и камень.
                var socket = Resources.Load<Sprite>("UI/Combo_Empty");

                if (socket != null)
                {
                    image.sprite = socket;
                    image.color = Color.white;
                }
                else
                {
                    image.color = ComboEmpty;
                }

                image.raycastTarget = false;

                comboDots.Add(image);
            }
        }

        /// <summary>
        /// Панель способностей внизу по центру — там, где её ищет глаз
        /// в любой игре этого жанра.
        /// </summary>
        /// <summary>Полоса поедания над панелью способностей.</summary>
        private void BuildEatBar(RectTransform root)
        {
            if (eatBarObject != null) Destroy(eatBarObject);

            var go = new GameObject("EatBar", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            // Над панелью способностей: она сама стоит на высоте полосы
            // опыта плюс отступ, а мы поднимаемся ещё на её высоту.
            rect.anchoredPosition = new Vector2(0f, ScreenMargin + ExpBarHeight + SlotSize + 18f);
            rect.sizeDelta = new Vector2(240f, 14f);

            var track = new GameObject("Track", typeof(Image));
            var trackRect = (RectTransform)track.transform;
            trackRect.SetParent(rect, false);
            Stretch(trackRect);
            track.GetComponent<Image>().color = new Color32(0x14, 0x12, 0x0E, 0xC8);

            var fill = new GameObject("Fill", typeof(Image));
            eatFill = (RectTransform)fill.transform;
            eatFill.SetParent(rect, false);

            // Точка опоры слева: заливка растёт слева направо, как читают.
            eatFill.anchorMin = new Vector2(0f, 0f);
            eatFill.anchorMax = new Vector2(1f, 1f);
            eatFill.pivot = new Vector2(0f, 0.5f);
            eatFill.offsetMin = new Vector2(2f, 2f);
            eatFill.offsetMax = new Vector2(-2f, -2f);
            eatFill.localScale = new Vector3(0f, 1f, 1f);

            // Зелёный — тот же, что у цифр лечения: одно действие должно
            // читаться одним цветом.
            fill.GetComponent<Image>().color = new Color32(0x7A, 0xD8, 0x72, 0xFF);

            // Подпись над полосой: положение и размер идут в сам вызов —
            // такова здешняя фабрика текста.
            eatLabel = CreateText(rect, "Label", "", 12,
                                  new Color32(0xE8, 0xE2, 0xD4, 0xFF),
                                  new Vector2(0f, 17f), new Vector2(240f, 16f));

            eatLabel.alignment = TextAnchor.LowerCenter;

            eatBarObject = go;
            go.SetActive(false);
        }

        private void BuildAbilityBar(RectTransform root)
        {
            hudRoot = root;
            slots.Clear();

            // Старую панель убираем целиком, а не правим на месте: приёмов
            // в наборах разное число, и переиспользование кнопок оставило бы
            // висеть лишние.
            if (abilityBarObject != null)
            {
                Destroy(abilityBarObject);
                abilityBarObject = null;
            }

            if (abilities == null) return;

            // Слотов всегда десять, сколько бы приёмов ни было готово.
            //
            // Панель, которая растёт с каждым выученным приёмом, каждый раз
            // прыгает и меняет ширину — и мышечная память игрока на пятый
            // слот сбрасывается. Пустые гнёзда заодно честно говорят, сколько
            // приёмов ещё будет.
            const int BarSlots = 10;

            int ready = abilities.Abilities.Count;
            float totalWidth = BarSlots * SlotSize + (BarSlots - 1) * SlotGap;

            var bar = new GameObject("AbilityBar", typeof(RectTransform));
            abilityBarObject = bar;
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(root, false);
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, ScreenMargin + ExpBarHeight);
            barRect.sizeDelta = new Vector2(totalWidth, SlotSize);

            // Нарисованная рамка под ряд иконок.
            //
            // Лежит ПОД слотами и шире их: у рамки собственные поля и торцы,
            // и если подогнать её ровно по ряду, иконки лягут на золотой кант.
            // Растягивается девятью кусками, поэтому число приёмов может
            // меняться — рамка подстроится.
            var plate = IsoRPG.UI.UiFrames.Enabled
                ? Resources.Load<Sprite>("UI/Frame_Abilities")
                : null;

            if (plate != null)
            {
                var frameGo = new GameObject("Frame", typeof(Image));
                var frameRect = (RectTransform)frameGo.transform;
                frameRect.SetParent(barRect, false);

                frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.anchoredPosition = Vector2.zero;
                // Запас считается от границ растяжения, а не на глаз.
                //
                // Торцы не тянутся: при множителе 3.6 каждый занимает 210/3.6,
                // то есть 58 точек, вместе 116. Прежний запас в 96 был МЕНЬШЕ
                // этой суммы — значит иконкам доставалось меньше места, чем
                // они занимают, и крайние вылезали на золото торца. С десятью
                // слотами это стало видно сразу.
                //
                // Даём 116 на торцы плюс 34 на воздух по краям ряда.
                frameRect.sizeDelta = new Vector2(totalWidth + 150f, SlotSize + 34f);

                var frameImage = frameGo.GetComponent<Image>();
                frameImage.sprite = plate;
                frameImage.type = Image.Type.Sliced;
                frameImage.raycastTarget = false;

                // Границы растяжения заданы в пикселях исходника, а он в семь
                // раз крупнее того, что рисуется на экране: слева и справа по
                // 210 точек при ширине панели в 290 — они не помещаются, и
                // Unity рисует пустоту вместо рамки.
                //
                // Множитель уменьшает границы в отрисовке, оставляя пропорции
                // картинки нетронутыми. Три — это ровно то отношение, при
                // котором торцы садятся на место.
                frameImage.pixelsPerUnitMultiplier = 3.6f;

                // Первой в списке — значит под всеми иконками по отрисовке.
                frameRect.SetAsFirstSibling();

                // Ромб на кромке — отдельным элементом.
                //
                // В картинке он был нарисован ровно посередине, то есть в
                // растяжимой части, и на панели в десять слотов уезжал вбок
                // и вылезал за верхний край. Границами это не лечится: любой
                // узор в середине девятикусочной картинки обречён. Вырезан из
                // рамки и кладётся своим размером.
                var gem = Resources.Load<Sprite>("UI/Abilities_Gem");

                if (gem != null)
                {
                    var gemGo = new GameObject("Gem", typeof(Image));
                    var gemRect = (RectTransform)gemGo.transform;
                    gemRect.SetParent(frameRect, false);

                    gemRect.anchorMin = new Vector2(0.5f, 1f);
                    gemRect.anchorMax = new Vector2(0.5f, 1f);
                    gemRect.pivot = new Vector2(0.5f, 1f);

                    // Тот же множитель, что у рамки: тогда ромб совпадает с
                    // кромкой, из которой вырезан.
                    gemRect.sizeDelta = new Vector2(gem.rect.width / 3.6f,
                                                    gem.rect.height / 3.6f);

                    // Опущен на треть своей высоты: в исходнике он торчал над
                    // кромкой, и на панели это читалось случайным треугольником
                    // над иконками, а не украшением на ней.
                    gemRect.anchoredPosition = new Vector2(0f, gemRect.sizeDelta.y * 0.34f);

                    var gemImage = gemGo.GetComponent<Image>();
                    gemImage.sprite = gem;
                    gemImage.raycastTarget = false;
                }
            }

            // Каменная подложка под каждым гнездом — кладётся первой, чтобы
            // оказаться под иконкой. Выбор Павлона 01.09.2026:
            // `inventory-slot-small` с рамкой `inventory-slot-small 1`.
            var backing = Resources.Load<Sprite>("UI/Slot_Backing");

            if (backing != null)
            {
                for (int i = 0; i < BarSlots; i++)
                    AddSlotArt(barRect, i * (SlotSize + SlotGap), backing, "SlotBack" + (i + 1),
                               SlotBackInset);
            }

            for (int i = 0; i < BarSlots; i++)
            {
                float x = i * (SlotSize + SlotGap);

                // Гнездо под приём, которого ещё нет: пустая плашка без
                // рисунка, без цифры и без нажатия. Подсказки у него тоже
                // нет — рассказывать нечего, а пустое всплывающее окно
                // читается как поломка.
                if (i >= ready)
                {
                    var emptyGo = new GameObject("Slot" + (i + 1) + "Empty", typeof(Image));
                    var emptyRect = (RectTransform)emptyGo.transform;
                    emptyRect.SetParent(barRect, false);
                    emptyRect.anchorMin = Vector2.zero;
                    emptyRect.anchorMax = Vector2.zero;
                    emptyRect.pivot = Vector2.zero;
                    emptyRect.anchoredPosition = new Vector2(x, 0f);
                    emptyRect.sizeDelta = new Vector2(SlotSize, SlotSize);

                    var emptyImage = emptyGo.GetComponent<Image>();
                    emptyImage.color = SlotPlate;

                    // Не ловит указатель: иначе пустое гнездо перехватывало бы
                    // клики, которые предназначены полю боя под панелью.
                    emptyImage.raycastTarget = false;
                    continue;
                }

                var ability = abilities.Abilities[i];

                var slotGo = new GameObject("Slot" + (i + 1), typeof(Image));
                var slotRect = (RectTransform)slotGo.transform;
                slotRect.SetParent(barRect, false);
                slotRect.anchorMin = new Vector2(0f, 0f);
                slotRect.anchorMax = new Vector2(0f, 0f);
                slotRect.pivot = new Vector2(0f, 0f);
                slotRect.anchoredPosition = new Vector2(x, 0f);
                slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);

                // Плашка у всех одинаковая, а рисунок сверху свой. Разные
                // цвета фона у соседних кнопок читаются как хаос: глаз
                // считает цвет признаком, ищет в нём смысл и не находит.
                var icon = slotGo.GetComponent<Image>();
                icon.color = ability.icon != null ? SlotPlate : ability.iconColor;

                // Ловит указатель: это единственный слой кнопки, который
                // может, — рисунок, откат и тексты выключены нарочно,
                // чтобы наведение не терялось на границе между ними.
                icon.raycastTarget = true;

                if (ability.icon != null)
                {
                    var art = new GameObject("Art", typeof(Image));
                    var artRect = (RectTransform)art.transform;
                    artRect.SetParent(slotRect, false);
                    artRect.anchorMin = Vector2.zero;
                    artRect.anchorMax = Vector2.one;

                    // Небольшой отступ: рисунок впритык к краю плашки
                    // выглядит обрезанным.
                    // Без отступа: рисунок уже нарисован с полями внутри
                    // себя, и второй отступ делает иконку мелкой вдвое.
                    artRect.offsetMin = Vector2.zero;
                    artRect.offsetMax = Vector2.zero;

                    var artImage = art.GetComponent<Image>();
                    artImage.sprite = ability.icon;

                    // Рисунок не ловит указатель: события должна получать
                    // кнопка под ним, иначе наведение теряется на границе
                    // между слоями.
                    artImage.raycastTarget = false;
                    artImage.preserveAspect = true;
                }

                // Тёмная плашка отката поверх иконки. Пока не идёт откат —
                // прозрачная, поэтому её просто не видно.
                var veilGo = new GameObject("Cooldown", typeof(Image));
                var veilRect = (RectTransform)veilGo.transform;
                veilRect.SetParent(slotRect, false);
                Stretch(veilRect);

                var veil = veilGo.GetComponent<Image>();
                veil.color = new Color(0f, 0f, 0f, 0f);
                veil.raycastTarget = false;

                var cdText = CreateText(slotRect, "CooldownText", "", 16, TextColor,
                    Vector2.zero, new Vector2(SlotSize, SlotSize));
                cdText.alignment = TextAnchor.MiddleCenter;
                Stretch((RectTransform)cdText.transform);

                // Клавиша в углу — как в WoW, чтобы не гадать, на что жать.
                var key = CreateText(slotRect, "Key", ability.hotkeyLabel, 11, TextDim,
                    new Vector2(3f, -2f), new Vector2(14f, 12f));

                // Названия под кнопками нет: шесть подписей мелким кеглем
                // читаются как забор, а нужны они ровно один раз — когда
                // игрок разбирается, что это. Для этого есть наведение.
                var hover = slotGo.AddComponent<IsoRPG.UI.AbilityHoverTrigger>();
                hover.Setup(ability, GetComponent<WeaponStats>());

                // И нажимается мышью: кнопка, которая только показывает
                // подсказку, но не работает по клику, читается как
                // сломанная. Клавиша остаётся быстрым путём.
                var press = slotGo.AddComponent<Button>();
                press.targetGraphic = icon;
                press.transition = Selectable.Transition.ColorTint;

                var pressColors = press.colors;
                pressColors.normalColor = Color.white;
                pressColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                pressColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                pressColors.selectedColor = Color.white;
                pressColors.fadeDuration = 0.06f;
                press.colors = pressColors;

                var pressed = ability;
                press.onClick.AddListener(() => abilities.TryUse(pressed));

                slots.Add(new AbilitySlot
                {
                    ability = ability,
                    icon = icon,
                    cooldownVeil = veil,
                    cooldownText = cdText
                });
            }

            // Рамки — последними, поверх всего: иначе кромка окажется под
            // иконкой и откатом, и гнездо будет выглядеть незакрытым.
            var slotFrame = Resources.Load<Sprite>("UI/Frame_Portrait");

            if (slotFrame != null)
            {
                for (int i = 0; i < BarSlots; i++)
                    AddSlotArt(barRect, i * (SlotSize + SlotGap), slotFrame, "SlotFrame" + (i + 1));
            }
        }

        /// <summary>
        /// Кладёт картинку ровно в гнездо приёма — подложку или рамку.
        ///
        /// Указатель не ловит: клики должны доставаться самой кнопке приёма,
        /// иначе панель перестанет нажиматься, а причина будет невидимой.
        /// </summary>
        private static void AddSlotArt(RectTransform bar, float x, Sprite sprite, string name,
                                       float inset = 0f)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(bar, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x + inset, inset);
            rect.sizeDelta = new Vector2(SlotSize - inset * 2f, SlotSize - inset * 2f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SlotSlice;
            image.raycastTarget = false;
        }

        /// <summary>
        /// Панель на нарисованной рамке.
        ///
        /// Размер задаётся шириной, высота считается из пропорции картинки:
        /// растянуть рамку по одной оси нельзя, иначе поплывут заклёпки и
        /// золотой кант.
        ///
        /// Если картинки нет, панель собирается по-старому — цветной
        /// плашкой. Игра без интерфейса хуже игры с некрасивым интерфейсом,
        /// а забытый файл в Resources ловится только запуском.
        /// </summary>
        private RectTransform CreateFramedPanel(RectTransform parent, string name,
                                                string sprite, Vector2 position,
                                                float width = 0f, float height = 0f)
        {
            if (width <= 0f) width = PanelWidth;
            if (height <= 0f) height = PanelHeight;

            // Рамки могут быть выключены общим рубильником. Тогда сюда же
            // приходим без картинки — и панель собирается плашкой ТОГО ЖЕ
            // размера, а не через CreatePanel: тот жёстко берёт размеры
            // панели игрока, и панель цели с её собственной шириной уехала бы
            // вместе со всем содержимым.
            var art = IsoRPG.UI.UiFrames.Enabled ? Resources.Load<Sprite>(sprite) : null;

            if (art == null && IsoRPG.UI.UiFrames.Enabled)
            {
                Debug.LogWarning("[IsoRPG] Нет спрайта " + sprite +
                                 " — панель нарисована плашкой. Прогони " +
                                 "Tools/IsoRPG/Настроить панели интерфейса.");
            }

            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;

            if (art != null)
            {
                image.sprite = art;
                image.type = Image.Type.Simple;
            }
            else if (Resources.Load<Sprite>("UI/Frame_Portrait") != null)
            {
                // Подложку убираем совсем — решение Павлона 01.09.2026:
                // «убрать подложку под ними нашу, поставить портрет в рамку и
                // рядом просто два бара». Своя оправа теперь есть и у
                // портрета, и у полосок, а тёмная плашка позади них только
                // утяжеляла угол экрана.
                image.color = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                image.color = PanelColor;

                // Тонкая светлая кромка — как у обычной панели: без неё
                // тёмный прямоугольник на оливковой земле читается как грязь.
                var edge = new GameObject("Edge", typeof(Image));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(rect, false);
                Stretch(edgeRect);
                edgeRect.offsetMin = new Vector2(-1f, -1f);
                edgeRect.offsetMax = new Vector2(1f, 1f);
                edge.transform.SetAsFirstSibling();

                var edgeImage = edge.GetComponent<Image>();
                edgeImage.color = PanelEdge;
                edgeImage.raycastTarget = false;
            }

            return rect;
        }

        /// <summary>Круг для обрезки портрета. Делается один раз на всю игру.</summary>
        private static Sprite circleSprite;

        private static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Плавный край в один пиксель: жёсткая граница круга
                    // рисуется лесенкой и видна даже на мелком портрете.
                    float alpha = Mathf.Clamp01(radius - distance);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;

            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                                         new Vector2(0.5f, 0.5f));

            return circleSprite;
        }

        /// <summary>Портрет в круглом гнезде нарисованной рамки.</summary>
        private Image CreateSlotPortrait(RectTransform panel,
                                         float width = 0f, float height = 0f,
                                         float centerX = -1f, float centerY = -1f,
                                         float diameter = 0f)
        {
            if (width <= 0f) width = PanelWidth;
            if (height <= 0f) height = PanelHeight;
            if (centerX < 0f) centerX = PortraitCenterX;
            if (centerY < 0f) centerY = PortraitCenterY;
            if (diameter <= 0f) diameter = PortraitDiameter;

            // Каменная рамка вокруг портрета.
            //
            // Выбор Павлона 01.09.2026: `inventory-slot-small 1` из набора
            // gui_fantasy_kit — «портрет в рамку, а подложку нашу убрать».
            // Кладём ПОД портрет и крупнее его: у рамки своя кромка, и если
            // подогнать её вровень, портрет закроет камень.
            var frameArt = Resources.Load<Sprite>("UI/Frame_Portrait");

            if (frameArt != null)
            {
                var frameGo = new GameObject("PortraitFrame", typeof(Image));
                var frameRect = (RectTransform)frameGo.transform;
                frameRect.SetParent(panel, false);

                // Рамка ровно по портрету: его сторона плюс две кромки.
                //
                // Павлон 01.09.2026: «нет, не портрет больше, а рамку меньше».
                // Так и правильнее — у 9-slice нет собственного размера, она
                // тянется на что угодно, лишь бы сторона была не меньше двух
                // кромок. У нас кромка девять точек, портрет семьдесят два —
                // запас громадный.
                float frameSize = width * diameter + PortraitWall * 2f;

                frameRect.anchorMin = new Vector2(0f, 1f);
                frameRect.anchorMax = new Vector2(0f, 1f);
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.anchoredPosition = new Vector2(width * centerX, -height * centerY);
                frameRect.sizeDelta = new Vector2(frameSize, frameSize);

                // Тёмное поле — оно же фон, когда портрета нет.
                //
                // Павлон 01.09.2026: «если портрета нет, залей рамку просто
                // тёмным». Пустая рамка показывала мир насквозь и читалась
                // как дыра в интерфейсе, а не как «лица нет».
                //
                // Лежит ПОД рамкой и ровно её размера, без отступа. Сначала
                // я отодвигал его внутрь на границу 9-slice — и между полем и
                // камнем остался просвет: видимая кромка тоньше этой границы,
                // и подгонять её числом значит гадать. Под рамкой подгонять
                // нечего: камень сам закрывает края поля.
                var fillGo = new GameObject("PortraitBack", typeof(Image));
                var fillRect = (RectTransform)fillGo.transform;
                fillRect.SetParent(panel, false);

                fillRect.anchorMin = new Vector2(0f, 1f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = new Vector2(width * centerX, -height * centerY);
                fillRect.sizeDelta = new Vector2(frameSize, frameSize);

                var fillImage = fillGo.GetComponent<Image>();
                fillImage.color = PortraitEmpty;
                fillImage.raycastTarget = false;

                // Раньше рамки в иерархии — значит рисуется под ней.
                fillRect.SetSiblingIndex(frameRect.GetSiblingIndex());

                var frameImage = frameGo.GetComponent<Image>();
                frameImage.sprite = frameArt;
                frameImage.type = Image.Type.Sliced;

                frameImage.pixelsPerUnitMultiplier = PortraitSlice;
                frameImage.raycastTarget = false;
            }

            // Круглое окно, за края которого портрет не вылезет.
            //
            // Портреты рисуются квадратными, а гнездо круглое: без обрезки
            // плечи и капюшон торчат за золотое кольцо, и панель выглядит
            // так, будто картинку положили сверху, а не вставили внутрь.
            var maskGo = new GameObject("PortraitMask", typeof(Image), typeof(Mask));
            var maskRect = (RectTransform)maskGo.transform;
            maskRect.SetParent(panel, false);

            var maskImage = maskGo.GetComponent<Image>();

            // Квадратное окно, если портрет стоит в каменной рамке.
            //
            // Павлон 01.09.2026: «портрет сделай квадратным и нормальную
            // рамку». Круг был нужен под золотое кольцо прежней покупной
            // панели; в квадратной каменной оправе он оставляет по углам
            // четыре пустых треугольника, и портрет выглядит вклеенным.
            maskImage.sprite = frameArt != null ? null : CircleSprite();
            maskImage.raycastTarget = false;

            var mask = maskGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var go = new GameObject("Portrait", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(maskRect, false);

            // Портрет на пять процентов крупнее гнезда: Павлон 01.09.2026
            // «портрет так и не прилегает к рамке, сделай больше на 5%».
            // Рамка при этом считается от базового размера, поэтому портрет
            // заходит под её кромку, а не отодвигает её.
            float size = width * diameter * PortraitInner;

            // Окно стоит в гнезде, портрет заполняет его целиком.
            maskRect.anchorMin = new Vector2(0f, 1f);
            maskRect.anchorMax = new Vector2(0f, 1f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(width * centerX, -height * centerY);
            maskRect.sizeDelta = new Vector2(size, size);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            // Под портретом ничего не рисуем: гнездо в рамке уже тёмное.
            image.color = Color.white;

            return image;
        }

        /// <summary>
        /// Полоса, лежащая в жёлобе рамки.
        ///
        /// Подложки нет — жёлоб нарисован. Заливка растёт слева направо
        /// внутри его границ, поэтому пустая полоса выглядит как пустой
        /// жёлоб, а не как чёрный прямоугольник поверх рисунка.
        /// </summary>
        /// <summary>
        /// Во сколько раз ужать картинку, чтобы она села в полоску по высоте.
        ///
        /// Зачем. У 9-slice углы не тянутся — они рисуются в своём размере, и
        /// если сумма верхней и нижней границы больше высоты полоски, Unity
        /// сжимает их, а рисунок сминается. 01.09.2026 Павлон увидел ровно
        /// это: подложка высотой 124 с границами 30 и 30 в полоске высотой 25
        /// превратилась в две шляпки по краям, «гантели».
        ///
        /// Считаем от ФАКТИЧЕСКОЙ высоты полоски, а не подбираем число:
        /// полоски у нас разной высоты (у игрока, у цели, под курсором), и
        /// подобранное под одну сомнётся на другой.
        /// </summary>
        private static float SliceFit(Sprite sprite, float height)
        {
            if (sprite == null || height <= 1f) return 1f;

            return Mathf.Max(1f, sprite.rect.height / height);
        }

        /// <summary>
        /// Плашка с именем над полосками — тот же каменный жёлоб, что под
        /// ними, только пустой.
        ///
        /// Образец — панель WoW, который прислал Павлон 01.09.2026: имя лежит
        /// на своей подложке ровно по ширине полосок, а не висит в воздухе
        /// над панелью. Цифр на полосках там нет вовсе, и это правильно: на
        /// расстоянии читается длина полосы, а не число.
        /// </summary>
        private Text CreateNamePlate(RectTransform panel, float width, float height,
                                     float fromX, float toX, float fromY, float toY)
        {
            var host = new GameObject("NamePlate", typeof(RectTransform), typeof(Image));
            var hostRect = (RectTransform)host.transform;
            hostRect.SetParent(panel, false);

            hostRect.anchorMin = new Vector2(fromX, 1f - toY);
            hostRect.anchorMax = new Vector2(toX, 1f - fromY);
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var socket = Resources.Load<Sprite>("UI/Bar_Socket");
            var image = host.GetComponent<Image>();

            if (socket != null)
            {
                image.sprite = socket;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = SliceFit(socket, height * Mathf.Abs(toY - fromY));
            }
            else image.color = PanelColor;

            image.raycastTarget = false;

            var label = CreateText(hostRect, "Value", "", 13, NameColor,
                                   Vector2.zero, Vector2.zero);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;

            // Тень в одну точку, вполсилы. Павлон 01.09.2026: «совсем
            // небольшая, не ярко выраженная». Она нужна не ради вида, а ради
            // чтения: жёлтые буквы на светлой части камня теряют край.
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return label;
        }

        private (RectTransform fill, Text label) CreateGrooveBar(
            RectTransform panel, string name, Color color, float fromY, float toY,
            float fromX = -1f, float toX = -1f, string fillSprite = null,
            bool keepTint = false)
        {
            if (fromX < 0f) fromX = BarsFrom;
            if (toX < 0f) toX = BarsTo;

            // Подложка под полоску — каменный жёлоб.
            //
            // Выбор Павлона 01.09.2026 из набора gui_fantasy_kit. Полоска без
            // подложки на светлой земле теряет края: тёмный прямоугольник
            // читается как грязь, а не как шкала. Жёлоб даёт ей границу.
            var socket = Resources.Load<Sprite>("UI/Bar_Socket");

            var host = new GameObject(name, socket != null
                                                ? new[] { typeof(RectTransform), typeof(Image) }
                                                : new[] { typeof(RectTransform) });
            var hostRect = (RectTransform)host.transform;
            hostRect.SetParent(panel, false);

            // Высота полоски на экране — из неё считается всё остальное.
            float barHeight = panel.rect.height * Mathf.Abs(toY - fromY);

            if (socket != null)
            {
                var socketImage = host.GetComponent<Image>();
                socketImage.sprite = socket;
                socketImage.type = Image.Type.Sliced;
                socketImage.raycastTarget = false;
                socketImage.pixelsPerUnitMultiplier = SliceFit(socket, barHeight);
            }

            hostRect.anchorMin = new Vector2(fromX, 1f - toY);
            hostRect.anchorMax = new Vector2(toX, 1f - fromY);
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(hostRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = color;
            fillImage.raycastTarget = false;

            // Готовая заливка со стеклянным бликом, если она заказана.
            //
            // Цвет остаётся тонировкой: у здоровья союзника картинка зелёная и
            // цвет её почти не трогает, а полоска врага той же картинкой
            // уводится в красный. Одна картинка на оба случая — иначе пришлось
            // бы держать по файлу на каждый оттенок.
            if (!string.IsNullOrEmpty(fillSprite))
            {
                var art = Resources.Load<Sprite>(fillSprite);

                if (art != null)
                {
                    fillImage.sprite = art;
                    fillImage.type = Image.Type.Sliced;
                    fillImage.pixelsPerUnitMultiplier =
                        SliceFit(art, barHeight - BarInset * 2f);

                    // Картинка уже нужного цвета — тонировку снимаем. Зелёная
                    // заливка, умноженная на наш зелёный, уходит в болото и
                    // теряет блик, ради которого её и брали.
                    //
                    // Кроме случая, когда тонировка и нужна: у врага картинка
                    // та же, а цвет обязан быть красным — это язык, по
                    // которому игрок отличает свою полоску от чужой.
                    if (!keepTint) fillImage.color = Color.white;
                }
            }

            // Внутрь жёлоба, а не вровень с ним: край подложки должен
            // оставаться виден, иначе жёлоб не читается.
            if (socket != null)
            {
                fillRect.offsetMin = new Vector2(BarInset, BarInset);
                fillRect.offsetMax = new Vector2(-BarInset, -BarInset);
            }

            // Цифры на полоске есть в разметке, но выключены.
            //
            // Павлон 01.09.2026 по образцу WoW: «цифры вообще там не нужны».
            // На дистанции игрок читает длину полосы, а число только шумит.
            // Держим объект живым, чтобы код обновления не менялся: он пишет
            // в текст, которого никто не видит, и это дешевле, чем разводить
            // две ветки на каждое изменение здоровья.
            var label = CreateText(hostRect, "Value", "", 11, TextColor,
                                   Vector2.zero, Vector2.zero);
            label.enabled = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;

            return (fillRect, label);
        }

        private RectTransform CreatePanel(RectTransform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            // Якорь в левый верхний угол: интерфейс боя привязан к нему в
            // подавляющем большинстве игр, и глаз ищет его именно там.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var image = go.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            // Тонкая светлая рамка — отделяет панель от светлой земли,
            // иначе на оливковом фоне тёмный прямоугольник читается как грязь.
            var edge = new GameObject("Edge", typeof(Image));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(rect, false);
            Stretch(edgeRect);
            edgeRect.offsetMin = new Vector2(-1f, -1f);
            edgeRect.offsetMax = new Vector2(1f, 1f);
            edge.transform.SetAsFirstSibling();

            var edgeImage = edge.GetComponent<Image>();
            edgeImage.color = PanelEdge;
            edgeImage.raycastTarget = false;

            return rect;
        }

        /// <summary>
        /// Место под портрет. Пока просто тёмный квадрат с рамкой — настоящий
        /// портрет требует второй камеры, снимающей лицо персонажа в текстуру,
        /// и это отдельная работа. Но место под него занято сразу: иначе, когда
        /// портрет появится, всю панель придётся перекомпоновывать.
        /// </summary>
        private Image CreatePortrait(RectTransform parent)
        {
            var go = new GameObject("Portrait", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(InnerPad, -InnerPad);
            rect.sizeDelta = new Vector2(PortraitSize, PortraitSize);

            var image = go.GetComponent<Image>();
            image.color = PortraitColor;
            image.raycastTarget = false;

            // Рисунок отдельным слоем поверх плашки: плашка остаётся рамкой,
            // а портрет может отсутствовать — тогда видно просто плашку, а не
            // пустое место.
            var art = new GameObject("Art", typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(rect, false);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(2f, 2f);
            artRect.offsetMax = new Vector2(-2f, -2f);

            var artImage = art.GetComponent<Image>();
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;
            artImage.enabled = false;

            return artImage;
        }

        private struct Bar
        {
            public RectTransform fill;
            public Text label;
        }

        private Bar CreateBar(RectTransform parent, string name, Color back, Color front,
                              Vector2 position, float width)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, BarHeight);

            var backImage = go.GetComponent<Image>();
            backImage.color = back;
            backImage.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(rect, false);
            Stretch(fillRect);

            // Растягиваем от левого края: полоска должна убывать вправо,
            // а не схлопываться к центру.
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = front;
            fillImage.raycastTarget = false;

            var label = CreateText(rect, "Label", "", 11, TextColor,
                Vector2.zero, new Vector2(width, BarHeight));
            label.alignment = TextAnchor.MiddleCenter;
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);

            return new Bar { fill = fillRect, label = label };
        }

        private Text CreateText(RectTransform parent, string name, string content,
                                int size, Color color, Vector2 position, Vector2 size2)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size2;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            LocalizedText.Bind(text, content);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------
        // Обновление
        // ------------------------------------------------------------------

        private void OnPlayerHealthChanged(int current, int max)
        {
            SetBar(playerHealthFill, playerHealthText, current, max);
        }

        private void OnEnergyChanged(int current, int max)
        {
            SetBar(playerEnergyFill, playerEnergyText, current, max);
        }

        private void OnExperienceChanged(int current, int needed)
        {
            // Подпись уровня обновляем здесь же.
            //
            // Она ставилась один раз при старте, а сохранение подгружается
            // после — и на портрете навсегда оставался первый уровень, хотя
            // в окне персонажа честно стоял шестой. Событие о смене уровня
            // для этого не годится: при загрузке игрок уровень не получает,
            // он его уже имеет, и звук повышения был бы неуместен.
            if (playerExperience != null) ShowPlayerLevel(playerExperience.Level);

            if (expFill != null)
            {
                float fraction = needed > 0 ? Mathf.Clamp01((float)current / needed) : 1f;
                expFill.localScale = new Vector3(fraction, 1f, 1f);
            }

            if (expText != null)
                LocalizedText.Bind(expText, needed > 0 ? current + " / " + needed : "максимальный уровень");
        }

        /// <summary>
        /// Имя героя на плашке. Без уровня.
        ///
        /// Павлон 01.09.2026: «замени „Разбойник ур. 4“ на „Шико“, уровень
        /// будем чуть позже выводить отдельно». В WoW уровень стоит кружком у
        /// портрета, а не в строке имени, — туда и пойдёт.
        ///
        /// Метод оставлен под уровень: он подписан на событие роста, и когда
        /// кружок появится, менять придётся только эту строку.
        /// </summary>
        private void ShowPlayerLevel(int level)
        {
            if (playerNameText != null) playerNameText.text = HeroName;
        }

        /// <summary>Перерисовать имя героя — например, после смены языка.</summary>
        private void RelabelPlayer()
        {
            if (playerExperience != null) ShowPlayerLevel(playerExperience.Level);
        }

        private void OnLevelUp(int level)
        {
            ShowPlayerLevel(level);

            CombatLog.LevelUp(level);
            IsoRPG.Audio.Sfx.LevelUp();
        }

        private void OnComboChanged(int points, int max)
        {
            // Подменяем картинку, а не красим одну: у нас две нарисованные —
            // пустое гнездо и зажжённый в нём камень. Они вырезаны в один
            // размер до пикселя, поэтому подмена не двигает ряд.
            var lit = Resources.Load<Sprite>("UI/Combo_Full");
            var dim = Resources.Load<Sprite>("UI/Combo_Empty");

            for (int i = 0; i < comboDots.Count; i++)
            {
                bool on = i < points;

                if (lit != null && dim != null)
                {
                    comboDots[i].sprite = on ? lit : dim;
                    comboDots[i].color = Color.white;
                }
                else
                {
                    comboDots[i].color = on ? ComboFull : ComboEmpty;
                }
            }
        }

        /// <summary>
        /// Затемнение кнопок во время отката и когда не хватает энергии.
        ///
        /// Игрок должен видеть готовность, не считая в уме: горит — можно,
        /// темно — нельзя. Это дешевле любой подсказки текстом.
        /// </summary>
        private void UpdateCooldowns()
        {
            if (abilities == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.ability == null) continue;

                float left = abilities.CooldownLeft(slot.ability);
                bool enoughEnergy = playerEnergy == null || playerEnergy.Has(slot.ability.energyCost);

                if (left > 0.05f)
                {
                    slot.cooldownVeil.color = CooldownVeil;
                    slot.cooldownText.text = left >= 1f
                        ? Mathf.CeilToInt(left).ToString()
                        : left.ToString("0.0");
                }
                else
                {
                    // Не хватает энергии — притеняем слабее, чем на откате:
                    // это другое состояние, и путать их не стоит.
                    slot.cooldownVeil.color = enoughEnergy
                        ? new Color(0f, 0f, 0f, 0f)
                        : new Color(0f, 0f, 0f, 0.4f);
                    slot.cooldownText.text = "";
                }
            }
        }

        /// <summary>
        /// Показать мирного: портрет и имя, без полосы здоровья.
        ///
        /// Отдельный путь, а не подкрашенная боевая панель: полоса здоровья —
        /// это обещание, что цель можно бить. У торговца её быть не должно,
        /// иначе игрок пробует и получает молчание в ответ.
        /// </summary>
        public void ShowNeutral(string title, Sprite portrait)
        {
            if (neutralPanel == null) return;

            // Боевую панель прячем: две панели рядом читались бы как две
            // цели, а цель всегда одна.
            if (targetPanel != null) targetPanel.SetActive(false);

            if (string.IsNullOrEmpty(title))
            {
                neutralPanel.SetActive(false);
                return;
            }

            neutralPanel.SetActive(true);

            if (neutralPortrait != null)
            {
                neutralPortrait.sprite = portrait;
                neutralPortrait.enabled = portrait != null;
            }

            if (neutralNameText != null) neutralNameText.text = Loc.T(title);
        }

        /// <summary>Убрать панель мирного — например, когда выбрали монстра.</summary>
        public void HideNeutral()
        {
            if (neutralPanel != null) neutralPanel.SetActive(false);
        }

        /// <summary>
        /// Показать панель наведения у курсора.
        /// </summary>
        /// <param name="title">Имя, уже переведённое.</param>
        /// <param name="hint">Что сделает нажатие. Пусто — строки не будет.</param>
        /// <param name="portrait">Лицо. Может не быть — у сундука его нет.</param>
        /// <param name="hostile">
        /// Враг ли. От этого зависит и рамка, и полоса жизни: полосу рисуем
        /// только тем, кого можно бить, иначе она обещает то, чего нельзя.
        /// </param>
        /// <param name="health">Текущее здоровье.</param>
        /// <param name="maxHealth">Наибольшее здоровье.</param>
        /// <param name="screenPosition">Курсор в пикселях экрана.</param>
        public void ShowHover(string title, string hint, Sprite portrait,
                              bool hostile, int health, int maxHealth,
                              Vector2 screenPosition)
        {
            if (hoverPanel == null) return;

            hoverPanel.SetActive(true);

            if (hoverFrame != null && IsoRPG.UI.UiFrames.Enabled)
            {
                var art = Resources.Load<Sprite>(hostile ? "UI/Frame_Enemy"
                                                         : "UI/Frame_Neutral");
                if (art != null) hoverFrame.sprite = art;
            }

            if (hoverPortrait != null)
            {
                hoverPortrait.sprite = portrait;
                hoverPortrait.enabled = portrait != null;
            }

            if (hoverNameText != null) hoverNameText.text = title;

            if (hoverHintText != null)
            {
                hoverHintText.text = hint;
                hoverHintText.enabled = !string.IsNullOrEmpty(hint);
            }

            if (hoverHealthHost != null) hoverHealthHost.SetActive(hostile);

            // Тем же SetBar, что и все остальные полосы: заполнение у нас
            // делается масштабом, а не якорями, и своя копия этой логики
            // разошлась бы с общей при первой правке.
            if (hostile) SetBar(hoverHealthFill, hoverHealthText, health, maxHealth);

            PlaceHoverAt(screenPosition);
        }

        public void HideHover()
        {
            if (hoverPanel != null) hoverPanel.SetActive(false);
        }

        /// <summary>
        /// Поставить панель у курсора, не дав ей вылезти за экран.
        ///
        /// Переворачиваем на другую сторону курсора, а не просто упираем в
        /// край: прижатая к краю панель накрыла бы то самое существо, на
        /// которое человек навёл, — а у края экрана это случается постоянно,
        /// потому что там и стоят те, к кому подходят.
        /// </summary>
        private void PlaceHoverAt(Vector2 screenPosition)
        {
            if (hoverPanelRect == null) return;

            var area = hudCanvas != null ? hudCanvas.transform as RectTransform : null;
            if (area == null) return;

            float scale = hudCanvas.scaleFactor;
            if (scale <= 0f) scale = 1f;

            // Экранные пиксели в единицы холста. Отсчёт по вертикали сверху:
            // панель привязана к левому верхнему углу, как и остальные.
            float x = screenPosition.x / scale;
            float y = (Screen.height - screenPosition.y) / scale;

            float w = area.rect.width;
            float h = area.rect.height;

            // Подсказка висит под рамкой, поэтому занятая высота больше самой
            // панели — иначе строка уезжала бы за нижний край незамеченной.
            const float hintRoom = 18f;
            float taken = HoverPanelHeight + hintRoom;

            float px = x + HoverCursorGap;
            if (px + HoverPanelWidth > w) px = x - HoverCursorGap - HoverPanelWidth;

            float py = y + HoverCursorGap;
            if (py + taken > h) py = y - HoverCursorGap - taken;

            px = Mathf.Clamp(px, 0f, Mathf.Max(0f, w - HoverPanelWidth));
            py = Mathf.Clamp(py, 0f, Mathf.Max(0f, h - taken));

            hoverPanelRect.anchoredPosition = new Vector2(px, -py);
        }

        private void OnTargetChanged(Targetable target)
        {
            if (target == null)
            {
                BindTargetHealth(null);
                if (targetPanel != null) targetPanel.SetActive(false);
                return;
            }

            if (neutralPanel != null) neutralPanel.SetActive(false);
            if (targetPanel != null) targetPanel.SetActive(true);

            // Портрет цели меняется вместе с целью: панель одна, а
            // существ много.
            if (targetPortrait != null)
            {
                // Нарисованный портрет, если он есть: снимок модели в круге
                // 60 пикселей читается плохо, все скелеты в нём одинаковы.
                var art = Portraits.For(target.DisplayName) ?? target.Portrait;

                targetPortrait.sprite = art;
                targetPortrait.enabled = art != null;
            }

            if (targetNameText != null)
            {
                var defense = target.GetComponent<DefenseStats>();

                if (defense != null)
                {
                    targetNameText.text = Loc.T(target.DisplayName) + "  " + Loc.F("ур. {0}", defense.Level);

                    // Цвет имени по разнице уровней. Игрок читает его быстрее,
                    // чем само число: серый значит «не трать время», красный —
                    // «не лезь». Цвета каноничные для жанра намеренно.
                    int playerLevel = playerDefense != null ? playerDefense.Level : 1;
                    targetNameText.color = LevelDifficulty.ColorOf(defense.Level, playerLevel);
                }
                else
                {
                    LocalizedText.Bind(targetNameText, target.DisplayName);
                    targetNameText.color = TextColor;
                }
            }

            BindTargetHealth(target.Health);

            if (target.Health != null)
                SetBar(targetHealthFill, targetHealthText, target.Health.Current, target.Health.Max);
        }

        private void BindTargetHealth(Health health)
        {
            if (boundTargetHealth != null) boundTargetHealth.Changed -= OnTargetHealthChanged;
            boundTargetHealth = health;
            if (boundTargetHealth != null) boundTargetHealth.Changed += OnTargetHealthChanged;
        }

        private void OnTargetHealthChanged(int current, int max)
        {
            SetBar(targetHealthFill, targetHealthText, current, max);
        }

        private static void SetBar(RectTransform fill, Text label, int current, int max)
        {
            float fraction = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (fill != null) fill.localScale = new Vector3(fraction, 1f, 1f);
            if (label != null) label.text = current + " / " + max;
        }
    }
}
