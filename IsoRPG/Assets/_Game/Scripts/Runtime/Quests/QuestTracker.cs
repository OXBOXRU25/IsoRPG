using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
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
        /// <summary>Счётчик заданий вверху панели — самый светлый, он же и заголовок.</summary>
        private static readonly Color HeaderColor = new Color32(0xF2, 0xD9, 0x8A, 0xFF);
        /// <summary>Название зоны — белым, как в образце: это не задание, а группа.</summary>
        private static readonly Color ZoneColor = new Color32(0xF0, 0xEE, 0xE6, 0xFF);
        private static readonly Color GoalColor = new Color32(0xC8, 0xC2, 0xB4, 0xFF);
        private static readonly Color DoneColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);

        private const float Width = 230f;
        private const float LineHeight = 19f;

        /// <summary>Потолок взятых заданий. Столько же в WoW, и число видно игроку в счётчике.</summary>
        private const int MaxActive = 20;

        private QuestLog log;
        private Font font;
        private RectTransform root;

        private readonly List<GameObject> spawned = new List<GameObject>();

        /// <summary>Задания, у которых цели свёрнуты кликом по названию.</summary>
        private readonly HashSet<QuestDefinition> collapsed = new HashSet<QuestDefinition>();

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
            Loc.Changed += Refresh;
            if (log != null) log.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
            if (log != null) log.Changed -= Refresh;
        }

        /// <summary>
        /// Пересобирает панель по образцу WoW.
        ///
        /// Строение снято с игры 01.09.2026 по просьбе Павла: сверху счётчик
        /// «Активные задания: 1/20», под ним зона, под ней задания — уровень
        /// в скобках, название, и строка выполнения с отбивкой вправо.
        /// Прежде тут был плоский список названий, и по нему нельзя было ни
        /// понять, сколько заданий влезает, ни свернуть надоевшее.
        /// </summary>
        private void Refresh()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (log == null) return;

            var active = new List<QuestDefinition>();
            foreach (var quest in log.Known)
            {
                if (quest != null && log.IsActive(quest)) active.Add(quest);
            }

            int line = 0;

            if (active.Count > 0)
            {
                AddLine(line++, Loc.T("Активные задания") + ": " + active.Count + "/" + MaxActive,
                        HeaderColor, 13);

                // Зона — заголовок группы. Берём у первого задания: пока
                // локация одна, разбивать список по зонам не на что, но поле
                // уже есть у каждого квеста, и группировка встанет сюда же.
                string zone = active[0] != null ? active[0].zone : null;
                if (!string.IsNullOrEmpty(zone)) AddLine(line++, zone, ZoneColor, 13);
            }

            foreach (var quest in active)
            {
                bool folded = collapsed.Contains(quest);

                // Название задания — кнопка: клик сворачивает и разворачивает
                // его цели. В образце так же, и это единственный способ
                // разгрузить панель, когда заданий много.
                var titleLine = AddLine(line++, (folded ? "+ " : "- ") + "[" + quest.level + "] " + quest.title,
                                        TitleColor, 13);
                MakeFoldable(titleLine, quest);

                if (folded) continue;

                int have = log.Progress(quest);
                bool done = have >= quest.requiredCount;

                // Выполненная цель окрашивается, а не исчезает: игрок должен
                // видеть, что осталось только дойти до заказчика.
                var goal = AddLine(line++, quest.ObjectiveLine(have) + (done ? "  " + Loc.T("готово") : ""),
                                   done ? DoneColor : GoalColor, 12);

                // Отбивка вправо — как в образце: цель читается как итог
                // строки задания, а не как её продолжение.
                goal.alignment = TextAnchor.MiddleRight;
            }

            root.gameObject.SetActive(line > 0);
            root.sizeDelta = new Vector2(Width, line * LineHeight + 12f);
        }

        /// <summary>
        /// Вешает на строку названия сворачивание.
        ///
        /// Кнопкой, а не отдельным значком: попасть в узкий треугольник на
        /// краю панели трудно, а в строку целиком — легко.
        /// </summary>
        private void MakeFoldable(Text titleLine, QuestDefinition quest)
        {
            if (titleLine == null || quest == null) return;

            titleLine.raycastTarget = true;

            var button = titleLine.gameObject.AddComponent<Button>();
            button.targetGraphic = titleLine;
            button.onClick.AddListener(() =>
            {
                if (!collapsed.Remove(quest)) collapsed.Add(quest);
                Refresh();
            });
        }

        private Text AddLine(int index, string text, Color color, int size)
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
            LocalizedText.Bind(label, text);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            spawned.Add(go);

            return label;
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
            // Тянемся за шириной, а не за средним между шириной и высотой.
            //
            // При среднем масштаб выходит дробным на любом экране, который не
            // 16:9: на 1920x1200 это 1.054, и шрифт растеризуется между
            // пикселями — надписи выглядят размытыми, особенно мелкие.
            // По ширине на том же экране масштаб ровно 1.0, и текст чёткий.
            scaler.matchWidthOrHeight = 0f;

            var go = new GameObject("QuestTracker", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent((RectTransform)canvasGo.transform, false);

            // Справа вверху — там же, где в играх жанра, и там не мешает
            // ни логу боя слева внизу, ни панели способностей. Но ПОД
            // миникартой: 01.09.2026 Павлон увидел, что список задач залезает
            // на неё углом. Низ карты считаем по ней самой, а не числом:
            // подвинут карту — трекер поедет следом, а не разъедется с ней.
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-18f, -MinimapBottom());
            root.sizeDelta = new Vector2(Width, 60f);

            go.SetActive(false);
        }

        /// <summary>
        /// Отступ сверху, на котором кончается миникарта вместе с подписью.
        ///
        /// Ищем её рамку в сцене и меряем; не нашли — запасное число по
        /// нынешним размерам карты (190 + поля + строка координат).
        /// </summary>
        private static float MinimapBottom()
        {
            const float Fallback = 236f;

            var frame = GameObject.Find("MinimapFrame");
            if (frame == null) return Fallback;

            if (frame.transform is not RectTransform rect) return Fallback;

            // Подпись координат висит под рамкой её ребёнком, в sizeDelta не
            // входит — добавляем её высоту и зазор до первой строки задач.
            return Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y + 32f;
        }
    }
}
