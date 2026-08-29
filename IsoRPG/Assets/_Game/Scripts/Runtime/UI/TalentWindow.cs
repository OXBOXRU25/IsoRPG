using System.Collections.Generic;
using IsoRPG.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Progression;

namespace IsoRPG.UI
{
    /// <summary>
    /// Окно талантов: три ветки рядом, очко за уровень.
    ///
    /// Три колонки в один экран, а не вкладки по ветке. Вкладки прячут ровно
    /// то, ради чего окно открывают: выбор делается между ветками, и увидеть
    /// их нужно одновременно. У Classic то же решение и по той же причине.
    ///
    /// Недоступный талант не прячется, а гаснет. Закрытая дверь, которую
    /// видно, — это цель; закрытая дверь, которой не видно, — это ничего.
    /// </summary>
    public sealed class TalentWindow : MonoBehaviour, IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF4);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        private static readonly Color TitleColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x84, 0x76, 0xFF);
        private static readonly Color PointsColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color CellEmpty = new Color32(0x24, 0x21, 0x1B, 0xFF);
        private static readonly Color RankFull = new Color32(0x8A, 0xC8, 0x7A, 0xFF);
        private static readonly Color RankSome = new Color32(0xE8, 0xE2, 0xD4, 0xFF);

        /// <summary>Затемнение недоступного: видно, но ясно, что пока нельзя.</summary>
        private static readonly Color Locked = new Color(0.42f, 0.42f, 0.42f, 1f);

        private const float Width = 560f;
        private const float Height = 452f;
        private const float ColumnWidth = 168f;
        private const float CellSize = 52f;
        private const float CellGap = 14f;
        private const float RowGap = 64f;
        private const float TreeTop = 92f;

        private TalentBook book;
        private Font font;
        private GameObject window;
        private Text pointsText;

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Text> branchTotals = new List<Text>();

        private sealed class Entry
        {
            public TalentDefinition talent;
            public Image plate;
            public Image art;
            public Text rank;
            public TalentHoverTrigger hover;
        }

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            book = GetComponent<TalentBook>();
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
            Loc.Changed += RefreshIfOpen;
            if (book != null) book.Changed += RefreshIfOpen;
        }

        private void OnDisable()
        {
            Loc.Changed -= RefreshIfOpen;
            if (book != null) book.Changed -= RefreshIfOpen;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || window == null) return;

            // N — как в Classic. Буква свободна, а привычка у играющих есть.
            if (keyboard.nKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle()
        {
            if (window == null) return;

            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (window == null) return;

            Refresh();
            window.SetActive(true);
            IsoRPG.Audio.Sfx.OpenWindow();
        }

        public void Close()
        {
            if (window == null || !window.activeSelf) return;

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
            if (book == null) return;

            if (pointsText != null)
            {
                int free = book.AvailablePoints;

                pointsText.text = free > 0
                    ? Loc.F("Свободных очков: {0}", free)
                    : "Очков нет — они приходят с уровнем";

                pointsText.color = free > 0 ? PointsColor : DimColor;
            }

            for (int i = 0; i < branchTotals.Count; i++)
                LocalizedText.Bind(branchTotals[i], book.SpentIn((TalentBranch)i).ToString());

            foreach (var entry in entries)
            {
                int rank = book.RankOf(entry.talent);
                bool can = book.CanLearn(entry.talent, out string reason);
                bool maxed = rank >= entry.talent.maxRank;

                // Открытым считается ярус, а не возможность потратить очко:
                // талант, на который просто не хватает очков, гасить нельзя —
                // иначе окно выглядит поломанным, пока копишь.
                bool tierOpen = book.SpentIn(entry.talent.branch) >= entry.talent.RequiredInBranch;

                Color tint = tierOpen ? Color.white : Locked;

                entry.plate.color = tierOpen
                    ? TalentDefinition.BranchColor(entry.talent.branch)
                    : Locked * 0.5f;

                if (entry.art != null) entry.art.color = tint;

                LocalizedText.Bind(entry.rank, rank + " / " + entry.talent.maxRank);
                entry.rank.color = maxed ? RankFull : (rank > 0 ? RankSome : DimColor);

                entry.hover.Setup(entry.talent, rank, can || maxed ? "" : reason);
            }
        }

        private void OnTalentClicked(TalentDefinition talent)
        {
            if (book == null) return;

            if (book.Learn(talent))
            {
                IsoRPG.Audio.Sfx.LevelUp();
                Refresh();
            }
            else IsoRPG.Audio.Sfx.CloseWindow();
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("TalentHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 13;

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

            var go = new GameObject("TalentWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Width, Height);

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

            var header = MakeText(rect, "Header", "Таланты", 15, TitleColor);
            Place(header, new Vector2(0f, -12f), new Vector2(Width - 24f, 22f), TextAnchor.MiddleCenter);

            pointsText = MakeText(rect, "Points", "", 12, PointsColor);
            Place(pointsText, new Vector2(0f, -34f), new Vector2(Width - 24f, 18f), TextAnchor.MiddleCenter);

            BuildBranches(rect);

            var hint = MakeText(rect, "Hint", "N — открыть и закрыть", 11, DimColor);
            Place(hint, new Vector2(0f, -(Height - 22f)), new Vector2(Width - 24f, 16f), TextAnchor.MiddleCenter);

            BuildResetButton(rect);

            IsoRPG.UI.WindowChrome.AddCloseButton(rect, font, Close);

            window = go;
            window.SetActive(false);
        }

        private void BuildBranches(RectTransform parent)
        {
            float left = (Width - ColumnWidth * 3f) * 0.5f;

            for (int b = 0; b < 3; b++)
            {
                var branch = (TalentBranch)b;
                float columnX = left + b * ColumnWidth;

                var title = MakeText(parent, branch + "Title",
                    TalentDefinition.BranchName(branch), 13, TalentDefinition.BranchColor(branch));

                PlaceLeft(title, new Vector2(columnX, -62f), new Vector2(ColumnWidth, 18f),
                          TextAnchor.MiddleCenter);

                var total = MakeText(parent, branch + "Total", "0", 12, DimColor);
                PlaceLeft(total, new Vector2(columnX, -78f), new Vector2(ColumnWidth, 14f),
                          TextAnchor.MiddleCenter);

                branchTotals.Add(total);

                // Разделители между ветками: три колонки без границ читаются
                // как одна таблица, а это три разных пути.
                if (b > 0) BuildDivider(parent, columnX - 1f);
            }

            if (book == null) return;

            foreach (var talent in book.All)
            {
                if (talent == null) continue;

                float columnX = left + (int)talent.branch * ColumnWidth;

                float x = columnX + (ColumnWidth - CellSize * 2f - CellGap) * 0.5f
                          + talent.column * (CellSize + CellGap);

                float y = -(TreeTop + talent.row * RowGap);

                BuildCell(parent, talent, new Vector2(x, y));
            }
        }

        private void BuildDivider(RectTransform parent, float x)
        {
            var go = new GameObject("Divider", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -56f);
            rect.sizeDelta = new Vector2(1f, Height - 86f);

            var image = go.GetComponent<Image>();
            image.color = new Color32(0x33, 0x30, 0x28, 0xFF);
            image.raycastTarget = false;
        }

        private void BuildCell(RectTransform parent, TalentDefinition talent, Vector2 position)
        {
            var go = new GameObject(talent.name, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(CellSize, CellSize);

            var plate = go.GetComponent<Image>();
            plate.color = TalentDefinition.BranchColor(talent.branch);

            // Тёмная подложка внутри цветной рамки — тот же приём, что в
            // сумке: цвет остаётся каймой, а рисунок ложится на тёмное.
            var backdrop = new GameObject("Backdrop", typeof(Image));
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.SetParent(rect, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = new Vector2(1.5f, 1.5f);
            backdropRect.offsetMax = new Vector2(-1.5f, -1.5f);

            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = CellEmpty;
            backdropImage.raycastTarget = false;

            var art = new GameObject("Art", typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(rect, false);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(2f, 2f);
            artRect.offsetMax = new Vector2(-2f, -2f);

            var artImage = art.GetComponent<Image>();
            artImage.sprite = talent.icon;
            artImage.enabled = talent.icon != null;
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;

            var rankText = MakeText(rect, "Rank", "0 / " + talent.maxRank, 10, DimColor);
            var rankRect = (RectTransform)rankText.transform;
            rankRect.anchorMin = new Vector2(0.5f, 0f);
            rankRect.anchorMax = new Vector2(0.5f, 0f);
            rankRect.pivot = new Vector2(0.5f, 1f);
            rankRect.anchoredPosition = new Vector2(0f, -1f);
            rankRect.sizeDelta = new Vector2(CellSize + 8f, 12f);
            rankText.alignment = TextAnchor.UpperCenter;

            var captured = talent;
            go.GetComponent<Button>().onClick.AddListener(() => OnTalentClicked(captured));

            var hover = go.AddComponent<TalentHoverTrigger>();

            entries.Add(new Entry
            {
                talent = talent,
                plate = plate,
                art = artImage,
                rank = rankText,
                hover = hover,
            });
        }

        private void BuildResetButton(RectTransform parent)
        {
            var go = new GameObject("Reset", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-14f, 12f);
            rect.sizeDelta = new Vector2(96f, 26f);

            go.GetComponent<Image>().color = CellEmpty;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                if (book == null) return;

                book.ResetAll();
                Refresh();
            });

            var label = MakeText(rect, "Label", "Сбросить", 11, DimColor);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
        }

        // ------------------------------------------------------------------

        private Text MakeText(RectTransform parent, string name, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            LocalizedText.Bind(text, value);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static void Place(Text text, Vector2 position, Vector2 size, TextAnchor anchor)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = anchor;
        }

        private static void PlaceLeft(Text text, Vector2 position, Vector2 size, TextAnchor anchor)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = anchor;
        }
    }

    /// <summary>Подсказка о таланте: что даёт сейчас и что даст следующее очко.</summary>
    public sealed class TalentHoverTrigger : TooltipTriggerBase
    {
        private TalentDefinition talent;
        private int rank;
        private string blockReason;

        public void Setup(TalentDefinition definition, int currentRank, string reason)
        {
            talent = definition;
            rank = currentRank;
            blockReason = reason;
        }

        protected override bool Display(Tooltip tooltip, Vector2 at)
        {
            if (talent == null) return false;

            tooltip.ShowTalent(talent, rank, blockReason, at);
            return true;
        }
    }
}
