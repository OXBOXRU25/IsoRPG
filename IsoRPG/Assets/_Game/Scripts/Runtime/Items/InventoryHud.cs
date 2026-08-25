using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IsoRPG.Items
{
    /// <summary>
    /// Сумка: значок в правом нижнем углу и окно с ячейками.
    ///
    /// Строится кодом, как и боевой интерфейс — на этом этапе правки идут
    /// каждый заход, и менять числа в одном файле быстрее, чем перекладывать
    /// объекты в дереве сцены.
    /// </summary>
    public sealed class InventoryHud : MonoBehaviour, IsoRPG.UI.IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        /// <summary>Тёмная подложка внутри цветной рамки редкости.</summary>
        private static readonly Color CellBackdrop = new Color32(0x22, 0x1F, 0x1A, 0xFF);

        private static readonly Color SlotEmpty = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        private const float Margin = 18f;
        private const float BagSize = 46f;
        /// <summary>
        /// Восемь колонок на сорок ячеек — это пять рядов.
        ///
        /// При прежних пяти колонках получилось бы восемь рядов, и окно
        /// вытянулось бы в узкий столбец высотой почти во весь экран.
        /// Широкая и низкая сетка ещё и просматривается быстрее: глаз
        /// бежит вдоль строки, а не прыгает по столбцу.
        /// </summary>
        private const int Columns = 8;
        private const float CellSize = 46f;
        private const float CellGap = 4f;
        private const float WindowPad = 12f;
        private const float TitleHeight = 22f;
        private const float FooterHeight = 22f;

        [SerializeField] private Inventory inventory;
        [SerializeField] private Equipment equipment;
        [SerializeField] private IsoRPG.Combat.Experience experience;
        [SerializeField] private FoodConsumer food;

        private Font font;
        private GameObject window;
        private Text goldText;
        private Text titleText;

        private readonly List<Image> cellIcons = new List<Image>();
        private readonly List<Text> cellCounts = new List<Text>();

        /// <summary>Рисунки предметов поверх цветных плашек редкости.</summary>
        private readonly List<Image> cellArt = new List<Image>();

        /// <summary>Подсказка о предмете в ячейке.</summary>
        private readonly List<IsoRPG.UI.ItemTooltipTrigger> cellTips =
            new List<IsoRPG.UI.ItemTooltipTrigger>();

        private void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<Inventory>();
            if (equipment == null) equipment = GetComponentInParent<Equipment>();
            if (experience == null) experience = GetComponentInParent<IsoRPG.Combat.Experience>();
            if (food == null) food = GetComponentInParent<FoodConsumer>();

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
            if (inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
            if (inventory != null) inventory.Changed -= Refresh;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // I — как в любой игре жанра. Плюс Esc закрывает, если открыто.
            if (keyboard.iKey.wasPressedThisFrame)
            {
                Toggle();

                // Звук по факту состояния, а не по нажатию: если окно не
                // открылось, звука быть не должно.
                if (window != null && window.activeSelf) IsoRPG.Audio.Sfx.OpenWindow();
                else IsoRPG.Audio.Sfx.CloseWindow();
            }
            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.
        }

        public bool IsOpen => window != null && window.activeSelf;

        public void Toggle()
        {
            if (window == null) return;

            window.SetActive(!window.activeSelf);
            if (window.activeSelf) Refresh();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("InventoryHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Поверх боевого интерфейса: окно должно перекрывать полоски,
            // а не прятаться под ними.
            canvas.sortingOrder = 10;

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

            // Кнопка сумки переехала в общий ряд кнопок (HudBar): четыре окна
            // должны открываться одинаково, а не каждое своим способом.
            BuildWindow(root);
        }

        private void BuildWindow(RectTransform root)
        {
            int capacity = inventory != null ? inventory.Capacity : 20;
            int rows = Mathf.CeilToInt(capacity / (float)Columns);

            float width = Columns * CellSize + (Columns - 1) * CellGap + WindowPad * 2f;
            float height = rows * CellSize + (rows - 1) * CellGap
                           + WindowPad * 2f + TitleHeight + FooterHeight;

            var go = new GameObject("InventoryWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            // Справа снизу, над иконкой сумки — как в играх жанра. Лог боя
            // при этом живёт слева и за место не спорит.
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-Margin, Margin + BagSize + 20f);
            rect.sizeDelta = new Vector2(width, height);

            var panel = go.GetComponent<Image>();
            panel.color = PanelColor;

            var edge = new GameObject("Edge", typeof(Image));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(rect, false);
            Stretch(edgeRect);
            edgeRect.offsetMin = new Vector2(-1f, -1f);
            edgeRect.offsetMax = new Vector2(1f, 1f);
            edge.transform.SetAsFirstSibling();
            edge.GetComponent<Image>().color = PanelEdge;

            titleText = CreateText(rect, "Title", "Сумка", 14, TextColor);
            var titleRect = (RectTransform)titleText.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -WindowPad * 0.5f);
            titleRect.sizeDelta = new Vector2(-WindowPad * 2f, TitleHeight);
            titleText.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < capacity; i++)
            {
                int column = i % Columns;
                int row = i / Columns;

                float x = WindowPad + column * (CellSize + CellGap);
                float y = -(WindowPad + TitleHeight + row * (CellSize + CellGap));

                BuildCell(rect, i, new Vector2(x, y));
            }

            goldText = CreateText(rect, "Gold", Loc.F("{0} золота", 0), 12, GoldColor);
            var goldRect = (RectTransform)goldText.transform;
            goldRect.anchorMin = new Vector2(0f, 0f);
            goldRect.anchorMax = new Vector2(1f, 0f);
            goldRect.pivot = new Vector2(0.5f, 0f);
            goldRect.anchoredPosition = new Vector2(0f, WindowPad * 0.5f);
            goldRect.sizeDelta = new Vector2(-WindowPad * 2f, FooterHeight);
            goldText.alignment = TextAnchor.MiddleCenter;

            IsoRPG.UI.WindowChrome.AddCloseButton(rect, font, Close);

            window = go;
            window.SetActive(false);
        }

        private void BuildCell(RectTransform parent, int index, Vector2 position)
        {
            var go = new GameObject("Cell" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(CellSize, CellSize);

            var icon = go.GetComponent<Image>();
            icon.color = SlotEmpty;
            cellIcons.Add(icon);

            // Тёмная подложка поверх цветной ячейки, с отступом. Цвет
            // редкости остаётся видимым только по краю — получается рамка,
            // а не заливка.
            //
            // Заливка не годится: рисунок предмета обведён тёмным контуром,
            // и на ярком фоне этот контур читается как грязная кайма. На
            // тёмной подложке он становится тем, чем задуман, — границей
            // предмета.
            var backdrop = new GameObject("Backdrop", typeof(Image));
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.SetParent((RectTransform)go.transform, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            // Рамка тонкая: у рисунка предмета есть собственная тёмная
            // обводка, и толстая цветная рамка рядом с ней читается как
            // вторая обводка — глаз видит две границы вместо одной.
            backdropRect.offsetMin = new Vector2(1.5f, 1.5f);
            backdropRect.offsetMax = new Vector2(-1.5f, -1.5f);

            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = CellBackdrop;
            backdropImage.raycastTarget = false;

            var art = new GameObject("Art", typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent((RectTransform)go.transform, false);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(2f, 2f);
            artRect.offsetMax = new Vector2(-2f, -2f);

            var artImage = art.GetComponent<Image>();
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;
            artImage.enabled = false;

            cellArt.Add(artImage);

            var count = CreateText(rect, "Count", "", 11, TextColor);
            var countRect = (RectTransform)count.transform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-3f, 2f);
            countRect.sizeDelta = new Vector2(24f, 14f);
            count.alignment = TextAnchor.LowerRight;
            cellCounts.Add(count);

            cellTips.Add(go.AddComponent<IsoRPG.UI.ItemTooltipTrigger>());

            // Перетаскивание за пределы окна выбрасывает вещь на землю.
            // Окно передаём сюда же: именно по его границе и решается,
            // выбросили вещь или просто повозили курсором внутри сумки.
            var drag = go.AddComponent<SlotDragSource>();
            drag.Setup(index, inventory, parent);

            int captured = index;
            go.GetComponent<Button>().onClick.AddListener(() => OnCellClicked(captured));
        }

        /// <summary>
        /// Клик по ячейке: надеть вещь или съесть еду.
        ///
        /// Одно действие на предмет, без меню по правой кнопке: у вещи
        /// оно очевидно, у еды тоже, а выбор из двух пунктов там, где
        /// пункт всегда один, — лишний шаг на каждое нажатие.
        /// </summary>
        private void OnCellClicked(int index)
        {
            if (inventory == null) return;

            var stack = inventory.GetSlot(index);
            if (stack.IsEmpty) return;

            if (stack.Item.IsFood)
            {
                if (food == null) return;

                // Тратим только если еда пошла: отказ «здоровье полное»
                // не должен съедать яблоко.
                if (food.Begin(stack.Item)) inventory.TakeFrom(index, 1);
                return;
            }

            if (equipment == null || !stack.Item.IsEquippable) return;

            equipment.EquipFromInventory(index);
        }

        private void Refresh()
        {
            if (inventory == null) return;

            for (int i = 0; i < cellIcons.Count; i++)
            {
                var stack = inventory.GetSlot(i);

                if (stack.IsEmpty)
                {
                    cellIcons[i].color = SlotEmpty;
                    cellCounts[i].text = "";

                    if (cellArt != null && i < cellArt.Count) cellArt[i].enabled = false;
                    if (i < cellTips.Count) cellTips[i].Setup(null, experience);
                    continue;
                }

                // Цвет ячейки по редкости: игрок видит ценность, не читая
                // названий. Это и есть смысл цветовой шкалы. Рисунок предмета
                // ложится поверх — так одно говорит ЧТО это, другое СКОЛЬКО
                // оно стоит, и они не мешают друг другу.
                cellIcons[i].color = stack.Item.RarityColor;
                LocalizedText.Bind(cellCounts[i], stack.Count > 1 ? stack.Count.ToString() : "");

                if (cellArt != null && i < cellArt.Count)
                {
                    cellArt[i].sprite = stack.Item.icon;
                    cellArt[i].enabled = stack.Item.icon != null;
                }

                if (i < cellTips.Count) cellTips[i].Setup(stack.Item, experience);
            }

            if (goldText != null) goldText.text = Loc.F("{0} золота", inventory.Gold);

            if (titleText != null)
                titleText.text = Loc.F("Сумка  {0} / {1}", inventory.UsedSlots, inventory.Capacity);
        }

        // ------------------------------------------------------------------

        private Text CreateText(RectTransform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(60f, 16f);

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
    }
}
