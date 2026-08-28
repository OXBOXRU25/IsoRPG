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
        /// Отступ окна от края экрана. Ноль — окно упирается вплотную.
        ///
        /// Раньше здесь было «оставим 48 пикселей на экране, чтобы окно можно
        /// было вернуть», то есть окно разрешалось увести за край почти
        /// целиком. Так делать незачем: в WoW у фреймов стоит
        /// SetClampedToScreen, и окно не уезжает за край ни на пиксель —
        /// а раз увести нельзя, то и возвращать нечего. Задача, под которую
        /// был написан запас, не существует.
        /// </summary>
        private const float ScreenPad = 0f;

        /// <summary>
        /// Порядок, с которого поднимаем взятое окно над соседями.
        ///
        /// Двадцать один — сразу над самым верхним окном в покое (настройки
        /// на двадцати) и заметно ниже подсказки (50) и экрана смерти (60):
        /// поднятое окно обязано перекрыть соседей, но не всплывающую
        /// подсказку о предмете, который в нём же и лежит.
        /// </summary>
        private const int FrontFrom = 21;

        /// <summary>
        /// Выше не поднимаемся. Досчитав до потолка, начинаем заново с
        /// двадцати двух — это всё равно выше любого окна в покое, так что
        /// сброс не виден; расходится лишь порядок между окнами, которые
        /// уже двигали.
        /// </summary>
        private const int FrontCap = 45;

        private static int front = FrontFrom;

        /// <summary>Углы окна. Поле, а не переменная: считаются каждый кадр.</summary>
        private readonly Vector3[] corners = new Vector3[4];

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
            //
            // Через sortingOrder, а не через SetAsLastSibling. Прежний вызов
            // не работал и работать не мог: каждое окно — корень СВОЕГО
            // холста, а холсты между собой упорядочены не иерархией, а
            // порядком сортировки. Окно честно становилось последним среди
            // своих детей, то есть среди самого себя, и продолжало уезжать
            // под соседа.
            BringToFront(canvas);
        }

        /// <summary>
        /// Поднять окно над соседними.
        ///
        /// Публичная и статическая, потому что зовут её из двух мест: отсюда,
        /// когда окно потащили, и из <see cref="WindowRaiser"/>, когда по
        /// окну просто нажали. Второе важнее первого и его-то и не было:
        /// тащить окно можно только за заголовок, а нажимают люди куда
        /// попало — по списку вещей, по кнопке, по пустому месту. Нижнее
        /// окно от такого нажатия оставалось внизу, и выглядело это как
        /// «подъём не работает вовсе».
        /// </summary>
        public static void BringToFront(Canvas canvas)
        {
            if (canvas == null) return;

            front++;
            if (front > FrontCap) front = FrontFrom + 1;
            canvas.sortingOrder = front;

            // И порядком в иерархии заодно. При равном sortingOrder решает
            // именно он, а холсты всех окон — соседи под одним объектом
            // игрока. Стоит дёшево и закрывает случай, когда два окна
            // оказались на одном номере.
            canvas.transform.SetAsLastSibling();
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

        /// <summary>
        /// Не даёт утащить окно за пределы экрана целиком.
        ///
        /// Считаем по настоящим углам окна, а не по формуле от центра холста.
        /// Прежняя формула исходила из того, что все окна привязаны к центру,
        /// — а окно персонажа привязано к левому верхнему углу. Для него
        /// предел ложился поперёк экрана: окно проезжало пару сотен пикселей
        /// и упиралось в невидимую стену примерно на середине. Со стороны это
        /// и выглядит как «окна не двигаются».
        ///
        /// Сдвиг считается в координатах холста и прибавляется к
        /// anchoredPosition напрямую: панель окна — прямой ребёнок своего
        /// холста, эти системы координат совпадают. То же допущение уже
        /// заложено в OnDrag.
        /// </summary>
        private void Clamp()
        {
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null) return;

            target.GetWorldCorners(corners);

            for (int i = 0; i < 4; i++)
                corners[i] = canvasRect.InverseTransformPoint(corners[i]);

            // 0 — левый нижний, 1 — левый верхний, 2 — правый верхний.
            float left = corners[0].x;
            float right = corners[2].x;
            float bottom = corners[0].y;
            float top = corners[1].y;

            Rect area = canvasRect.rect;

            Vector2 shift = Vector2.zero;

            // Окно целиком внутри экрана — обе стороны сразу. Если окно шире
            // экрана, левый край побеждает: уехавший вправо правый край
            // человек хотя бы видит, а уехавший влево заголовок не поймать.
            if (right > area.xMax - ScreenPad) shift.x = area.xMax - ScreenPad - right;
            if (left + shift.x < area.xMin + ScreenPad)
                shift.x = area.xMin + ScreenPad - left;

            if (top > area.yMax - ScreenPad) shift.y = area.yMax - ScreenPad - top;
            if (bottom + shift.y < area.yMin + ScreenPad)
                shift.y = area.yMin + ScreenPad - bottom;

            if (shift != Vector2.zero)
                target.anchoredPosition += shift;
        }

        /// <summary>
        /// Вешает на окно полосу-ручку и возвращает её.
        ///
        /// Ручка невидима и лежит поверх заголовка. Своей заливки у неё нет,
        /// но прозрачная картинка нужна: без неё элемент не ловит указатель
        /// вовсе, и перетаскивание молча не работает — самая частая причина
        /// «код есть, а не двигается».
        /// </summary>
        /// <param name="handleHeight">
        /// Тридцать пикселей — это полоса заголовка и ничего больше.
        ///
        /// Сорок, стоявшие тут сначала, залезали на первый ряд ячеек сумки:
        /// заголовок с отступом занимает тридцать четыре, и лишние шесть
        /// пикселей отняли бы у верхних ячеек часть площади нажатия.
        /// </param>
        /// <param name="overhang">
        /// На сколько ручка вылезает за края панели — вверх и в стороны.
        ///
        /// Нужно из-за нарисованной рамки: она кладётся ОТДЕЛЬНЫМ слоем
        /// снаружи панели и потому выступает за её край. Видимый верх окна
        /// уезжает вверх на эту величину, а панель остаётся где была — и
        /// золотая кромка, за которую человек и хватается, оказывается вне
        /// панели, где ловить указатель нечем: детали рамки нажатий не
        /// принимают намеренно. Живой оставалась узкая полоска НИЖЕ золота,
        /// прямо под надписью заголовка, — то есть там, где браться никто
        /// не станет.
        /// </param>
        public static DraggableWindow Attach(RectTransform panel,
                                             float handleHeight = 30f,
                                             float overhang = 0f)
        {
            var go = new GameObject("DragHandle", typeof(Image), typeof(DraggableWindow));
            var rect = (RectTransform)go.transform;

            rect.SetParent(panel, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(-overhang, -handleHeight);
            rect.offsetMax = new Vector2(overhang, overhang);

            // Раскладка ручку не трогает.
            //
            // У окна настроек на самой панели висит VerticalLayoutGroup: без
            // этой строки она выстроила бы ручку как обычную строку списка и
            // заодно отняла бы под неё место у настоящего содержимого.
            go.AddComponent<LayoutElement>().ignoreLayout = true;

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
