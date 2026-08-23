using UnityEngine;
using UnityEngine.UI;

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
        private const float PanelWidth = 250f;
        private const float PanelHeight = 66f;
        private const float PanelGap = 12f;
        private const float BarHeight = 20f;      // толще: в WoW полоска — главный элемент панели
        private const float BarGap = 4f;
        private const float InnerPad = 7f;

        // Портрет квадратный и занимает высоту панели за вычетом полей.
        // Без него панель выглядит отладочной плашкой, а не интерфейсом игры.
        private const float PortraitSize = PanelHeight - InnerPad * 2f;
        private const float BarsLeft = InnerPad * 2f + PortraitSize;

        // Панель способностей
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
                OnLevelUp(playerExperience.Level);
            }

            if (targets != null)
            {
                targets.TargetChanged += OnTargetChanged;
                OnTargetChanged(targets.Current);
            }

            if (abilities != null) abilities.BarChanged += OnBarChanged;
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

            // Состояния кнопок (откаты, нехватка энергии) обновляются каждый
            // кадр — достаточно дать им один проход сразу, чтобы новая панель
            // не мигнула активной, если приём на самом деле не готов.
            UpdateCooldowns();
        }

        private void OnDisable()
        {
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
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            float barsWidth = PanelWidth - BarsLeft - InnerPad;
            float nameY = -InnerPad;
            float healthY = nameY - 17f;
            float secondY = healthY - BarHeight - BarGap;

            // --- Панель игрока ---
            var player = CreatePanel(root, "PlayerPanel",
                new Vector2(ScreenMargin, -ScreenMargin));

            CreatePortrait(player);

            playerNameText = CreateText(player, "Name", "Разбойник", 14, TextColor,
                new Vector2(BarsLeft, nameY), new Vector2(barsWidth, 16f));

            var healthBar = CreateBar(player, "Health", AllyHealthBack, AllyHealthColor,
                new Vector2(BarsLeft, healthY), barsWidth);
            playerHealthFill = healthBar.fill;
            playerHealthText = healthBar.label;

            var energyBar = CreateBar(player, "Energy", EnergyBack, EnergyColor,
                new Vector2(BarsLeft, secondY), barsWidth);
            playerEnergyFill = energyBar.fill;
            playerEnergyText = energyBar.label;

            // Цифры на энергии тёмные: полоска жёлтая, светлый текст на ней
            // не читается вовсе.
            playerEnergyText.color = new Color32(0x3A, 0x30, 0x14, 0xFF);

            // --- Панель цели: справа от панели игрока, скрыта без цели ---
            var target = CreatePanel(root, "TargetPanel",
                new Vector2(ScreenMargin + PanelWidth + PanelGap, -ScreenMargin));
            targetPanel = target.gameObject;

            CreatePortrait(target);

            targetNameText = CreateText(target, "Name", "", 14, TextColor,
                new Vector2(BarsLeft, nameY), new Vector2(barsWidth, 16f));

            var targetBar = CreateBar(target, "Health", HealthBack, HealthColor,
                new Vector2(BarsLeft, healthY), barsWidth);
            targetHealthFill = targetBar.fill;
            targetHealthText = targetBar.label;

            BuildComboDots(target, new Vector2(BarsLeft, secondY - 2f), barsWidth);

            targetPanel.SetActive(false);

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

                var icon = slotGo.GetComponent<Image>();
                icon.color = ability.iconColor;
                icon.raycastTarget = false;

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

                var nameLabel = CreateText(slotRect, "Name", ability.displayName, 10, TextColor,
                    new Vector2(0f, -SlotSize - 2f), new Vector2(SlotSize, 12f));
                nameLabel.alignment = TextAnchor.UpperCenter;

                slots.Add(new AbilitySlot
                {
                    ability = ability,
                    icon = icon,
                    cooldownVeil = veil,
                    cooldownText = cdText
                });
            }
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
        private void CreatePortrait(RectTransform parent)
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
            text.text = content;
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
            if (expFill != null)
            {
                float fraction = needed > 0 ? Mathf.Clamp01((float)current / needed) : 1f;
                expFill.localScale = new Vector3(fraction, 1f, 1f);
            }

            if (expText != null)
                expText.text = needed > 0 ? current + " / " + needed : "максимальный уровень";
        }

        private void OnLevelUp(int level)
        {
            // Уровень пишем рядом с именем игрока — там же, где он у цели.
            if (playerNameText != null) playerNameText.text = "Разбойник  ур. " + level;

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

        private void OnTargetChanged(Targetable target)
        {
            if (target == null)
            {
                BindTargetHealth(null);
                if (targetPanel != null) targetPanel.SetActive(false);
                return;
            }

            if (targetPanel != null) targetPanel.SetActive(true);

            if (targetNameText != null)
            {
                var defense = target.GetComponent<DefenseStats>();

                if (defense != null)
                {
                    targetNameText.text = target.DisplayName + "  ур. " + defense.Level;

                    // Цвет имени по разнице уровней. Игрок читает его быстрее,
                    // чем само число: серый значит «не трать время», красный —
                    // «не лезь». Цвета каноничные для жанра намеренно.
                    int playerLevel = playerDefense != null ? playerDefense.Level : 1;
                    targetNameText.color = LevelDifficulty.ColorOf(defense.Level, playerLevel);
                }
                else
                {
                    targetNameText.text = target.DisplayName;
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
