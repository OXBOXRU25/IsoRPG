using System.Collections.Generic;
using UnityEngine;
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
    public sealed class InventoryHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        private static readonly Color SlotEmpty = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color BagColor = new Color32(0x7A, 0x5C, 0x38, 0xFF);

        private const float Margin = 18f;
        private const float BagSize = 46f;
        private const int Columns = 5;
        private const float CellSize = 46f;
        private const float CellGap = 4f;
        private const float WindowPad = 12f;
        private const float TitleHeight = 22f;
        private const float FooterHeight = 22f;

        [SerializeField] private Inventory inventory;
        [SerializeField] private Equipment equipment;

        private Font font;
        private GameObject window;
        private Text goldText;
        private Text titleText;

        private readonly List<Image> cellIcons = new List<Image>();
        private readonly List<Text> cellCounts = new List<Text>();

        private void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<Inventory>();
            if (equipment == null) equipment = GetComponentInParent<Equipment>();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
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
            if (keyboard.escapeKey.wasPressedThisFrame && window != null && window.activeSelf)
                window.SetActive(false);
        }

        public void Toggle()
        {
            if (window == null) return;

            window.SetActive(!window.activeSelf);
            if (window.activeSelf) Refresh();
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
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            BuildBagButton(root);
            BuildWindow(root);
        }

        private void BuildBagButton(RectTransform root)
        {
            var go = new GameObject("BagButton", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);

            // Приподнят над полоской опыта, чтобы не наезжать на неё.
            rect.anchoredPosition = new Vector2(-Margin, Margin + 10f);
            rect.sizeDelta = new Vector2(BagSize, BagSize);

            var image = go.GetComponent<Image>();
            image.color = BagColor;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(Toggle);

            var label = CreateText(rect, "Label", "СУМКА", 9, TextColor);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch((RectTransform)label.transform);

            var hint = CreateText(rect, "Hint", "I", 11, new Color32(0xD8, 0xC8, 0xA8, 0xFF));
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(0f, 1f);
            hintRect.pivot = new Vector2(0f, 1f);
            hintRect.anchoredPosition = new Vector2(3f, -2f);
            hintRect.sizeDelta = new Vector2(12f, 12f);
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

            goldText = CreateText(rect, "Gold", "0 золота", 12, GoldColor);
            var goldRect = (RectTransform)goldText.transform;
            goldRect.anchorMin = new Vector2(0f, 0f);
            goldRect.anchorMax = new Vector2(1f, 0f);
            goldRect.pivot = new Vector2(0.5f, 0f);
            goldRect.anchoredPosition = new Vector2(0f, WindowPad * 0.5f);
            goldRect.sizeDelta = new Vector2(-WindowPad * 2f, FooterHeight);
            goldText.alignment = TextAnchor.MiddleCenter;

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

            var count = CreateText(rect, "Count", "", 11, TextColor);
            var countRect = (RectTransform)count.transform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-3f, 2f);
            countRect.sizeDelta = new Vector2(24f, 14f);
            count.alignment = TextAnchor.LowerRight;
            cellCounts.Add(count);

            int captured = index;
            go.GetComponent<Button>().onClick.AddListener(() => OnCellClicked(captured));
        }

        /// <summary>Клик по ячейке — надеть, если предмет надевается.</summary>
        private void OnCellClicked(int index)
        {
            if (inventory == null || equipment == null) return;

            var stack = inventory.GetSlot(index);
            if (stack.IsEmpty || !stack.Item.IsEquippable) return;

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
                    continue;
                }

                // Цвет ячейки по редкости: игрок видит ценность, не читая
                // названий. Это и есть смысл цветовой шкалы.
                cellIcons[i].color = stack.Item.RarityColor;
                cellCounts[i].text = stack.Count > 1 ? stack.Count.ToString() : "";
            }

            if (goldText != null) goldText.text = inventory.Gold + " золота";

            if (titleText != null)
                titleText.text = "Сумка  " + inventory.UsedSlots + " / " + inventory.Capacity;
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
    }
}
