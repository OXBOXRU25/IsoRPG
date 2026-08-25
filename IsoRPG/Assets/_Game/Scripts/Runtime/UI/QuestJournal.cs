using System.Collections.Generic;
using IsoRPG.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Quests;

namespace IsoRPG.UI
{
    /// <summary>
    /// Журнал квестов: полный текст задания, цель, награда.
    ///
    /// Панель отслеживания на экране отвечает на вопрос «сколько осталось», и
    /// этого хватает в бою. Журнал отвечает на другой — «кто меня послал и что
    /// обещал», — и нужен он раз в десять минут. Поэтому панель висит всегда,
    /// а журнал открывается по клавише.
    ///
    /// Сданные квесты не стираются: список пройденного — единственный след
    /// того, что игрок вообще успел сделать.
    /// </summary>
    public sealed class QuestJournal : MonoBehaviour, IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        private static readonly Color TitleColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color ActiveColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color ReadyColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);
        private static readonly Color DoneColor = new Color32(0x74, 0x70, 0x66, 0xFF);
        private static readonly Color BodyColor = new Color32(0xA8, 0xA2, 0x94, 0xFF);
        private static readonly Color GoalColor = new Color32(0xC8, 0xC2, 0xB4, 0xFF);
        private static readonly Color RewardColor = new Color32(0x7A, 0xB8, 0xE0, 0xFF);
        private static readonly Color SeparatorColor = new Color32(0x33, 0x30, 0x28, 0xFF);

        private const float Width = 400f;
        private const float MaxHeight = 560f;

        private QuestLog log;
        private Font font;
        private GameObject window;
        private RectTransform content;

        private readonly List<GameObject> spawned = new List<GameObject>();

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            log = GetComponent<QuestLog>();
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
            if (log != null) log.Changed += RefreshIfOpen;
        }

        private void OnDisable()
        {
            Loc.Changed -= RefreshIfOpen;
            if (log != null) log.Changed -= RefreshIfOpen;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || window == null) return;

            // J — journal, как в играх жанра.
            if (keyboard.jKey.wasPressedThisFrame) Toggle();

            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.
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
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (log == null) return;

            int shown = 0;

            // Порядок: сначала то, что можно сдать прямо сейчас, потом взятое,
            // и только потом сданное. Игрок открывает журнал ради первого.
            foreach (var state in new[] { QuestState.ReadyToTurnIn, QuestState.Active, QuestState.Completed })
            {
                foreach (var quest in log.Known)
                {
                    if (quest == null || log.StateOf(quest) != state) continue;

                    AddQuest(quest, state, shown > 0);
                    shown++;
                }
            }

            if (shown == 0)
            {
                var empty = MakeText(content, "Empty",
                    "Заданий пока нет. Поговори с теми, у кого над головой висит восклицательный знак.",
                    12, BodyColor);

                spawned.Add(empty.gameObject);
            }
        }

        private void AddQuest(QuestDefinition quest, QuestState state, bool separator)
        {
            if (separator) spawned.Add(MakeSeparator());

            Color color = state switch
            {
                QuestState.ReadyToTurnIn => ReadyColor,
                QuestState.Completed => DoneColor,
                _ => ActiveColor,
            };

            string suffix = state switch
            {
                QuestState.ReadyToTurnIn => "   " + Loc.T("— можно сдать"),
                QuestState.Completed => "   " + Loc.T("— сдано"),
                _ => "",
            };

            var title = MakeText(content, "QuestTitle", quest.title + suffix, 14, color);
            spawned.Add(title.gameObject);

            if (!string.IsNullOrEmpty(quest.offerText))
            {
                var body = MakeText(content, "QuestText", quest.offerText, 12,
                    state == QuestState.Completed ? DoneColor : BodyColor);

                spawned.Add(body.gameObject);
            }

            if (state != QuestState.Completed && quest.requiredItem != null)
            {
                int have = log.Progress(quest);

                var goal = MakeText(content, "QuestGoal", Loc.F("Цель:  {0}", quest.ObjectiveLine(have)), 12,
                    have >= quest.requiredCount ? ReadyColor : GoalColor);

                spawned.Add(goal.gameObject);
            }

            string reward = RewardLine(quest);
            if (!string.IsNullOrEmpty(reward) && state != QuestState.Completed)
            {
                var rewardText = MakeText(content, "QuestReward", Loc.F("Награда:  {0}", reward), 12, RewardColor);
                spawned.Add(rewardText.gameObject);
            }
        }

        private static string RewardLine(QuestDefinition quest)
        {
            var parts = new List<string>();

            if (quest.rewardItem != null)
            {
                string name = quest.rewardItem.displayName;
                if (quest.rewardCount > 1) name += " x" + quest.rewardCount;
                parts.Add(name);
            }

            if (quest.rewardExperience > 0) parts.Add(Loc.F("{0} опыта", quest.rewardExperience));
            if (quest.rewardGold > 0) parts.Add(Loc.F("{0} золота", quest.rewardGold));

            return string.Join(",  ", parts);
        }

        private GameObject MakeSeparator()
        {
            var go = new GameObject("Separator", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(content, false);

            go.GetComponent<Image>().color = SeparatorColor;

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 1f;
            element.minHeight = 1f;

            return go;
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("QuestJournalHUD",
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

            var go = new GameObject("QuestJournalWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // По центру: журнал читают, а не поглядывают на него краем глаза.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Width, MaxHeight);

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

            var header = MakeText(rect, "Header", "Журнал заданий", 15, TitleColor);
            var headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -12f);
            headerRect.sizeDelta = new Vector2(-24f, 22f);
            header.alignment = TextAnchor.MiddleCenter;

            var hint = MakeText(rect, "Hint", "J — открыть и закрыть", 11, DoneColor);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 10f);
            hintRect.sizeDelta = new Vector2(-24f, 16f);
            hint.alignment = TextAnchor.MiddleCenter;

            var listGo = new GameObject("Content", typeof(RectTransform),
                                        typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = (RectTransform)listGo.transform;
            content.SetParent(rect, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -40f);
            content.sizeDelta = new Vector2(-28f, 0f);

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            listGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            WindowChrome.AddCloseButton(rect, font, Close);

            window = go;
            window.SetActive(false);
        }

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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;

            return text;
        }
    }
}
