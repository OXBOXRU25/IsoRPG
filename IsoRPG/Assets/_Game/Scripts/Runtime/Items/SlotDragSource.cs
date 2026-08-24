using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IsoRPG.Items
{
    /// <summary>
    /// Перетаскивание вещи из ячейки сумки.
    ///
    /// Отпустил внутри окна — ничего не произошло; отпустил снаружи — вещь
    /// упала на землю мешком. Это привычный жест: так вещи выбрасывают почти
    /// во всех играх с инвентарём, и объяснять его не нужно.
    ///
    /// Подтверждения нет намеренно. Окно с вопросом «точно выбросить?» при
    /// каждом движении превращает частое действие в мучение, а редкую ошибку
    /// всё равно не предотвращает — на третий раз его закрывают не глядя.
    /// Цена ошибки здесь мала: вещь лежит под ногами, её видно и можно
    /// поднять обратно.
    /// </summary>
    public sealed class SlotDragSource : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int index;
        private Inventory inventory;
        private RectTransform window;
        private Canvas canvas;

        /// <summary>Иконка, которая едет за курсором.</summary>
        private GameObject ghost;

        public void Setup(int slotIndex, Inventory bag, RectTransform windowRect)
        {
            index = slotIndex;
            inventory = bag;
            window = windowRect;
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (inventory == null) return;

            var stack = inventory.GetSlot(index);
            if (stack.IsEmpty || stack.Item == null) return;

            var sprite = stack.Item.icon;
            if (sprite == null) return;

            ghost = new GameObject("DragGhost", typeof(Image));
            var rect = (RectTransform)ghost.transform;

            // Кладём в корень холста, а не рядом с ячейкой: иначе иконку
            // обрежет окно, стоит только вынести её за край, — а нам нужно
            // именно это движение.
            rect.SetParent(canvas != null ? canvas.transform : transform.root, false);
            rect.sizeDelta = new Vector2(40f, 40f);

            var image = ghost.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            // Полупрозрачная: так видно, что вещь ещё не перенесена, а только
            // взята в руку.
            image.color = new Color(1f, 1f, 1f, 0.85f);

            // Не ловит указатель: иначе она же и оказалась бы под курсором,
            // и проверка «отпустили над окном» всегда давала бы иконку.
            image.raycastTarget = false;

            Move(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;

            if (inventory == null || window == null) return;

            // Холст в режиме наложения на экран: камеру передавать не нужно,
            // и передача ненулевой камеры как раз всё и ломает — проверка
            // начинает считать в другой системе координат.
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            bool insideWindow = RectTransformUtility.RectangleContainsScreenPoint(
                window, eventData.position, camera);

            if (insideWindow) return;

            var dropper = inventory.GetComponent<ItemDropper>();
            if (dropper == null) return;

            dropper.DropSlot(index);
        }

        private void Move(PointerEventData eventData)
        {
            if (ghost == null || canvas == null) return;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;

            Vector2 point;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out point);

            ((RectTransform)ghost.transform).anchoredPosition = point;
        }
    }
}
