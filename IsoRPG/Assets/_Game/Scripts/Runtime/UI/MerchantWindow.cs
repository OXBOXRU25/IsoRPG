using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.UI
{
    /// <summary>
    /// Окно торговли: слева товар, справа своя сумка.
    ///
    /// Обе стороны сразу, а не вкладками «купить» и «продать». Игрок приходит
    /// к торговцу с одним вопросом — «что я могу себе позволить, если сдам
    /// вот это», — и вкладки заставляют держать ответ в голове, переключаясь
    /// туда-сюда.
    ///
    /// Цена написана на каждой строке. Числа, которые надо узнавать
    /// наведением, в лавке не работают: решение принимается сравнением, а
    /// сравнивать можно только видимое.
    /// </summary>
    public sealed class MerchantWindow : MonoBehaviour, IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF4);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        private static readonly Color TitleColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x84, 0x76, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color RowColor = new Color32(0x24, 0x21, 0x1B, 0xFF);
        private static readonly Color RowHover = new Color32(0x3A, 0x34, 0x28, 0xFF);
        private static readonly Color CantAfford = new Color32(0x7A, 0x50, 0x4A, 0xFF);

        private const float Width = 620f;
        private const float Height = 460f;
        private const float ColumnWidth = 290f;
        private const float RowHeight = 34f;
        private const float Pad = 14f;
        private const float TitleHeight = 26f;

        private Merchant current;
        private Inventory inventory;
        private Experience experience;

        private Font font;
        private GameObject window;
        private Text goldText;
        private RectTransform stockList;
        private RectTransform bagList;

        private readonly List<GameObject> spawned = new List<GameObject>();

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            experience = GetComponent<Experience>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += RefreshIfOpen;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= RefreshIfOpen;
        }

        private void Update()
        {
            if (!IsOpen || current == null) return;

            // Отошёл — лавка закрылась. Торговать через полкарты нельзя, и
            // окно, висящее после ухода, читается как забытое.
            if (Vector3.Distance(transform.position, current.transform.position) >
                current.TalkRange + 1.5f)
            {
                Close();
            }
        }

        public void Open(Merchant merchant)
        {
            if (window == null || merchant == null) return;

            current = merchant;

            Refresh();
            window.SetActive(true);
            IsoRPG.Audio.Sfx.OpenWindow();
        }

        public void Close()
        {
            if (window == null || !window.activeSelf) return;

            current = null;
            window.SetActive(false);
            IsoRPG.Audio.Sfx.CloseWindow();
        }

        // ------------------------------------------------------------------

        private void RefreshIfOpen()
        {
            if (IsOpen) Refresh();
        }

        private void Refresh()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (current == null || inventory == null) return;

            goldText.text = "У тебя: " + inventory.Gold + " золота";

            // Товар торговца
            int row = 0;
            foreach (var item in current.Stock)
            {
                if (item == null) continue;

                int price = Merchant.PriceToBuy(item);
                bool affordable = inventory.Gold >= price;

                var captured = item;

                AddRow(stockList, row++, item, price, affordable,
                       () => current.Sell(captured, inventory));
            }

            if (row == 0) AddEmpty(stockList, "Лавка пуста");

            // Своя сумка
            row = 0;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack.IsEmpty || stack.Item == null) continue;

                int price = Merchant.PriceToSell(stack.Item);
                int slot = i;

                AddRow(bagList, row++, stack.Item, price, true,
                       () => current.Buy(slot, inventory), stack.Count);
            }

            if (row == 0) AddEmpty(bagList, "Сумка пуста");
        }

        private void AddRow(RectTransform parent, int index, ItemDefinition item, int price,
                            bool enabled, System.Action onClick, int count = 1)
        {
            var go = new GameObject("Row" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rect.sizeDelta = new Vector2(0f, RowHeight - 3f);

            var plate = go.GetComponent<Image>();
            plate.color = enabled ? RowColor : CantAfford * 0.5f;

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            button.interactable = enabled;
            button.onClick.AddListener(() => onClick());

            var colors = button.colors;
            colors.highlightedColor = RowHover;
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            // Рисунок предмета: в лавке узнают вещь по картинке быстрее, чем
            // по названию, — тот же значок, что и в сумке.
            if (item.icon != null)
            {
                var art = new GameObject("Art", typeof(Image));
                var artRect = (RectTransform)art.transform;
                artRect.SetParent(rect, false);
                artRect.anchorMin = new Vector2(0f, 0.5f);
                artRect.anchorMax = new Vector2(0f, 0.5f);
                artRect.pivot = new Vector2(0f, 0.5f);
                artRect.anchoredPosition = new Vector2(4f, 0f);
                artRect.sizeDelta = new Vector2(26f, 26f);

                var artImage = art.GetComponent<Image>();
                artImage.sprite = item.icon;
                artImage.preserveAspect = true;
                artImage.raycastTarget = false;
            }

            string label = item.displayName;
            if (count > 1) label += "  x" + count;

            var name = MakeText(rect, "Name", label, 12, item.RarityColor);
            var nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(34f, 0f);
            nameRect.offsetMax = new Vector2(-62f, 0f);
            name.alignment = TextAnchor.MiddleLeft;

            var cost = MakeText(rect, "Price", price.ToString(), 12,
                                enabled ? GoldColor : DimColor);
            var costRect = (RectTransform)cost.transform;
            costRect.anchorMin = new Vector2(1f, 0f);
            costRect.anchorMax = new Vector2(1f, 1f);
            costRect.pivot = new Vector2(1f, 0.5f);
            costRect.anchoredPosition = new Vector2(-8f, 0f);
            costRect.sizeDelta = new Vector2(54f, 0f);
            cost.alignment = TextAnchor.MiddleRight;

            // Подсказка о вещи — та же, что в сумке: игрок сравнивает покупку
            // с надетым, и данные для сравнения должны выглядеть одинаково.
            var tip = go.AddComponent<ItemTooltipTrigger>();
            tip.Setup(item, experience);

            spawned.Add(go);
        }

        private void AddEmpty(RectTransform parent, string caption)
        {
            var text = MakeText(parent, "Empty", caption, 12, DimColor);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.sizeDelta = new Vector2(0f, 20f);
            text.alignment = TextAnchor.MiddleCenter;

            spawned.Add(text.gameObject);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("MerchantHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 14;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("MerchantWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Width, Height);

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

            var header = MakeText(rect, "Header", "Лавка", 15, TitleColor);
            Place(header, new Vector2(0f, -10f), new Vector2(Width - 28f, TitleHeight));

            goldText = MakeText(rect, "Gold", "", 12, GoldColor);
            Place(goldText, new Vector2(0f, -34f), new Vector2(Width - 28f, 18f));

            stockList = MakeColumn(rect, "Stock", Pad, "Товар");
            bagList = MakeColumn(rect, "Bag", Width - ColumnWidth - Pad, "Твоя сумка");

            WindowChrome.AddCloseButton(rect, font, Close);

            window = go;
            window.SetActive(false);
        }

        private RectTransform MakeColumn(RectTransform parent, string name, float x, string caption)
        {
            var title = MakeText(parent, name + "Title", caption, 13, DimColor);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(x, -58f);
            titleRect.sizeDelta = new Vector2(ColumnWidth, 18f);
            title.alignment = TextAnchor.MiddleLeft;

            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -80f);
            rect.sizeDelta = new Vector2(ColumnWidth, Height - 96f);

            return rect;
        }

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private Text MakeText(RectTransform parent, string name, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
