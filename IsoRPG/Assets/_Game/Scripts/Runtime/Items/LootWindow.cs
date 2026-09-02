using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Окно добычи: что лежит в мешке и что из этого брать.
    ///
    /// Смысл окна не в удобстве, а в выборе. Автоматический подбор экономит
    /// клик, но отнимает решение: игрок не смотрит на добычу и не оценивает
    /// её — вещи просто накапливаются в сумке. Окно возвращает момент, ради
    /// которого добыча вообще нужна.
    ///
    /// Закрывается само, когда игрок отходит: мешок остаётся лежать, к нему
    /// можно вернуться, а окно, висящее через полкарты, — мусор на экране.
    /// </summary>
    public sealed class LootWindow : MonoBehaviour, IsoRPG.UI.IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xD2);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0x8A);
        private static readonly Color RowColor = new Color32(0x2A, 0x27, 0x21, 0xC0);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x3A, 0x32, 0x24, 0xE0);
        private static readonly Color ButtonHover = new Color32(0x54, 0x46, 0x30, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        private const float Width = 230f;
        private const float RowHeight = 30f;
        private const float Pad = 10f;
        private const float TitleHeight = 24f;
        private const float ButtonHeight = 26f;

        [Tooltip("Дальше этого расстояния окно закрывается само.")]
        [SerializeField] private float maxDistance = 4.5f;

        private Inventory inventory;
        private Font font;

        private GameObject window;
        private RectTransform rows;

        /// <summary>Кнопка «Собрать всё». Живёт всё время, ездит по высоте.</summary>
        private RectTransform takeAllButton;
        private LootDrop current;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private void Awake()
        {
            inventory = GetComponentInParent<Inventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void Update()
        {
            if (current == null)
            {
                if (window != null && window.activeSelf) Close();
                return;
            }

            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.

            // Отошёл — закрываем. Мешок остаётся лежать.
            if (Vector3.Distance(transform.position, current.transform.position) > maxDistance)
                Close();
        }

        /// <summary>Открыть мешок. Вызывается по клику.</summary>
        public void Open(LootDrop drop)
        {
            if (drop == null) return;

            if (current != null)
            {
                current.Changed -= Refresh;
                current.Emptied -= Close;
            }

            current = drop;
            current.Changed += Refresh;
            current.Emptied += Close;

            window.SetActive(true);
            Refresh();

            IsoRPG.Audio.Sfx.OpenWindow();
        }

        public bool IsOpen => window != null && window.activeSelf;

        public void Close()
        {
            if (current != null)
            {
                current.Changed -= Refresh;
                current.Emptied -= Close;
                current = null;
            }

            if (window != null) window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Refresh()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (current == null) return;

            int index = 0;

            if (current.Gold > 0)
            {
                AddRow(index++, Loc.F("{0} золота", current.Gold), GoldColor, null, () =>
                {
                    int taken = current.TakeGold(inventory);
                    if (taken <= 0) return;

                    CombatLog.GainedGold(taken);
                    IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.gold,
                                          transform.position, 0.45f, 0.11f);
                });
            }

            var contents = current.Contents;

            for (int i = 0; i < contents.Count; i++)
            {
                int slot = i;
                var stack = contents[i];

                AddRow(index++, stack.ToString(), stack.Item.RarityColor, stack.Item, () =>
                {
                    if (!current.TakeItem(slot, inventory, out var taken)) return;

                    CombatLog.Looted(taken.ToString(), taken.Item.RarityColor);
                    IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.pickup,
                                          transform.position, 0.4f, 0.11f);
                });
            }

            // Высота окна по содержимому: пустое место под последней строкой
            // выглядит так, будто там что-то не отрисовалось.
            var rect = (RectTransform)window.transform;

            float listHeight = index * RowHeight;
            rect.sizeDelta = new Vector2(Width,
                TitleHeight + listHeight + ButtonHeight + Pad * 2f + 4f);

            // Кнопка встаёт под последней строкой, а не прибита к низу окна:
            // окно меняет высоту под содержимое, и прибитая кнопка ездила бы
            // относительно списка.
            if (takeAllButton != null)
            {
                takeAllButton.anchoredPosition = new Vector2(Pad, -(TitleHeight + listHeight + 2f));
                takeAllButton.sizeDelta = new Vector2(Width - Pad * 2f, ButtonHeight);

                // Одна строка — собирать нечего скопом, проще ткнуть в неё.
                takeAllButton.gameObject.SetActive(index > 1);
            }
        }

        /// <summary>
        /// Забрать всё разом.
        ///
        /// Идём с конца списка: взятый предмет исчезает из мешка, и обход
        /// с начала пропускал бы каждый второй — индексы съезжают под нами.
        ///
        /// Сумка кончилась — останавливаемся и говорим об этом. Молча
        /// оставить половину добычи в мешке хуже, чем не взять ничего:
        /// игрок уходит уверенным, что забрал всё.
        /// </summary>
        private void TakeEverything()
        {
            if (current == null || inventory == null) return;

            int gold = current.TakeGold(inventory);
            int items = 0;
            bool full = false;

            for (int i = current.Contents.Count - 1; i >= 0; i--)
            {
                if (!current.TakeItem(i, inventory, out var taken))
                {
                    full = true;
                    continue;
                }

                CombatLog.Looted(taken.ToString(), taken.Item.RarityColor);
                items++;
            }

            if (gold > 0) CombatLog.Add(Loc.F("Получено золота: {0}", gold), LogKind.System);

            // Звук один на весь сбор, а не на каждую строку: пять наложенных
            // подборов подряд слышны как треск, а не как добыча.
            if (gold > 0 || items > 0)
                IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.pickup,
                                      transform.position, 0.45f, 0.11f);

            if (full) CombatLog.Add("В сумке нет места — часть добычи осталась.", LogKind.System);
        }

        private void BuildTakeAllButton(RectTransform parent)
        {
            var go = new GameObject("TakeAll", typeof(Image), typeof(Button));
            takeAllButton = (RectTransform)go.transform;
            takeAllButton.SetParent(parent, false);
            takeAllButton.anchorMin = new Vector2(0f, 1f);
            takeAllButton.anchorMax = new Vector2(0f, 1f);
            takeAllButton.pivot = new Vector2(0f, 1f);

            var plate = go.GetComponent<Image>();
            plate.color = ButtonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(TakeEverything);

            var colors = button.colors;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonColor;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var label = MakeText(takeAllButton, "Label", "Собрать всё", 12, TextColor);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
        }

        private void AddRow(int index, string label, Color color, ItemDefinition item,
                            System.Action onClick)
        {
            var go = new GameObject("Row" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(rows, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rect.sizeDelta = new Vector2(0f, RowHeight - 3f);

            go.GetComponent<Image>().color = RowColor;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            if (item != null)
            {
                var tip = go.AddComponent<IsoRPG.UI.ItemTooltipTrigger>();
                tip.Setup(item, GetComponent<IsoRPG.Combat.Experience>());
            }

            var text = MakeText(rect, "Label", label, 12, color);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            spawned.Add(go);
        }

        private void Build()
        {
            var canvasGo = new GameObject("LootHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

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

            var go = new GameObject("LootWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // По центру справа: не закрывает ни персонажа, ни лог боя.
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 60f);
            rect.sizeDelta = new Vector2(Width, 120f);

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

            var title = MakeText(rect, "Title", "Добыча", 13, TextColor);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -Pad * 0.5f);
            titleRect.sizeDelta = new Vector2(0f, TitleHeight);
            title.alignment = TextAnchor.MiddleCenter;

            var list = new GameObject("Rows", typeof(RectTransform));
            rows = (RectTransform)list.transform;

            rows.SetParent(rect, false);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0f, 1f);
            rows.anchoredPosition = new Vector2(Pad, -(TitleHeight + Pad * 0.5f));
            rows.sizeDelta = new Vector2(-Pad * 2f, 0f);

            BuildTakeAllButton(rect);

            // Окно добычи рамку не носит, а потому ApplyFrame его минует —
            // значит и обвязку зовём руками. Остальные окна получают её от
            // рамки; окно, которое стоит на трупе и закрывает собой пол-угла
            // экрана, обязано двигаться не меньше прочих.
            IsoRPG.UI.WindowChrome.MakeDraggable(rect, TitleHeight);

            window = go;
            window.SetActive(false);
        }

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
    }
}
