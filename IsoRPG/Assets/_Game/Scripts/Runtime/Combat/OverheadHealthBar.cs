using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Полоска здоровья над головой. Строит себя сама, префаб не нужен.
    ///
    /// Собрана на мировом Canvas и разворачивается к камере каждый кадр:
    /// при изометрии камера не крутится, но зум меняет масштаб, и полоска
    /// должна оставаться одинаковой на экране, а не расти вместе с миром.
    /// </summary>
    [RequireComponent(typeof(Targetable))]
    public sealed class OverheadHealthBar : MonoBehaviour
    {
        [SerializeField] private float width = 1.1f;

        [Tooltip("Высота полоски здоровья. Вдвое тоньше прежней: 0.13 читалось как плашка, а не как полоска.")]
        [SerializeField] private float height = 0.065f;

        [Tooltip("Высота ряда комбо-очков под здоровьем. Заметно уже самой полоски — это отметка, а не шкала.")]
        [SerializeField] private float comboHeight = 0.032f;

        [Tooltip("Просвет между здоровьем и комбо, и между самими фрагментами.")]
        [SerializeField] private float gap = 0.012f;

        [SerializeField] private Color fillColor = new Color32(0xC4, 0x3A, 0x3A, 0xFF);
        [SerializeField] private Color backColor = new Color32(0x1A, 0x1A, 0x1A, 0xC0);

        [Tooltip("Цвет набранного комбо-очка.")]
        [SerializeField] private Color comboColor = new Color32(0xF0, 0xC8, 0x40, 0xFF);

        [Tooltip("Прятать полоску при полном здоровье. У нетронутых монстров она только засоряет экран.")]
        [SerializeField] private bool hideWhenFull = true;

        private Targetable targetable;
        private Health health;
        private Canvas canvas;
        private RectTransform fillRect;
        private CanvasGroup group;
        private Camera cam;

        /// <summary>
        /// Фрагменты комбо. Их ровно столько, сколько очков в механике, и
        /// закрашиваются они слева направо.
        /// </summary>
        private Image[] comboSegments;
        private CanvasGroup comboGroup;

        /// <summary>
        /// Комбо-очки игрока. Ищем один раз при включении: очки копятся НА
        /// ЦЕЛИ, поэтому каждая полоска спрашивает у них своё число сама.
        /// Поиск по сцене в кадре — то, чем мы уже роняли игру.
        /// </summary>
        private ComboPoints combo;

        /// <summary>
        /// Размеры от задания сборки.
        ///
        /// Нужно потому, что уже расставленный компонент правку умолчания в
        /// коде не догоняет: в сцене лежат старые числа, и полоска осталась бы
        /// прежней толщины у всех, кто уже стоит на карте.
        /// </summary>
        public void SetSize(float barWidth, float barHeight, float combo, float space)
        {
            width = barWidth;
            height = barHeight;
            comboHeight = combo;
            gap = space;
        }

        private void Awake()
        {
            targetable = GetComponent<Targetable>();
            health = GetComponent<Health>();
            cam = Camera.main;

            // ДО сборки: число фрагментов комбо берётся у самой механики.
            combo = FindFirstObjectByType<ComboPoints>();

            Build();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Changed += OnHealthChanged;
                OnHealthChanged(health.Current, health.Max);
            }

            if (combo == null) combo = FindFirstObjectByType<ComboPoints>();

            if (combo != null)
            {
                combo.Changed += OnComboChanged;
                RefreshCombo();
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (combo != null) combo.Changed -= OnComboChanged;
        }

        /// <summary>
        /// Комбо изменились где-то в бою. Число в событии — про ТЕКУЩУЮ цель
        /// игрока, а нас интересует своя, поэтому спрашиваем отдельно.
        /// </summary>
        private void OnComboChanged(int points, int max) => RefreshCombo();

        private void RefreshCombo()
        {
            if (comboSegments == null || comboGroup == null) return;

            int mine = combo != null ? combo.PointsOn(targetable) : 0;

            // Пустой ряд у каждого моба на карте — мусор. Показываем его
            // только там, где очки уже есть: тогда видно и сколько набрано, и
            // сколько осталось до финишера.
            comboGroup.alpha = mine > 0 ? 1f : 0f;

            for (int i = 0; i < comboSegments.Length; i++)
                comboSegments[i].color = i < mine ? comboColor : backColor;
        }

        private void Build()
        {
            var root = new GameObject("HealthBar", typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.layer = gameObject.layer;

            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            group = root.GetComponent<CanvasGroup>();

            // Полоска не должна ловить клики: иначе она перехватывает выбор
            // цели у монстра, над которым висит.
            group.blocksRaycasts = false;
            group.interactable = false;

            // Холст держит обе строки: здоровье сверху, комбо под ним.
            float total = height + gap + comboHeight;

            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(width, total);

            // --- здоровье ----------------------------------------------------
            var bar = new GameObject("Health", typeof(RectTransform));
            bar.transform.SetParent(canvasRect, false);
            bar.layer = root.layer;

            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(0f, -height);
            barRect.offsetMax = Vector2.zero;

            var back = CreateImage("Back", barRect, backColor);
            Stretch(back);

            var fill = CreateImage("Fill", barRect, fillColor);
            Stretch(fill);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fillRect = fill;

            BuildCombo(canvasRect, root.layer);
        }

        /// <summary>
        /// Ряд комбо-очков: узкая полоска под здоровьем, разрезанная на пять
        /// фрагментов.
        ///
        /// Кружками их рисуют в интерфейсе игрока, а над целью нужна именно
        /// полоска: она читается тем же жестом, что и здоровье, и не спорит с
        /// ним по форме. Замысел Павлона 02.09.2026 — фрагменты того же
        /// тёмного цвета, что фон здоровья, набранные загораются жёлтым.
        ///
        /// Число фрагментов берём у самой механики, а не пишем пятёрку: если
        /// талант когда-нибудь даст шестое очко, полоска узнает об этом сама.
        /// </summary>
        private void BuildCombo(RectTransform canvasRect, int layer)
        {
            if (comboHeight <= 0.001f) return;

            var row = new GameObject("Combo", typeof(RectTransform), typeof(CanvasGroup));
            row.transform.SetParent(canvasRect, false);
            row.layer = layer;

            comboGroup = row.GetComponent<CanvasGroup>();
            comboGroup.blocksRaycasts = false;
            comboGroup.interactable = false;
            comboGroup.alpha = 0f;

            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = new Vector2(0f, comboHeight);

            int count = combo != null ? combo.MaxPoints : 5;
            if (count <= 0) count = 5;

            comboSegments = new Image[count];

            float step = 1f / count;

            for (int i = 0; i < count; i++)
            {
                var segment = CreateImage("Combo_" + (i + 1), rowRect, backColor);

                segment.anchorMin = new Vector2(i * step, 0f);
                segment.anchorMax = new Vector2((i + 1) * step, 1f);

                // Просвет режем отступами, а не шириной: тогда крайние
                // фрагменты остаются вровень с краями полоски здоровья.
                float half = gap * 0.5f;
                segment.offsetMin = new Vector2(i == 0 ? 0f : half, 0f);
                segment.offsetMax = new Vector2(i == count - 1 ? 0f : -half, 0f);

                comboSegments[i] = segment.GetComponent<Image>();
            }
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnHealthChanged(int current, int max)
        {
            float fraction = max > 0 ? (float)current / max : 0f;

            if (fillRect != null)
                fillRect.localScale = new Vector3(Mathf.Clamp01(fraction), 1f, 1f);

            if (group != null)
            {
                bool visible = current > 0 && (!hideWhenFull || fraction < 0.999f);
                group.alpha = visible ? 1f : 0f;
            }
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            canvas.transform.position = targetable.OverheadPoint;

            // Разворот к камере: копируем её ориентацию целиком, а не смотрим
            // на неё. При ортографической проекции «смотреть на камеру» даёт
            // разный наклон в разных углах экрана, и полоски перекашиваются.
            canvas.transform.rotation = cam.transform.rotation;
        }
    }
}

