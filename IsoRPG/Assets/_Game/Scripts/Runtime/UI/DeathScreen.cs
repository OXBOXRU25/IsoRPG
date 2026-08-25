using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.UI;
using IsoRPG.Combat;

namespace IsoRPG.UI
{
    /// <summary>
    /// Экран смерти: затемнение, надгробие и кнопка «Возродиться».
    ///
    /// Возрождение по кнопке, а не по таймеру. Разница не в удобстве: пока
    /// игрок сам не нажал, он остаётся лежать и смотреть на то место, где его
    /// убили, — и успевает подумать, что пошло не так. Автоматический подъём
    /// через три секунды такую паузу съедает, и смерть перестаёт что-либо
    /// значить.
    ///
    /// Штрафа за смерть у нас пока нет. Когда появится, он будет здесь же —
    /// на экране, где игрок его прочтёт, а не тихо в логе.
    /// </summary>
    public sealed class DeathScreen : MonoBehaviour
    {
        private static readonly Color Veil = new Color(0.02f, 0.01f, 0.01f, 0.72f);
        private static readonly Color TitleColor = new Color32(0xD8, 0x5A, 0x4A, 0xFF);
        private static readonly Color HintColor = new Color32(0x9A, 0x94, 0x86, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x3A, 0x32, 0x24, 0xFF);
        private static readonly Color ButtonHover = new Color32(0x5A, 0x4A, 0x30, 0xFF);
        private static readonly Color ButtonText = new Color32(0xE8, 0xE2, 0xD4, 0xFF);

        private const float ImageSize = 300f;

        [Tooltip("Надгробие. Пусто — экран останется без картинки, но рабочим.")]
        [SerializeField] private Sprite artwork;

        private Health health;
        private Respawner respawner;
        private GameObject screen;
        private Font font;

        public void SetupArt(Sprite sprite) => artwork = sprite;

        private void Awake()
        {
            health = GetComponent<Health>();
            respawner = GetComponent<Respawner>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(GameObject killer)
        {
            if (screen != null) screen.SetActive(true);
        }

        private void Revive()
        {
            if (screen != null) screen.SetActive(false);

            // Всю работу делает возрождатель: он же поднимает монстров, и
            // держать вторую копию той же логики для игрока — верный способ
            // однажды забыть включить обратно один компонент из десяти.
            if (respawner != null) respawner.Revive();
            else if (health != null) health.Revive();
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("DeathHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Выше всего, включая настройки: пока герой мёртв, остальной
            // интерфейс не должен спорить за внимание.
            canvas.sortingOrder = 60;

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

            var veilGo = new GameObject("Veil", typeof(Image));
            var veilRect = (RectTransform)veilGo.transform;
            veilRect.SetParent((RectTransform)canvasGo.transform, false);
            veilRect.anchorMin = Vector2.zero;
            veilRect.anchorMax = Vector2.one;
            veilRect.offsetMin = Vector2.zero;
            veilRect.offsetMax = Vector2.zero;

            // Затемнение ловит клики: сквозь него не должно быть видно ни
            // кнопок интерфейса, ни земли под курсором.
            veilGo.GetComponent<Image>().color = Veil;

            if (artwork != null)
            {
                var artGo = new GameObject("Art", typeof(Image));
                var artRect = (RectTransform)artGo.transform;
                artRect.SetParent(veilRect, false);
                artRect.anchorMin = new Vector2(0.5f, 0.5f);
                artRect.anchorMax = new Vector2(0.5f, 0.5f);
                artRect.pivot = new Vector2(0.5f, 0.5f);
                artRect.anchoredPosition = new Vector2(0f, 60f);
                artRect.sizeDelta = new Vector2(ImageSize, ImageSize);

                var art = artGo.GetComponent<Image>();
                art.sprite = artwork;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }

            var title = MakeText(veilRect, "Title", "Вы погибли", 34, TitleColor);
            Place(title, new Vector2(0f, -120f), new Vector2(600f, 44f));

            var hint = MakeText(veilRect, "Hint", "Противники вернулись на свои места", 13, HintColor);
            Place(hint, new Vector2(0f, -160f), new Vector2(600f, 20f));

            BuildButton(veilRect);

            screen = veilGo;
            screen.SetActive(false);
        }

        private void BuildButton(RectTransform parent)
        {
            var go = new GameObject("Revive", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -215f);

            // Крупная цель: это единственная кнопка на экране, и промахнуться
            // по ней в момент, когда игрок раздражён смертью, — лишнее.
            rect.sizeDelta = new Vector2(220f, 52f);

            var plate = go.GetComponent<Image>();
            plate.color = ButtonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(Revive);

            var colors = button.colors;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = MakeText(rect, "Label", "Возродиться", 17, ButtonText);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
        }

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = TextAnchor.MiddleCenter;
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
