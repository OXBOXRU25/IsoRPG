using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.UI;

namespace IsoRPG.Quests
{
    /// <summary>
    /// Окно разговора с NPC: текст и кнопки выбора.
    ///
    /// Выбор здесь важнее текста. «Взять» и «Отказаться» — первое решение,
    /// которое игра предлагает игроку словами, а не кликом по врагу; без него
    /// квест выдавался бы автоматически и перестал быть согласием на сделку.
    /// </summary>
    public sealed class DialogueWindow : MonoBehaviour, IsoRPG.UI.IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xE0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0x8A);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color AcceptColor = new Color32(0x4A, 0x7A, 0x4A, 0xFF);
        private static readonly Color DeclineColor = new Color32(0x5A, 0x44, 0x40, 0xFF);

        private const float Width = 460f;
        private const float Pad = 16f;
        private const float ButtonHeight = 32f;

        private Font font;
        private GameObject window;
        private Text title;
        private Text body;
        private RectTransform buttons;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private QuestGiver current;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.

            // Отошёл от собеседника — разговор кончился. Иначе окно висит
            // через полкарты, и сдать квест можно было бы издалека.
            if (current == null ||
                Vector3.Distance(transform.position, current.transform.position) > current.TalkRange + 1f)
                Close();
        }

        public void Open(QuestGiver giver)
        {
            if (giver != null) IsoRPG.Audio.Sfx.VillagerVoice(giver.transform.position);

            if (giver == null) return;

            current = giver;
            window.SetActive(true);

            IsoRPG.Audio.Sfx.OpenWindow();
            Refresh();
        }

        public void Close()
        {
            current = null;
            if (window != null) window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Refresh()
        {
            if (current == null) return;

            var quest = current.Quest;

            LocalizedText.Bind(title, quest != null ? quest.title : "Разговор");
            LocalizedText.Bind(body, current.CurrentText());

            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            switch (current.State)
            {
                case QuestState.Available:
                    AddButton(0, "Взять", AcceptColor, () =>
                    {
                        current.Accept();
                        Close();
                    });
                    AddButton(1, "Отказаться", DeclineColor, Close);
                    break;

                case QuestState.ReadyToTurnIn:
                    AddButton(0, "Отдать", AcceptColor, () =>
                    {
                        current.TurnIn();
                        Close();
                    });
                    AddButton(1, "Позже", DeclineColor, Close);
                    break;

                default:
                    AddButton(0, "Закрыть", DeclineColor, Close);
                    break;
            }
        }

        private void AddButton(int index, string label, Color color, System.Action onClick)
        {
            var go = new GameObject("Button" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(buttons, false);

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(index * 150f, 0f);
            rect.sizeDelta = new Vector2(140f, ButtonHeight);

            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var text = MakeText(rect, "Label", label, 13, TextColor);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;

            spawned.Add(go);
        }

        private void Build()
        {
            var canvasGo = new GameObject("DialogueHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 14;

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

            var go = new GameObject("DialogueWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // По центру, чуть ниже середины: разговор — то, на чём внимание
            // сосредоточено целиком, ему место в середине экрана.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(Width, 190f);

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

            title = MakeText(rect, "Title", "", 15, TextColor);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(Pad, -Pad);
            titleRect.sizeDelta = new Vector2(-Pad * 2f, 22f);

            body = MakeText(rect, "Body", "", 13, TextColor);
            var bodyRect = (RectTransform)body.transform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(Pad, Pad * 2f + ButtonHeight);
            bodyRect.offsetMax = new Vector2(-Pad, -(Pad + 28f));
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var row = new GameObject("Buttons", typeof(RectTransform));
            buttons = (RectTransform)row.transform;
            buttons.SetParent(rect, false);
            buttons.anchorMin = new Vector2(0f, 0f);
            buttons.anchorMax = new Vector2(0f, 0f);
            buttons.pivot = new Vector2(0f, 0f);
            buttons.anchoredPosition = new Vector2(Pad, Pad);
            buttons.sizeDelta = new Vector2(Width - Pad * 2f, ButtonHeight);

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
