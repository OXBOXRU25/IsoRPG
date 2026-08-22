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
        [SerializeField] private float height = 0.13f;

        [SerializeField] private Color fillColor = new Color32(0xC4, 0x3A, 0x3A, 0xFF);
        [SerializeField] private Color backColor = new Color32(0x1A, 0x1A, 0x1A, 0xC0);

        [Tooltip("Прятать полоску при полном здоровье. У нетронутых монстров она только засоряет экран.")]
        [SerializeField] private bool hideWhenFull = true;

        private Targetable targetable;
        private Health health;
        private Canvas canvas;
        private RectTransform fillRect;
        private CanvasGroup group;
        private Camera cam;

        private void Awake()
        {
            targetable = GetComponent<Targetable>();
            health = GetComponent<Health>();
            cam = Camera.main;

            Build();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Changed += OnHealthChanged;
                OnHealthChanged(health.Current, health.Max);
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
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

            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(width, height);

            var back = CreateImage("Back", canvasRect, backColor);
            Stretch(back);

            var fill = CreateImage("Fill", canvasRect, fillColor);
            Stretch(fill);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fillRect = fill;
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

