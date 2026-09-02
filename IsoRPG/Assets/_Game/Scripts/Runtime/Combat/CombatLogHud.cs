using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.UI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Окно боевого лога в правом нижнем углу.
    ///
    /// Показывает последние строки и подписывается на статический CombatLog —
    /// сама логика боя про это окно ничего не знает и знать не должна.
    /// </summary>
    public sealed class CombatLogHud : MonoBehaviour
    {
        // Полупрозрачная подложка: лог занимает угол экрана постоянно, и
        // глухая плашка там съедает часть локации. Читаемость держится на
        // контрасте текста, а не на плотности фона.
        private static readonly Color PanelColor = new Color32(0x14, 0x13, 0x10, 0x8A);
        // Рамка тоже полупрозрачная. Она лежит ПОД панелью и чуть больше
        // неё — то есть заполняет собой всю площадь. Непрозрачная рамка
        // сводит прозрачность панели к нулю: сквозь стекло видна доска.
        private static readonly Color PanelEdge = new Color32(0x30, 0x2C, 0x24, 0x5A);

        // Цвета по видам сообщений. Игрок различает их боковым зрением,
        // не вчитываясь: красное — по нам попали, жёлтое — крит или золото.
        private static readonly Color DealtColor = new Color32(0xE0, 0xDC, 0xD0, 0xFF);
        private static readonly Color TakenColor = new Color32(0xE0, 0x6A, 0x5A, 0xFF);
        private static readonly Color CritColor = new Color32(0xFF, 0xC4, 0x4A, 0xFF);
        private static readonly Color MissColor = new Color32(0x9A, 0x96, 0x8E, 0xFF);
        private static readonly Color LootColor = new Color32(0x7A, 0xC8, 0xE0, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color ExpColor = new Color32(0xA0, 0x86, 0xD8, 0xFF);
        private static readonly Color SystemColor = new Color32(0xB0, 0xAA, 0x9A, 0xFF);

        private const float Margin = 18f;
        private const float Width = 330f;
        private const float LineHeight = 16f;
        private const int MaxLines = 12;
        private const float Pad = 8f;

        /// <summary>
        /// Низ журнала — вровень с низом ряда приёмов.
        ///
        /// Павлон 02.09.2026: «опусти его ниже, чтобы был параллельно блокам
        /// со скилами». Раньше журнал стоял на 84 точках — его подняли, чтобы
        /// не налезал на иконку сумки, — и нижняя строка висела выше ряда
        /// приёмов, отчего низ экрана читался как две разные полки.
        ///
        /// Число то же, что у ряда приёмов: поле экрана плюс полоска опыта
        /// (`ScreenMargin + ExpBarHeight` в CombatHud). Держим здесь копией,
        /// а не ссылкой: журнал живёт отдельным компонентом и не должен
        /// знать про устройство боевого интерфейса.
        /// </summary>
        private const float BottomOffset = 28f;

        private readonly List<Text> lines = new List<Text>();
        private readonly List<string> buffer = new List<string>();
        private readonly List<LogKind> kinds = new List<LogKind>();

        private Font font;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void OnEnable() => CombatLog.LineAdded += OnLineAdded;
        private void OnDisable() => CombatLog.LineAdded -= OnLineAdded;

        private void Build()
        {
            var canvasGo = new GameObject("CombatLogHUD",
                typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

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

            float height = MaxLines * LineHeight + Pad * 2f;

            var panelGo = new GameObject("LogPanel", typeof(Image));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(root, false);
            // Левый нижний угол — там же, где чат в играх этого жанра.
            // Справа живёт сумка, и делить с ней угол незачем.
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(Margin, BottomOffset);
            panel.sizeDelta = new Vector2(Width, height);

            var image = panelGo.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            var edge = new GameObject("Edge", typeof(Image));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(panel, false);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = new Vector2(-1f, -1f);
            edgeRect.offsetMax = new Vector2(1f, 1f);
            edge.transform.SetAsFirstSibling();

            var edgeImage = edge.GetComponent<Image>();
            edgeImage.color = PanelEdge;
            edgeImage.raycastTarget = false;

            // Строки снизу вверх: новая появляется внизу, старые уползают.
            for (int i = 0; i < MaxLines; i++)
            {
                var textGo = new GameObject("Line" + i, typeof(Text));
                var rect = (RectTransform)textGo.transform;
                rect.SetParent(panel, false);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(Pad, Pad + i * LineHeight);
                rect.sizeDelta = new Vector2(-Pad * 2f, LineHeight);

                var text = textGo.GetComponent<Text>();
                text.font = font;
                text.fontSize = 12;
                text.color = SystemColor;
                text.text = "";
                text.alignment = TextAnchor.MiddleLeft;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Truncate;

                lines.Add(text);
            }
        }

        private void OnLineAdded(string text, LogKind kind)
        {
            buffer.Add(text);
            kinds.Add(kind);

            while (buffer.Count > MaxLines)
            {
                buffer.RemoveAt(0);
                kinds.RemoveAt(0);
            }

            Redraw();
        }

        private void Redraw()
        {
            // lines[0] — самая нижняя строка, значит самая свежая запись.
            for (int i = 0; i < lines.Count; i++)
            {
                int index = buffer.Count - 1 - i;

                if (index < 0)
                {
                    lines[i].text = "";
                    continue;
                }

                lines[i].text = Loc.T(buffer[index]);
                lines[i].color = ColorOf(kinds[index]);
            }
        }

        private static Color ColorOf(LogKind kind) => kind switch
        {
            LogKind.DamageDealt => DealtColor,
            LogKind.DamageTaken => TakenColor,
            LogKind.Crit => CritColor,
            LogKind.Miss => MissColor,
            LogKind.Loot => LootColor,
            LogKind.Gold => GoldColor,
            LogKind.Experience => ExpColor,
            _ => SystemColor
        };
    }
}
