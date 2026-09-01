using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Подложка пустого гнезда. 0.28 было почти не видно на тёмной панели.</summary>
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.7f);

        private static readonly Color SlotEmpty = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color TextDim = new Color32(0xA8, 0xA0, 0x90, 0xFF);
        private static readonly Color StatColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);
        private static readonly Color ScrollTrack = new Color32(0x24, 0x21, 0x1B, 0x80);
        private static readonly Color ScrollHandle = new Color32(0x5A, 0x52, 0x42, 0xFF);

        private const float Margin = 18f;

        /// <summary>
        /// Ширина окна. Снята с образца 01.09.2026: у WoW 484 при экране
        /// 1920 x 1200 — четверть ширины. У нас было 750: три равные колонки
        /// в ряд, и окно читалось таблицей, а не портретом героя.
        /// </summary>
        private const float Width = 490f;

        /// <summary>Сторона гнезда под вещь. В образце 40 при ширине окна 484.</summary>
        private const float Slot = 40f;

        /// <summary>Шаг между гнёздами по вертикали: сторона плюс зазор 4.</summary>
        private const float SlotStep = 44f;

        /// <summary>Колонка гнёзд: сама вещь плюс поля до края окна.</summary>
        private const float SlotColumnWidth = Slot + 14f;

        /// <summary>Третьей колонки больше нет: числа ушли под модель, как в образце.</summary>
        /// <summary>Блок чисел занимает всю ширину модели: он теперь под ней, а не сбоку.</summary>
        private const float StatColumnWidth = (ModelColumnWidth - 14f) * 0.5f;

        /// <summary>
        /// Отступ колонки чисел от края. Без него подписи упираются в рамку,
        /// и колонка читается как вываленная за край.
        /// </summary>
        private const float StatInset = 12f;

        /// <summary>Ширина полосы прокрутки. Тонкая: она подсказка, не орган управления.</summary>
        private const float ScrollWidth = 5f;

        /// <summary>Витрина с моделью — между колонками вещей, во всю их ширину.</summary>
        private const float ModelColumnWidth = Width - SlotColumnWidth * 2f;

        /// <summary>Шаг строки характеристик. В образце 22 при кегле 13.</summary>
        private const float StatRow = 22f;

        /// <summary>Заголовок раздела: сверху воздуха больше, чем снизу.</summary>
        private const float StatHeader = 26f;

        private const float RowHeight = SlotStep;
        private const float Pad = 14f;
        private const float TitleHeight = 24f;

        /// <summary>Подзаголовок под именем: «Человек, разбойник 1-го уровня» в образце.</summary>
        private const float SubtitleHeight = 18f;

        private const float StatsHeight = 212f;

        // --- Карта окна. Все позиции заданы явно, сверху вниз. ---
        //
        // Так, а не выводом из соседних величин: раскладка, собранная из
        // «высота минус то, плюс это», однажды перестаёт сходиться, и числа
        // наезжают на модель — ровно это Павлон и увидел 01.09.2026.

        /// <summary>Верх содержимого: сразу под заголовком с подзаголовком.</summary>
        private const float ContentTop = Pad + TitleHeight + SubtitleHeight;

        /// <summary>Витрина с героем. В образце он занимает верхние две трети окна.</summary>
        private const float ModelHeight = 270f;

        /// <summary>Числа — сразу под моделью.</summary>
        private const float StatsTop = ContentTop + ModelHeight + 8f;

        /// <summary>Полоса оружия — под числами.</summary>
        private const float WeaponsTop = StatsTop + StatsHeight + 10f;

        /// <summary>Полная высота окна. В образце 603 при экране 1920 x 1200.</summary>
        private const float WindowHeight = WeaponsTop + Slot + Pad;

        /// <summary>
        /// Левая колонка: то, что надевают на корпус, сверху вниз по телу.
        ///
        /// Раскладка снята с образца 01.09.2026 — в WoW гнёзда стоят двумя
        /// колонками по краям окна, а между ними в полный рост стоит герой.
        /// Прежний одиночный столбец из двенадцати строк с подписями занимал
        /// треть окна и не оставлял модели места.
        /// </summary>
        private static readonly EquipSlot[] LeftSlots =
        {
            EquipSlot.Head,
            EquipSlot.Necklace,
            EquipSlot.Shoulders,
            EquipSlot.Cloak,
            EquipSlot.Chest,
            EquipSlot.Shirt,
            EquipSlot.Tabard,
            EquipSlot.Wrists,
        };

        /// <summary>Правая колонка: руки, ноги и украшения.</summary>
        private static readonly EquipSlot[] RightSlots =
        {
            EquipSlot.Hands,
            EquipSlot.Waist,
            EquipSlot.Legs,
            EquipSlot.Feet,
            EquipSlot.Ring,
            EquipSlot.Ring2,
            EquipSlot.Trinket,
            EquipSlot.Trinket2,
        };

        /// <summary>Оружие — полосой под моделью, как в образце.</summary>
        private static readonly EquipSlot[] BottomSlots =
        {
            EquipSlot.MainHand,
            EquipSlot.OffHand,
            EquipSlot.Ranged,

            // Колчан — справа от дальнего боя, как в образце: луку нужны
            // стрелы, метательным кинжалам снаряды не нужны, и гнездо
            // просто стоит пустым.
            EquipSlot.Ammo,
        };

        /// <summary>Все гнёзда одним списком — для обхода при обновлении.</summary>
        private static readonly EquipSlot[] Slots =
            LeftSlots.Concat(RightSlots).Concat(BottomSlots).ToArray();
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

            // Слоты, добавленные 01.09.2026 под набор иконок Павлона.
            // Без имени здесь построение окна падало на первом же таком
            // гнезде и обрывалось: не рисовались ни правая колонка, ни
            // модель, ни числа — окно выглядело пустой панелью.
            { EquipSlot.Shoulders, "Плечи" },
            { EquipSlot.Wrists, "Запястья" },
            { EquipSlot.Waist, "Пояс" },
            { EquipSlot.Ammo, "Колчан" },
            { EquipSlot.Trinket, "Аксессуар" },
            { EquipSlot.Trinket2, "Аксессуар" },
            { EquipSlot.Tabard, "Гербовая накидка" },
            { EquipSlot.Shirt, "Рубашка" },
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
        private Text subtitle;

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


        /// <summary>
        /// Подхватывает подложки пустых слотов из ресурсов.
        ///
        /// Имя файла совпадает с именем слота, поэтому новый слот получает
        /// свою подложку сам, без правки кода: положил рядом Tabard.png — и
        /// она появилась в слоте гербовой накидки. Иконки нарезаны 01.09.2026
        /// из сеток Павлона и выровнены по яркости.
        ///
        /// Грузим один раз при сборке окна: Resources.Load в обновлении
        /// слота стоил бы поиска по всем ресурсам на каждую перерисовку.
        /// </summary>
        private void LoadSlotHints()
        {
            foreach (var slot in Slots)
            {
                if (slotHints.ContainsKey(slot) && slotHints[slot] != null) continue;

                var sprite = Resources.Load<Sprite>("SlotIcons/" + slot);
                if (sprite != null) slotHints[slot] = sprite;
            }
        }

        private void Build()
        {
            LoadSlotHints();

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
            // Высота: заголовок с подзаголовком, самая длинная колонка
            // гнёзд, блок чисел под моделью и полоса оружия. В образце
            // 603 при экране 1920 x 1200 — у нас выходит близко к тому.
            float slotsHeight = Mathf.Max(LeftSlots.Length, RightSlots.Length) * SlotStep;

            float height = WindowHeight;

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

            // Подзаголовок под именем — как в образце («Человек, разбойник
            // 1-го уровня»). Расы и класса у нас пока нет, поэтому уровень:
            // это единственное, что герой о себе уже сообщает.
            subtitle = MakeText(rect, "Subtitle", "", 12, TextDim);
            Place(subtitle, new Vector2(Pad, -(Pad + TitleHeight)), new Vector2(Width - Pad * 2f, SubtitleHeight));
            subtitle.alignment = TextAnchor.MiddleCenter;

            // Раскладка по образцу WoW: гнёзда двумя колонками по краям,
            // между ними герой в полный рост, оружие полосой под ним.
            float slotsTop = -ContentTop;
            float rightX = Width - Pad - Slot;

            for (int i = 0; i < LeftSlots.Length; i++)
                BuildRow(rect, LeftSlots[i], Pad, slotsTop - i * SlotStep);

            for (int i = 0; i < RightSlots.Length; i++)
                BuildRow(rect, RightSlots[i], rightX, slotsTop - i * SlotStep);

            // Оружие по центру под моделью: в образце это отдельная полоса,
            // отбитая от колонок, и читается она как «что в руках».
            float bottomY = -WeaponsTop;
            float bottomWidth = BottomSlots.Length * SlotStep - (SlotStep - Slot);
            float bottomX = (Width - bottomWidth) * 0.5f;

            for (int i = 0; i < BottomSlots.Length; i++)
                BuildRow(rect, BottomSlots[i], bottomX + i * SlotStep, bottomY);
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

        private void BuildRow(RectTransform parent, EquipSlot slot, float x, float y)
        {
            // Квадратик предмета
            var iconGo = new GameObject(slot + "Icon", typeof(Image), typeof(Button));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            // Сторона гнезда из образца: 40 при ширине окна 484. Прежние
            // 26 делали иконку вдвое мельче — Павлон 01.09.2026: «обрати
            // внимание на их размер в референсе, у тебя намного меньше».
            //
            // Гнездо боеприпасов в образце заметно меньше соседей: оно не
            // равноправная рука, а приложение к дальнему бою. Центрируем
            // его по высоте полосы, иначе оно повисло бы на её верхнем крае.
            float side = slot == EquipSlot.Ammo ? Slot * 0.82f : Slot;
            float sink = (Slot - side) * 0.5f;

            iconRect.anchoredPosition = new Vector2(x + sink, y - sink);
            iconRect.sizeDelta = new Vector2(side, side);
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

            // Подписей в образце нет: гнёзда опознаются по рисунку, а
            // название вещи показывает подсказка при наведении. Объект
            // оставляем — на него завязано обновление слота, — но не
            // рисуем, иначе колонка вещей снова расползётся на треть окна.
            label.enabled = false;
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

            // Подзаголовок: в образце под именем стоит «Человек, разбойник
            // 1-го уровня». Расы и класса у нас нет — пишем то, что есть.
            if (subtitle != null)
            {
                int level = experience != null ? experience.Level : 1;
                LocalizedText.Bind(subtitle, "Искатель приключений, " + level + "-й уровень");
            }
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
            float left = SlotColumnWidth;
            float top = ContentTop;
            float boxHeight = ModelHeight;

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
            // Держим пропорцию съёмки: витрина снимает героя в текстуру
            // 200 x 340, а область под неё шире и ниже. Растянутая на всю
            // область картинка сплющивала героя по высоте — Павлон
            // 01.09.2026: «модель сплюснута, поставь её в нормальный рост».
            //
            // Поэтому не растягиваем, а вписываем по центру: высота
            // области целиком, ширина — сколько требует пропорция.
            const float shotWidth = 200f, shotHeight = 340f;
            float viewHeight = ModelHeight - 6f;
            float viewWidth = viewHeight * (shotWidth / shotHeight);

            viewRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.anchoredPosition = Vector2.zero;
            viewRect.sizeDelta = new Vector2(viewWidth, viewHeight);
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
            // Две колонки рядом, без прокрутки — как в образце: слева свойства
            // героя, справа ближний бой. Прокрутка здесь была лишней (числа
            // помещаются), а её полоса торчала светлой чертой у края.
            float gap = 14f;
            float columnWidth = (ModelColumnWidth - gap) * 0.5f;

            var leftColumn = StatColumn(parent, "StatsLeft", SlotColumnWidth, columnWidth);
            var rightColumn = StatColumn(parent, "StatsRight", SlotColumnWidth + columnWidth + gap, columnWidth);

            var parentBackup = parent;

            parent = leftColumn;
            float y = 0f;

            y = StatSection(parent, "Общее", y);
            y = StatLine(parent, "level", "Уровень", y);
            y = StatLine(parent, "health", "Здоровье", y);
            y = StatLine(parent, "energy", "Энергия", y);
            y = StatLine(parent, "armor", "Броня", y);

            y = StatSection(parent, "Характеристики", y);
            y = StatLine(parent, "strength", "Сила", y);
            y = StatLine(parent, "agility", "Ловкость", y);
            y = StatLine(parent, "stamina", "Выносливость", y);

            parent = rightColumn;
            y = 0f;

            y = StatSection(parent, "Ближний бой", y);

            // Имя оружия — на всю ширину колонки: «Кинжал бандита» рядом с
            // подписью «Оружие» не помещается и вылезает за край.
            y = StatWideLine(parent, "weapon", y);
            y = StatLine(parent, "damage", "Урон", y);
            y = StatLine(parent, "speed", "Скорость", y);
            y = StatLine(parent, "dps", "Урон в секунду", y);
            y = StatLine(parent, "crit", "Шанс крита", y);
            y = StatLine(parent, "critmult", "Сила крита", y);
            y = StatLine(parent, "miss", "Шанс промаха", y);

            parent = parentBackup;
        }

        /// <summary>Колонка чисел: пустой прямоугольник, в который ложатся строки.</summary>
        private RectTransform StatColumn(RectTransform parent, string name, float x, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -StatsTop);
            rect.sizeDelta = new Vector2(width, StatsHeight);
            return rect;
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
            rect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f, StatHeader);
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
            rect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f, StatRow);
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
            labelRect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f, StatRow);
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
            valueRect.sizeDelta = new Vector2(StatColumnWidth - StatInset * 2f, StatRow);
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
