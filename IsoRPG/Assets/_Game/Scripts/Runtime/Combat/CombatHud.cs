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
        private static readonly Color HealthBack = new Color32(0x2A, 0x14, 0x12, 0xFF);
        private static readonly Color PortraitColor = new Color32(0x4A, 0x42, 0x36, 0xFF);
        private static readonly Color EnergyColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color EnergyBack = new Color32(0x2C, 0x24, 0x10, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color TextDim = new Color32(0xA8, 0xA0, 0x90, 0xFF);

        // --- Геометрия. Отступ от края одинаковый везде: один токен, не три ---
        private const float ScreenMargin = 18f;
        // Ширина панели и её высота связаны пропорцией картинки: 1932 на 814.
        // Задавать высоту отдельно нельзя — рамка растянется и торцы поплывут.
        private const float PanelWidth = 300f;
        private const float PanelHeight = PanelWidth * 718f / 1865f;

        /// <summary>
        /// Где внутри нарисованной рамки лежат гнёзда — доли от её размера.
        ///
        /// Сняты с самой картинки: круг портрета по центру слева, два жёлоба
        /// справа от него. Пока рамку не перерисовали, эти числа не меняются.
        /// </summary>
        // Числа сняты с картинки замером: от центра гнезда наружу до
        // золотого канта. На глаз по сетке они выходили на пару процентов
        // мимо — и полосы вылезали за жёлоба.
        private const float PortraitCenterX = 0.198f;
        private const float PortraitCenterY = 0.477f;
        private const float PortraitDiameter = 0.252f;   // от ширины рамки

        private const float BarsFrom = 0.355f;
        private const float BarsTo = 0.903f;
        private const float TopBarFrom = 0.329f;
        private const float TopBarTo = 0.460f;
        private const float LowBarFrom = 0.550f;
        private const float LowBarTo = 0.680f;

        /// <summary>
        /// Рамка цели: 2172 на 724, полоса одна и лежит правее портрета.
        /// </summary>
        // Шире геройской намеренно: у рамки врага портрет занимает меньшую
        // долю ширины (0.184 против 0.252), и при равной ширине панелей его
        // портрет выходил заметно мельче. Подобрано так, чтобы круги на
        // экране получились одного размера.
        private const float EnemyPanelWidth = 420f;
        private const float EnemyPanelHeight = EnemyPanelWidth * 675f / 2140f;

        private const float EnemyPortraitCenterX = 0.191f;
        private const float EnemyPortraitCenterY = 0.470f;
        private const float EnemyPortraitDiameter = 0.184f;

        /// <summary>
        /// Рамка мирного: 2117 на 685, вместо жёлоба — табличка под имя.
        /// Круг у неё сквозной, поэтому портрет там виден целиком.
        /// </summary>
        private const float NeutralPanelWidth = 400f;
        private const float NeutralPanelHeight = NeutralPanelWidth * 685f / 2117f;

        private const float NeutralPortraitCenterX = 0.160f;
        private const float NeutralPortraitCenterY = 0.481f;
        private const float NeutralPortraitDiameter = 0.193f;

        private const float NeutralPlateFrom = 0.327f;
        private const float NeutralPlateTo = 0.960f;
        private const float NeutralPlateTop = 0.388f;
        private const float NeutralPlateBottom = 0.677f;

        private const float EnemyBarFrom = 0.351f;
        private const float EnemyBarTo = 0.911f;
        private const float EnemyBarTop = 0.400f;
        private const float EnemyBarBottom = 0.578f;
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

        private const float SlotSize = 48f;
        private const float SlotGap = 6f;

        private static readonly Color ComboEmpty = new Color32(0x2E, 0x2A, 0x22, 0xFF);
        private static readonly Color ComboFull = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
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
            var self = GetComponent<Targetable>();

            if (self != null && playerPortrait != null && self.Portrait != null)
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

            // Имя с уровнем — над рамкой, а не внутри: внутри рамки места нет,
            // всё занято портретом и жёлобами.
            playerNameText = CreateText(player, "Name", "Разбойник", 13, TextColor,
                new Vector2(PanelWidth * BarsFrom, 2f),
                new Vector2(PanelWidth * (BarsTo - BarsFrom), 16f));

            var healthBar = CreateGrooveBar(player, "Health", AllyHealthColor,
                                            TopBarFrom, TopBarTo);
            playerHealthFill = healthBar.fill;
            playerHealthText = healthBar.label;

            var energyBar = CreateGrooveBar(player, "Energy", EnergyColor,
                                            LowBarFrom, LowBarTo);
            playerEnergyFill = energyBar.fill;
            playerEnergyText = energyBar.label;

            // Цифры на энергии тёмные: полоска жёлтая, светлый текст на ней
            // не читается вовсе.
            playerEnergyText.color = new Color32(0x3A, 0x30, 0x14, 0xFF);

            // --- Панель цели: справа от панели игрока, скрыта без цели ---
            var target = CreateFramedPanel(root, "TargetPanel", "UI/Frame_Enemy",
                new Vector2(ScreenMargin + PanelWidth + PanelGap, -ScreenMargin),
                EnemyPanelWidth, EnemyPanelHeight);
            targetPanel = target.gameObject;

            targetPortrait = CreateSlotPortrait(target, EnemyPanelWidth, EnemyPanelHeight,
                                                EnemyPortraitCenterX, EnemyPortraitCenterY,
                                                EnemyPortraitDiameter);

            targetNameText = CreateText(target, "Name", "", 13, TextColor,
                new Vector2(EnemyPanelWidth * EnemyBarFrom, 2f),
                new Vector2(EnemyPanelWidth * (EnemyBarTo - EnemyBarFrom), 16f));

            var targetBar = CreateGrooveBar(target, "Health", HealthColor,
                                            EnemyBarTop, EnemyBarBottom,
                                            EnemyBarFrom, EnemyBarTo);
            targetHealthFill = targetBar.fill;
            targetHealthText = targetBar.label;

            // Точки комбо — под рамкой: внутри неё места нет, полоса одна.
            BuildComboDots(target, new Vector2(EnemyPanelWidth * EnemyBarFrom,
                                               -EnemyPanelHeight - 4f),
                           EnemyPanelWidth * (EnemyBarTo - EnemyBarFrom));

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
            neutralNameText = CreateText(neutral, "Name", "", 14, TextColor,
                Vector2.zero, Vector2.zero);

            var neutralNameRect = (RectTransform)neutralNameText.transform;
            neutralNameRect.anchorMin = new Vector2(NeutralPlateFrom, 1f - NeutralPlateBottom);
            neutralNameRect.anchorMax = new Vector2(NeutralPlateTo, 1f - NeutralPlateTop);
            neutralNameRect.offsetMin = Vector2.zero;
            neutralNameRect.offsetMax = Vector2.zero;
            neutralNameText.alignment = TextAnchor.MiddleCenter;

            neutralPanel.SetActive(false);

            BuildAbilityBar(root);
            BuildExperienceBar(root);
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
        private void BuildComboDots(RectTransform parent, Vector2 position, float width)
        {
            comboDots.Clear();

            int count = combo != null ? combo.MaxPoints : 5;
            float size = 11f;
            float gap = 5f;
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
                rect.anchoredPosition = new Vector2(startX + i * (size + gap), position.y);
                rect.sizeDelta = new Vector2(size, size);

                var image = go.GetComponent<Image>();
                image.color = ComboEmpty;
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

            if (abilities == null || abilities.Abilities.Count == 0) return;

            int count = abilities.Abilities.Count;
            float totalWidth = count * SlotSize + (count - 1) * SlotGap;

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
            var plate = Resources.Load<Sprite>("UI/Frame_Abilities");

            if (plate != null)
            {
                var frameGo = new GameObject("Frame", typeof(Image));
                var frameRect = (RectTransform)frameGo.transform;
                frameRect.SetParent(barRect, false);

                frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.anchoredPosition = Vector2.zero;
                // Запас по высоте больше, чем кажется нужным: границы
                // растяжения занимают по 32 точки сверху и снизу, и при
                // высоте панели в 78 на середину оставалось два пикселя —
                // рамка схлопывалась в золотую ниточку между иконками.
                frameRect.sizeDelta = new Vector2(totalWidth + 96f, SlotSize + 34f);

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
            }

            for (int i = 0; i < count; i++)
            {
                var ability = abilities.Abilities[i];
                float x = i * (SlotSize + SlotGap);

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

            var art = Resources.Load<Sprite>(sprite);

            if (art == null)
            {
                Debug.LogWarning("[IsoRPG] Нет спрайта " + sprite +
                                 " — панель нарисована плашкой. Прогони " +
                                 "Tools/IsoRPG/Настроить панели интерфейса.");

                return CreatePanel(parent, name, position);
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
            image.sprite = art;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

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

            // Круглое окно, за края которого портрет не вылезет.
            //
            // Портреты рисуются квадратными, а гнездо круглое: без обрезки
            // плечи и капюшон торчат за золотое кольцо, и панель выглядит
            // так, будто картинку положили сверху, а не вставили внутрь.
            var maskGo = new GameObject("PortraitMask", typeof(Image), typeof(Mask));
            var maskRect = (RectTransform)maskGo.transform;
            maskRect.SetParent(panel, false);

            var maskImage = maskGo.GetComponent<Image>();
            maskImage.sprite = CircleSprite();
            maskImage.raycastTarget = false;

            var mask = maskGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var go = new GameObject("Portrait", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(maskRect, false);

            float size = width * diameter;

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
        private (RectTransform fill, Text label) CreateGrooveBar(
            RectTransform panel, string name, Color color, float fromY, float toY,
            float fromX = -1f, float toX = -1f)
        {
            if (fromX < 0f) fromX = BarsFrom;
            if (toX < 0f) toX = BarsTo;

            var host = new GameObject(name, typeof(RectTransform));
            var hostRect = (RectTransform)host.transform;
            hostRect.SetParent(panel, false);

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

            var label = CreateText(hostRect, "Value", "", 11, TextColor,
                                   Vector2.zero, Vector2.zero);
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
        /// Показать уровень рядом с именем. Отдельно от события: при
        /// старте подпись нужна, а праздновать там нечего — прямой вызов
        /// обработчика играл джингл повышения при каждом запуске игры.
        /// </summary>
        private void ShowPlayerLevel(int level)
        {
            if (playerNameText != null) playerNameText.text = Loc.F("Разбойник  ур. {0}", level);
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
            for (int i = 0; i < comboDots.Count; i++)
                comboDots[i].color = i < points ? ComboFull : ComboEmpty;
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
