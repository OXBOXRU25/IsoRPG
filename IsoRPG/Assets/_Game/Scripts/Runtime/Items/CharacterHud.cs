using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.UI;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Окно персонажа: надетые вещи и итоговые характеристики.
    ///
    /// Без него экипировка работает вслепую — не видно ни что надето, ни
    /// что это дало. А смысл всей системы предметов именно в том, чтобы
    /// игрок видел, как растёт его персонаж.
    /// </summary>
    public sealed class CharacterHud : MonoBehaviour, IsoRPG.UI.IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        /// <summary>Силуэт пустого слота: заметен, но не спорит с вещами.</summary>
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.28f);

        private static readonly Color SlotEmpty = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color TextDim = new Color32(0xA8, 0xA0, 0x90, 0xFF);
        private static readonly Color StatColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);
        private static readonly Color ScrollTrack = new Color32(0x24, 0x21, 0x1B, 0x80);
        private static readonly Color ScrollHandle = new Color32(0x5A, 0x52, 0x42, 0xFF);

        private const float Margin = 18f;
        /// <summary>
        /// Все три колонки одной ширины: вещи, герой, числа.
        ///
        /// Равные доли читаются как три раздела одного окна, а разной ширины —
        /// как главный раздел и два довеска. Здесь все три равнозначны.
        /// </summary>
        private const float ColumnWidth = 250f;

        private const float SlotColumnWidth = ColumnWidth;

        /// <summary>Правая колонка с числами.</summary>
        private const float StatColumnWidth = ColumnWidth;

        /// <summary>
        /// Отступ колонки чисел от разделителя и от правого края окна.
        /// Без него подписи упираются в черту слева, а значения — в рамку
        /// справа, и колонка читается как вываленная за край.
        /// </summary>
        private const float StatInset = 12f;

        /// <summary>Ширина полосы прокрутки. Тонкая: она подсказка, не орган управления.</summary>
        private const float ScrollWidth = 5f;

        /// <summary>Витрина с моделью — между вещами и числами, как в жанре.</summary>
        private const float ModelColumnWidth = ColumnWidth;

        private const float Width = SlotColumnWidth + ModelColumnWidth + StatColumnWidth;
        /// <summary>
        /// Шаг строки. При кегле 11 шестнадцати пикселей мало: буквы
        /// соседних строк почти касаются, и колонка читается как сплошное
        /// пятно. Девятнадцать дают тот же воздух, что между слотами слева.
        /// </summary>
        private const float StatRow = 19f;

        /// <summary>Заголовок раздела: сверху воздуха больше, чем снизу.</summary>
        private const float StatHeader = 27f;
        private const float RowHeight = 30f;
        private const float Pad = 12f;
        private const float TitleHeight = 24f;
        private const float StatsHeight = 86f;

        // Слоты, которые показываем. Порядок сверху вниз — как на человеке.
        private static readonly EquipSlot[] Slots =
        {
            EquipSlot.Head,
            EquipSlot.Necklace,
            EquipSlot.Chest,
            EquipSlot.Cloak,
            EquipSlot.Hands,
            EquipSlot.Legs,
            EquipSlot.Feet,
            EquipSlot.MainHand,
            EquipSlot.OffHand,
            EquipSlot.Ranged,
            EquipSlot.Ring,
            EquipSlot.Ring2,
        };

        private static readonly Dictionary<EquipSlot, string> SlotNames = new Dictionary<EquipSlot, string>
        {
            { EquipSlot.Head, "Голова" },
            { EquipSlot.Chest, "Грудь" },
            { EquipSlot.Hands, "Кисти" },
            { EquipSlot.Legs, "Ноги" },
            { EquipSlot.Feet, "Ступни" },
            { EquipSlot.MainHand, "Правая рука" },
            { EquipSlot.OffHand, "Левая рука" },
            { EquipSlot.Ring, "Кольцо" },
            { EquipSlot.Ring2, "Кольцо" },
            { EquipSlot.Necklace, "Ожерелье" },
            { EquipSlot.Cloak, "Плащ" },
            { EquipSlot.Ranged, "Метательное" },
        };

        [SerializeField] private Equipment equipment;
        [SerializeField] private WeaponStats weapon;
        [SerializeField] private DefenseStats defense;
        [SerializeField] private Experience experience;

        private Font font;
        private GameObject window;
        /// <summary>Значения характеристик по ключу. Заголовки не хранятся.</summary>
        private readonly Dictionary<string, Text> statValues = new Dictionary<string, Text>();

        private CharacterPreview preview;

        /// <summary>Рамка витрины. Картинку в неё вставляем при первом показе.</summary>
        private RectTransform modelBox;

        private readonly Dictionary<EquipSlot, Image> slotIcons = new Dictionary<EquipSlot, Image>();
        private readonly Dictionary<EquipSlot, Text> slotLabels = new Dictionary<EquipSlot, Text>();

        /// <summary>Рисунок надетой вещи поверх плашки слота.</summary>
        private readonly Dictionary<EquipSlot, Image> slotArt = new Dictionary<EquipSlot, Image>();

        /// <summary>Подсказки пустых слотов: силуэт шлема, сапог, кольца.</summary>
        private readonly Dictionary<EquipSlot, Sprite> slotHints = new Dictionary<EquipSlot, Sprite>();

        /// <summary>Подсказка о надетой вещи.</summary>
        private readonly Dictionary<EquipSlot, IsoRPG.UI.ItemTooltipTrigger> slotTips =
            new Dictionary<EquipSlot, IsoRPG.UI.ItemTooltipTrigger>();

        /// <summary>Задаётся сборщиком сцены: какой силуэт какому слоту.</summary>
        public void SetupSlotHints(EquipSlot slot, Sprite hint) => slotHints[slot] = hint;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponentInParent<Equipment>();
            if (weapon == null) weapon = GetComponentInParent<WeaponStats>();
            if (defense == null) defense = GetComponentInParent<DefenseStats>();
            if (experience == null) experience = GetComponentInParent<Experience>();
            if (preview == null) preview = GetComponentInParent<CharacterPreview>();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void OnEnable()
        {

            // Смена языка перерисовывает окно.
            //
            // Подписи с переводом обновляются сами, но всё, что собрано из
            // кусков — «Сумка 12 / 40», названия с количеством, строки
            // наград, — пересобирается только здесь. Без этого человек
            // переключал язык и видел половину окна на прежнем.
            Loc.Changed += Refresh;
            if (equipment != null) equipment.Changed += Refresh;

            // Не только снаряжение: в окне стоят уровень, здоровье и
            // характеристики, а они меняются сами по себе — от нового
            // уровня, от таланта, от загрузки сохранения. Раньше окно
            // показывало то, что было в момент открытия: игрок брал
            // седьмой уровень и продолжал видеть шестой.
            if (experience != null) experience.Changed += OnExperienceChanged;

            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
            if (equipment != null) equipment.Changed -= Refresh;
            if (experience != null) experience.Changed -= OnExperienceChanged;
        }

        /// <summary>
        /// Событие несёт числа, а Refresh их не принимает — переходник.
        ///
        /// Опыт капает после каждого убитого, и этого достаточно: к моменту,
        /// когда игрок откроет окно после боя, оно уже пересобрано. Обновляем
        /// только при открытом окне — перестраивать содержимое закрытого
        /// после каждого удара незачем.
        /// </summary>
        private void OnExperienceChanged(int current, int needed)
        {
            if (IsOpen) Refresh();
        }

        public bool IsOpen => window != null && window.activeSelf;

        public void Toggle()
        {
            if (window == null) return;

            window.SetActive(!window.activeSelf);

            // Витрина рисуется, только пока окно открыто.
            EnsureModelView();
            if (preview != null) preview.SetVisible(window.activeSelf);

            if (window.activeSelf)
            {
                Refresh();
                IsoRPG.Audio.Sfx.OpenWindow();
            }
            else IsoRPG.Audio.Sfx.CloseWindow();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
            if (preview != null) preview.SetVisible(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || window == null) return;

            // C — как в играх жанра.
            if (keyboard.cKey.wasPressedThisFrame) Toggle();

            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("CharacterHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;

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

            // Высоту задают слоты — их число меняется редко и осознанно.
            // Числа под неё подстраиваются прокруткой: список характеристик
            // растёт с каждой механикой, и тянуть за ним окно означает
            // однажды получить окно выше экрана.
            float slotsHeight = Slots.Length * RowHeight;

            float height = TitleHeight + slotsHeight + Pad * 2f;

            var go = new GameObject("CharacterWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // Слева, под панелями игрока и цели — как окно персонажа в WoW.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Margin, -(Margin + 90f));
            rect.sizeDelta = new Vector2(Width, height);

            // Нарисованная рамка вместо плоской плашки. Заливка и
            // обводка нужны только тому, кому рамки не досталось: у неё
            // есть собственный контур, и вторая линия вокруг него читается
            // как лишний кант.
            if (!WindowChrome.ApplyFrame(go))
            {
                go.GetComponent<Image>().color = PanelColor;

                var edge = new GameObject("Edge", typeof(Image));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(rect, false);
                edgeRect.anchorMin = Vector2.zero;
                edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = new Vector2(-1f, -1f);
                edgeRect.offsetMax = new Vector2(1f, 1f);
                edge.transform.SetAsFirstSibling();
                edge.GetComponent<Image>().color = PanelEdge;
            }

            var title = MakeText(rect, "Title", "Персонаж", 14, TextColor);
            Place(title, new Vector2(Pad, -Pad), new Vector2(Width - Pad * 2f, TitleHeight));
            title.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < Slots.Length; i++)
                BuildRow(rect, Slots[i], -(Pad + TitleHeight + i * RowHeight));

            // Тонкая черта между колонками: без неё числа читаются как
            // продолжение списка вещей.
            var divider = new GameObject("Divider", typeof(Image));
            var dividerRect = (RectTransform)divider.transform;
            dividerRect.SetParent(rect, false);
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(0f, 1f);
            dividerRect.pivot = new Vector2(0f, 1f);
            dividerRect.anchoredPosition = new Vector2(SlotColumnWidth - 8f, -(Pad + TitleHeight));
            dividerRect.sizeDelta = new Vector2(1f, height - TitleHeight - Pad * 2f);

            var dividerImage = divider.GetComponent<Image>();
            dividerImage.color = PanelEdge;
            dividerImage.raycastTarget = false;

            BuildModel(rect, height);
            BuildStats(rect, height);

            IsoRPG.UI.WindowChrome.AddCloseButton(rect, font, Close);


            window = go;
            window.SetActive(false);
        }

        private void BuildRow(RectTransform parent, EquipSlot slot, float y)
        {
            // Квадратик предмета
            var iconGo = new GameObject(slot + "Icon", typeof(Image), typeof(Button));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(Pad, y);
            iconRect.sizeDelta = new Vector2(26f, 26f);

            var icon = iconGo.GetComponent<Image>();
            icon.color = SlotEmpty;
            slotIcons[slot] = icon;

            // Картинка отдельным слоем: плашка остаётся цветом редкости, а
            // рисунок сверху не перекрашивается вместе с ней.
            var art = new GameObject("Art", typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(iconRect, false);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(1.5f, 1.5f);
            artRect.offsetMax = new Vector2(-1.5f, -1.5f);

            var artImage = art.GetComponent<Image>();
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;
            artImage.enabled = false;

            slotArt[slot] = artImage;

            slotTips[slot] = iconGo.AddComponent<IsoRPG.UI.ItemTooltipTrigger>();

            var captured = slot;
            iconGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (equipment != null) equipment.Unequip(captured);
            });

            // Подпись: название слота или предмета
            var label = MakeText(parent, slot + "Label", SlotNames[slot], 12, TextDim);
            Place(label, new Vector2(Pad + 32f, y - 4f), new Vector2(Width - Pad * 2f - 32f, 20f));
            slotLabels[slot] = label;
        }

        private void Refresh()
        {
            if (equipment == null) return;

            foreach (var slot in Slots)
            {
                var stack = equipment.GetSlot(slot);

                if (stack.IsEmpty)
                {
                    slotIcons[slot].color = SlotEmpty;
                    LocalizedText.Bind(slotLabels[slot], SlotNames[slot]);
                    slotLabels[slot].color = TextDim;

                    // В пустом слоте показываем приглушённый силуэт того, что
                    // сюда надевается. Пустой квадрат ничего не сообщает, а
                    // силуэт объясняет назначение без единого слова.
                    if (slotArt.TryGetValue(slot, out var emptyArt))
                    {
                        bool hasHint = slotHints.TryGetValue(slot, out var hint) && hint != null;

                        emptyArt.sprite = hasHint ? hint : null;
                        emptyArt.color = HintColor;
                        emptyArt.enabled = hasHint;
                    }

                    if (slotTips.TryGetValue(slot, out var emptyTip))
                        emptyTip.Setup(null, experience);
                }
                else
                {
                    slotIcons[slot].color = stack.Item.RarityColor;
                    LocalizedText.Bind(slotLabels[slot], stack.Item.displayName);
                    slotLabels[slot].color = stack.Item.RarityColor;

                    if (slotArt.TryGetValue(slot, out var wornArt))
                    {
                        wornArt.sprite = stack.Item.icon;
                        wornArt.color = Color.white;
                        wornArt.enabled = stack.Item.icon != null;
                    }

                    if (slotTips.TryGetValue(slot, out var wornTip))
                        wornTip.Setup(stack.Item, experience);
                }
            }

            RefreshStats();
        }

        /// <summary>
        /// Числа справа. Порядок разделов как в играх жанра: сначала то, что
        /// держит в живых, потом то, из чего всё считается, и только потом
        /// сам бой. Игрок читает сверху вниз и останавливается там, где
        /// нашёл ответ.
        /// </summary>
        private void RefreshStats()
        {
            if (statValues.Count == 0) return;

            var bonus = equipment != null ? equipment.TotalStatBonus() : default;
            var health = GetComponentInParent<Health>();
            var energy = GetComponentInParent<ResourcePool>();
            var talents = GetComponentInParent<IsoRPG.Progression.TalentBook>();

            Set("level", experience != null ? experience.Level.ToString() : "1");
            Set("health", health != null ? health.Max.ToString() : "—");
            Set("energy", energy != null ? energy.Max.ToString() : "—");
            Set("armor", defense != null ? defense.Armor.ToString() : "0");

            // Показываем полные характеристики, а не прибавку от вещей.
            //
            // Раньше в окне стояло только то, что дало снаряжение: герой
            // седьмого уровня видел «Сила 0» и справедливо считал, что
            // характеристика не работает. Работает — просто основа и прирост
            // за уровни в это число не входили.
            var stats = GetComponentInParent<IsoRPG.Progression.TalentStats>();
            var total = stats != null ? stats.TotalStats : bonus;

            Set("strength", total.Strength.ToString());
            Set("agility", total.Agility.ToString());
            Set("stamina", total.Stamina.ToString());

            if (weapon != null)
            {
                Set("weapon", weapon.WeaponName);
                Set("damage", weapon.WeaponDamage.ToString());
                Set("speed", Loc.F("{0} с", weapon.AttackInterval.ToString("0.0")));

                // Урон в секунду — единственное число, которым сравнивают
                // быстрый кинжал с медленным топором, не считая в уме.
                float dps = weapon.WeaponDamage / Mathf.Max(0.1f, weapon.AttackInterval);
                Set("dps", dps.ToString("0.#"));
            }
            else
            {
                Set("weapon", "нет");
                Set("damage", "—");
                Set("speed", "—");
                Set("dps", "—");
            }

            // Криты и промахи держатся в боевом коде, а таланты их двигают.
            // Показываем итог, а не базу: игрок вкладывает очки именно ради
            // этой цифры и должен видеть, что она сдвинулась.
            float crit = CombatMath.DefaultCritChance;
            if (talents != null) crit += talents.Bonus(IsoRPG.Progression.TalentEffect.CritChance);

            float critMult = CombatMath.DefaultCritMultiplier;
            if (talents != null)
                critMult += talents.Bonus(IsoRPG.Progression.TalentEffect.CritMultiplier);

            Set("crit", Mathf.RoundToInt(Mathf.Clamp01(crit) * 100f) + "%");
            Set("critmult", critMult.ToString("0.##") + " x");
            Set("miss", Mathf.RoundToInt(CombatMath.DefaultMissChance * 100f) + "%");
        }

        private void Set(string key, string value)
        {
            if (statValues.TryGetValue(key, out var text) && text != null) text.text = value;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Витрина с моделью героя. Пустая рамка, если модели нет: окно
        /// должно работать и без неё, а дыра посреди окна выглядит поломкой.
        /// </summary>
        private void BuildModel(RectTransform parent, float height)
        {
            float left = SlotColumnWidth + 4f;
            float top = Pad + TitleHeight;
            float boxHeight = height - top - Pad;

            var backGo = new GameObject("ModelBack", typeof(Image));
            var backRect = (RectTransform)backGo.transform;
            backRect.SetParent(parent, false);
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(left, -top);
            backRect.sizeDelta = new Vector2(ModelColumnWidth - 12f, boxHeight);

            var back = backGo.GetComponent<Image>();

            // Прозрачная, а не своего цвета: витрина посреди окна не должна
            // читаться отдельной плашкой — окно одно, и подложка под моделью
            // делила его надвое.
            //
            // Но и убрать её нельзя: Image здесь ловит перетаскивание, которым
            // героя разворачивают. Прозрачная картинка события принимает
            // по-прежнему, а видно её не будет.
            back.color = new Color(0f, 0f, 0f, 0f);
            back.raycastTarget = true;

            var spin = backGo.AddComponent<ModelSpinner>();
            spin.Setup(this);

            modelBox = backRect;
        }

        /// <summary>
        /// Вставить картинку витрины, если её ещё нет.
        ///
        /// Не в постройке окна: порядок Awake между компонентами Unity не
        /// задаёт, и окно успевало собраться раньше, чем витрина заводила
        /// свою текстуру. Снаружи это выглядело как чёрный прямоугольник
        /// вместо героя — ошибки нет, ссылки просто ещё не было.
        /// </summary>
        private void EnsureModelView()
        {
            if (modelBox == null || preview == null || preview.Texture == null) return;
            if (modelBox.childCount > 0) return;

            var viewGo = new GameObject("Model", typeof(RawImage));
            var viewRect = (RectTransform)viewGo.transform;
            viewRect.SetParent(modelBox, false);
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = new Vector2(3f, 3f);
            viewRect.offsetMax = new Vector2(-3f, -3f);

            var view = viewGo.GetComponent<RawImage>();
            view.texture = preview.Texture;
            view.raycastTarget = false;
        }

        /// <summary>
        /// Колонка чисел с прокруткой.
        ///
        /// Полоса тонкая и появляется только когда есть что прокручивать:
        /// это не список, который листают, а справка, в которую заглядывают.
        /// Толстый скролл рядом с четырьмя строками выглядел бы как ошибка.
        /// </summary>
        private void BuildStats(RectTransform parent, float height)
        {
            float left = SlotColumnWidth + ModelColumnWidth;
            float top = Pad + TitleHeight;
            float viewHeight = height - top - Pad;

            var scrollGo = new GameObject("StatsScroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = new Vector2(0f, 1f);
            scrollRect.anchorMax = new Vector2(0f, 1f);
            scrollRect.pivot = new Vector2(0f, 1f);
            scrollRect.anchoredPosition = new Vector2(left, -top);
            scrollRect.sizeDelta = new Vector2(StatColumnWidth, viewHeight);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;

            var bar = BuildScrollbar(scrollRect, viewHeight);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 22f;
            scroll.verticalScrollbar = bar;

            // Полоса прячется сама, когда всё влезло: в окне и без неё тесно.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            float y = 0f;

            var parentBackup = parent;
            parent = content;

            y = StatSection(parent, "Общее", y);
            y = StatLine(parent, "level", "Уровень", y);
            y = StatLine(parent, "health", "Здоровье", y);
            y = StatLine(parent, "energy", "Энергия", y);
            y = StatLine(parent, "armor", "Броня", y);

            y = StatSection(parent, "Характеристики", y);
            y = StatLine(parent, "strength", "Сила", y);
            y = StatLine(parent, "agility", "Ловкость", y);
            y = StatLine(parent, "stamina", "Выносливость", y);

            y = StatSection(parent, "Ближний бой", y);

            // Имя оружия — на всю ширину колонки: «Кинжал бандита» рядом с
            // подписью «Оружие» не помещается и вылезает за край окна.
            y = StatWideLine(parent, "weapon", y);
            y = StatLine(parent, "damage", "Урон", y);
            y = StatLine(parent, "speed", "Скорость", y);
            y = StatLine(parent, "dps", "Урон в секунду", y);
            y = StatLine(parent, "crit", "Шанс крита", y);
            y = StatLine(parent, "critmult", "Сила крита", y);
            y = StatLine(parent, "miss", "Шанс промаха", y);

            // Высота содержимого — по последней строке. Без неё прокрутка не
            // знает, докуда ехать, и стоит на месте при любом списке.
            content.sizeDelta = new Vector2(0f, -y + Pad);

            parent = parentBackup;
        }

        /// <summary>Тонкая полоса прокрутки: две плашки, без стрелок.</summary>
        private Scrollbar BuildScrollbar(RectTransform parent, float height)
        {
            var go = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ScrollWidth, 0f);

            go.GetComponent<Image>().color = ScrollTrack;

            var slideGo = new GameObject("SlidingArea", typeof(RectTransform));
            var slide = (RectTransform)slideGo.transform;
            slide.SetParent(rect, false);
            slide.anchorMin = Vector2.zero;
            slide.anchorMax = Vector2.one;
            slide.offsetMin = Vector2.zero;
            slide.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(Image));
            var handle = (RectTransform)handleGo.transform;
            handle.SetParent(slide, false);
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;

            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = ScrollHandle;

            var bar = go.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;

            return bar;
        }

        private float StatSection(RectTransform parent, string caption, float y)
        {
            var text = MakeText(parent, "Section" + caption, caption, 12, StatColor);

            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(StatInset, y - 6f);
            rect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f - ScrollWidth, StatHeader);
            text.alignment = TextAnchor.LowerLeft;

            // Плюс зазор под заголовком: без него первая строка раздела
            // приклеена к нему и читается как его продолжение, а не как
            // отдельная величина.
            return y - StatHeader - 4f;
        }

        /// <summary>
        /// Строка во всю ширину колонки. Для значений, которые не влезают
        /// рядом с подписью, — имён вещей прежде всего.
        /// </summary>
        private float StatWideLine(RectTransform parent, string key, float y)
        {
            var value = MakeText(parent, key + "Value", "—", 11, TextColor);

            var rect = (RectTransform)value.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(StatInset, y);
            rect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f - ScrollWidth, StatRow);
            value.alignment = TextAnchor.MiddleLeft;

            statValues[key] = value;
            return y - StatRow;
        }

        /// <summary>
        /// Что делает каждая характеристика. Показывается по наведению.
        ///
        /// Держим здесь, рядом с окном, а не в данных: это описание правил
        /// игры, и меняется оно вместе с формулами в коде, а не отдельно.
        /// </summary>
        private static string Explain(string key)
        {
            switch (key)
            {
                case "level":
                    return "Растёт с опытом. Каждый уровень прибавляет характеристики " +
                           "и очко талантов.";

                case "health":
                    return "Сколько урона выдержит герой. Даёт выносливость: " +
                           "единица выносливости — десять здоровья.";

                case "energy":
                    return "Тратится на приёмы и восстанавливается сама. " +
                           "Не зависит от снаряжения.";

                case "armor":
                    return "Снижает получаемый урон долей, а не вычитанием: " +
                           "работает одинаково и против слабых ударов, и против " +
                           "сильных. Чем выше уровень бьющего, тем меньше помогает.";

                case "strength":
                    return "Сила. Прибавляет урон в ближнем бою — процент за единицу. " +
                           "Разбойнику полезна, но ловкость полезнее.";

                case "agility":
                    return "Ловкость. Главная характеристика разбойника: повышает шанс " +
                           "критического удара и добавляет броню.";

                case "stamina":
                    return "Выносливость. Превращается в здоровье: единица — десять " +
                           "здоровья. Не влияет ни на что другое.";

                case "damage":
                    return "Урон оружия за один удар, до брони цели. Крит умножает " +
                           "его, броня уменьшает.";

                case "speed":
                    return "Секунд между ударами. Меньше — чаще бьёшь.";

                case "dps":
                    return "Урон в секунду: урон, делённый на скорость. Число для " +
                           "сравнения оружия между собой, а не то, что увидишь в бою.";

                case "crit":
                    return "Вероятность критического удара. Растёт от ловкости " +
                           "и талантов.";

                case "critmult":
                    return "Во сколько раз крит сильнее обычного удара.";

                case "miss":
                    return "Вероятность задеть вскользь и нанести меньше урона. " +
                           "Растёт, если цель выше уровнем.";
            }

            return string.Empty;
        }

        private float StatLine(RectTransform parent, string key, string caption, float y)
        {
            var label = MakeText(parent, key + "Label", caption, 11, TextDim);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(StatInset, y);
            labelRect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f - ScrollWidth, StatRow);
            label.alignment = TextAnchor.MiddleLeft;

            var value = MakeText(parent, key + "Value", "—", 11, TextColor);

            // Значение обрезается по колонке, а не вылезает наружу: длинное
            // число лучше поджать, чем вывести за рамку окна.
            value.horizontalOverflow = HorizontalWrapMode.Wrap;

            var valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = new Vector2(0f, 1f);
            valueRect.anchorMax = new Vector2(0f, 1f);
            valueRect.pivot = new Vector2(0f, 1f);
            valueRect.anchoredPosition = new Vector2(StatInset, y);
            valueRect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f - ScrollWidth, StatRow);
            value.alignment = TextAnchor.MiddleRight;

            statValues[key] = value;

            string hint = Explain(key);

            if (!string.IsNullOrEmpty(hint))
            {
                var hover = new GameObject(key + "Hover", typeof(Image));
                hover.transform.SetParent(parent, false);

                var hoverRect = (RectTransform)hover.transform;
                hoverRect.anchorMin = new Vector2(0f, 1f);
                hoverRect.anchorMax = new Vector2(0f, 1f);
                hoverRect.pivot = new Vector2(0f, 1f);
                hoverRect.anchoredPosition = new Vector2(StatInset, y);
                hoverRect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f, StatRow);

                // Полностью прозрачная: она нужна только чтобы ловить
                // указатель. Совсем без картинки объект указатель не ловит.
                var plate = hover.GetComponent<Image>();
                plate.color = new Color(1f, 1f, 1f, 0f);
                plate.raycastTarget = true;

                // Под подписями по порядку отрисовки — иначе перекрыла бы
                // текст, будь у неё цвет.
                hoverRect.SetAsFirstSibling();

                var trigger = hover.AddComponent<IsoRPG.UI.TextTooltipTrigger>();
                trigger.Setup(caption, hint);
            }

            return y - StatRow;
        }

        // ------------------------------------------------------------------

        private Text MakeText(RectTransform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

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

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Отдать витрине доворот, который натянул игрок мышью.</summary>
        internal void SpinModel(float degrees)
        {
            if (preview != null) preview.Spin(degrees);
        }
    }

    /// <summary>
    /// Разворот героя перетаскиванием по витрине.
    ///
    /// Отдельным компонентом, а не строчкой в окне: интерфейсы перетаскивания
    /// требуют MonoBehaviour на том же объекте, где стоит картинка, ловящая
    /// события. Здесь этот объект — прозрачная подложка витрины.
    ///
    /// Слушаем именно перетаскивание, а не движение мыши: без нажатой кнопки
    /// герой крутился бы от одного проезда курсора мимо, и попасть по слоту
    /// снаряжения рядом стало бы невозможно.
    /// </summary>
    internal sealed class ModelSpinner : MonoBehaviour, UnityEngine.EventSystems.IDragHandler
    {
        /// <summary>
        /// Градусов на пиксель. Треть — подобрано так, чтобы полный оборот
        /// занимал примерно ширину витрины: тогда движение читается как
        /// «толкнул героя рукой», а не как рывок.
        /// </summary>
        private const float DegreesPerPixel = 0.45f;

        private CharacterHud owner;

        public void Setup(CharacterHud hud)
        {
            owner = hud;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (owner == null) return;

            // Минус: тянешь вправо — герой поворачивается к тебе правым боком,
            // как будто ты крутишь его самого, а не сцену вокруг него.
            owner.SpinModel(-eventData.delta.x * DegreesPerPixel);
        }
    }
}
