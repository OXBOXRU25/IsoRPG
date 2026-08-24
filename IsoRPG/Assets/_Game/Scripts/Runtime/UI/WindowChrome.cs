using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.UI
{
    /// <summary>
    /// Крестик закрытия — один на все окна.
    ///
    /// Esc закрывает всё и так, но полагаться на это нельзя: игрок узнаёт про
    /// Esc, только если попробует, а до тех пор окно без видимого выхода
    /// читается как зависшее. Крестик — единственная кнопка, которую ищут
    /// глазами, не думая.
    /// </summary>
    public static class WindowChrome
    {
        private static readonly Color Idle = new Color32(0x8A, 0x84, 0x76, 0xFF);
        private static readonly Color Hover = new Color32(0xE8, 0x9A, 0x8A, 0xFF);
        private static readonly Color Plate = new Color32(0x2A, 0x27, 0x21, 0x00);
        private static readonly Color PlateHover = new Color32(0x3A, 0x2A, 0x24, 0xFF);

        /// <summary>
        /// 26 на 26 — меньше обычной цели в 48, и намеренно: крестик стоит
        /// в углу окна, где мимо него промахнуться некуда, а полноразмерная
        /// кнопка съела бы заголовок.
        /// </summary>
        private const float Size = 26f;

        /// <summary>
        /// Общая обвязка окна: крестик в углу и полоса, за которую окно
        /// таскают мышью.
        ///
        /// Оба здесь, а не в каждом окне отдельно, по одной причине: новое
        /// окно однажды забудут снабдить ручкой, и оно единственное станет
        /// вести себя не как остальные.
        /// </summary>
        public static void AddCloseButton(RectTransform panel, Font font,
                                          UnityEngine.Events.UnityAction onClose)
        {
            DraggableWindow.Attach(panel);

            var go = new GameObject("Close", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-4f, -4f);
            rect.sizeDelta = new Vector2(Size, Size);

            var plate = go.GetComponent<Image>();
            plate.color = Plate;

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(onClose);

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = PlateHover;
            colors.pressedColor = PlateHover;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var textGo = new GameObject("X", typeof(Text));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = font;

            // Латинская «икс», а не символ умножения: встроенный шрифт Unity
            // рисует далеко не всё, и вместо крестика легко получить пустой
            // прямоугольник.
            text.text = "x";
            text.fontSize = 16;
            text.color = Idle;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            // Подсветка цветом текста: закрашивать плашку целиком под мышью —
            // слишком громко для кнопки, которую задевают мимоходом.
            var tint = go.AddComponent<CloseButtonTint>();
            tint.Setup(text, Idle, Hover);
        }
    }

    /// <summary>Перекрашивает крестик под курсором.</summary>
    public sealed class CloseButtonTint : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private Text target;
        private Color idle;
        private Color hover;

        public void Setup(Text text, Color idleColor, Color hoverColor)
        {
            target = text;
            idle = idleColor;
            hover = hoverColor;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (target != null) target.color = hover;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (target != null) target.color = idle;
        }
    }
}
