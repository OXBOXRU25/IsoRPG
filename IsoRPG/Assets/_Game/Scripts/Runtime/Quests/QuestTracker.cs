using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.Quests
{
    /// <summary>
    /// Панель отслеживания: какие квесты взяты и сколько осталось.
    ///
    /// Висит на экране постоянно, пока есть активные квесты, и исчезает,
    /// когда их нет. Это единственное место, где игрок видит цель, не открывая
    /// ничего: квест, спрятанный в журнале по клавише, забывается через
    /// пять минут боя.
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        private static readonly Color TitleColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color GoalColor = new Color32(0xC8, 0xC2, 0xB4, 0xFF);
        private static readonly Color DoneColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);

        private const float Width = 230f;
        private const float LineHeight = 19f;

        private QuestLog log;
        private Font font;
        private RectTransform root;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private void Awake()
        {
            log = GetComponent<QuestLog>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void OnEnable()
        {
            if (log != null) log.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (log != null) log.Changed -= Refresh;
        }

        private void Refresh()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (log == null) return;

            int line = 0;

            foreach (var quest in log.Known)
            {
                if (quest == null || !log.IsActive(quest)) continue;

                AddLine(line++, quest.title, TitleColor, 13);

                int have = log.Progress(quest);
                bool done = have >= quest.requiredCount;

                // Выполненная цель окрашивается, а не исчезает: игрок должен
                // видеть, что осталось только дойти до заказчика.
                AddLine(line++, "   " + quest.ObjectiveLine(have) +
                                (done ? "  готово" : ""),
                        done ? DoneColor : GoalColor, 12);

                line++;   // пустая строка между квестами
            }

            root.gameObject.SetActive(line > 0);
            root.sizeDelta = new Vector2(Width, line * LineHeight + 12f);
        }

        private void AddLine(int index, string text, Color color, int size)
        {
            var go = new GameObject("Line" + index, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * LineHeight - 6f);
            rect.sizeDelta = new Vector2(0f, LineHeight);

            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            spawned.Add(go);
        }

        private void Build()
        {
            var canvasGo = new GameObject("QuestHUD",
                typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("QuestTracker", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent((RectTransform)canvasGo.transform, false);

            // Справа вверху — там же, где в играх жанра, и там не мешает
            // ни логу боя слева внизу, ни панели способностей.
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-18f, -18f);
            root.sizeDelta = new Vector2(Width, 60f);

            go.SetActive(false);
        }
    }
}
