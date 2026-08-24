using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IsoRPG.UI
{
    /// <summary>
    /// Позволяет таскать окно мышью за верхнюю полосу.
    ///
    /// Окна у нас открываются по центру и там же остаются. Пока открыто одно,
    /// это незаметно; но сумка вместе с лавкой уже перекрывают друг друга, а
    /// сравнить цену с тем, что лежит в мешке, нужно именно одновременно.
    ///
    /// Тянем за полосу заголовка, а не за всё окно. Иначе промах мимо строки
    /// списка сдвигал бы окно, и попытка нажать на предмет превращалась бы в
    /// перетаскивание — ошибка, которую человек не понимает и повторяет.
    /// </summary>
    public sealed class DraggableWindow : MonoBehaviour,
        IBeginDragHandler, IDragHandler
    {
        /// <summary>Что двигаем. Обычно панель окна, а не сама ручка.</summary>
        private RectTransform target;

        private Canvas canvas;

        /// <summary>
        /// Сколько окна обязано остаться на экране.
        ///
        /// Без запаса окно утаскивается за край целиком и вернуть его нечем:
        /// хвататься больше не за что. Полоса заголовка всегда остаётся
        /// доступной.
        /// </summary>
        private const float KeepVisible = 48f;

        public void Setup(RectTransform panel)
        {
            target = panel;
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) return;

            // Взятое окно выходит поверх остальных. Это ожидаемо настолько,
            // что обратное читается как поломка: тянешь окно, а оно уезжает
            // под соседнее.
            target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;

            // Делим на масштаб холста: событие приходит в пикселях экрана, а
            // положение элемента живёт в единицах интерфейса. На мониторе с
            // другим разрешением окно иначе убегало бы от курсора.
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            target.anchoredPosition += eventData.delta / scale;

            Clamp();
        }

        /// <summary>Не даёт утащить окно за пределы экрана целиком.</summary>
        private void Clamp()
        {
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null) return;

            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 size = target.rect.size;

            // Считаем от центра холста: окна привязаны к центру, поэтому
            // пределы симметричны.
            float maxX = half.x + size.x * 0.5f - KeepVisible;
            float maxY = half.y + size.y * 0.5f - KeepVisible;

            var position = target.anchoredPosition;

            position.x = Mathf.Clamp(position.x, -maxX, maxX);
            position.y = Mathf.Clamp(position.y, -maxY, maxY);

            target.anchoredPosition = position;
        }

        /// <summary>
        /// Вешает на окно полосу-ручку и возвращает её.
        ///
        /// Ручка невидима и лежит поверх заголовка. Своей заливки у неё нет,
        /// но прозрачная картинка нужна: без неё элемент не ловит указатель
        /// вовсе, и перетаскивание молча не работает — самая частая причина
        /// «код есть, а не двигается».
        /// </summary>
        /// <summary>
        /// Тридцать пикселей — это полоса заголовка и ничего больше.
        ///
        /// Сорок, стоявшие тут сначала, залезали на первый ряд ячеек сумки:
        /// заголовок с отступом занимает тридцать четыре, и лишние шесть
        /// пикселей отняли бы у верхних ячеек часть площади нажатия.
        /// </summary>
        public static DraggableWindow Attach(RectTransform panel, float handleHeight = 30f)
        {
            var go = new GameObject("DragHandle", typeof(Image), typeof(DraggableWindow));
            var rect = (RectTransform)go.transform;

            rect.SetParent(panel, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -handleHeight);
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            // Последней в списке, а не первой.
            //
            // В uGUI порядок в иерархии — это порядок отрисовки: первый
            // элемент рисуется снизу, последний сверху. Указатель же ищет
            // цель сверху вниз, поэтому ручка, поставленная первой,
            // оказывалась под заголовком окна и не получала ни одного
            // события. Со стороны это выглядело как «перетаскивание не
            // сделано», хотя код отрабатывал.
            //
            // Кнопку закрытия она не накроет: крестик создаётся после
            // ручки и потому лежит выше неё.
            rect.SetAsLastSibling();

            var drag = go.GetComponent<DraggableWindow>();
            drag.Setup(panel);

            return drag;
        }
    }
}
