using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Подсказка о способности при наведении.
    ///
    /// Подписи под кнопками убраны намеренно: шесть названий мелким кеглем
    /// читаются как забор и мешают именно тогда, когда идёт бой. А нужны они
    /// ровно один раз — когда игрок разбирается, что это за приём. Наведение
    /// и есть тот момент.
    ///
    /// Появляется мгновенно, без задержки: задержка уместна там, где курсор
    /// часто проходит над элементами транзитом. Панель способностей внизу
    /// экрана, случайно по ней не водят.
    /// </summary>
    public sealed class AbilityTooltip : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color32(0x16, 0x14, 0x11, 0xF2);
        private static readonly Color EdgeColor = new Color32(0x3A, 0x36, 0x2C, 0xB0);
        private static readonly Color TitleColor = new Color32(0xF0, 0xE4, 0xC8, 0xFF);
        private static readonly Color BodyColor = new Color32(0xB4, 0xAE, 0xA0, 0xFF);
        private static readonly Color CostColor = new Color32(0x7A, 0xB8, 0xE0, 0xFF);

        private const float Width = 250f;

        private Font font;
        private RectTransform panel;
        private Text title;
        private Text body;
        private Text cost;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        public void Show(AbilityDefinition ability, Vector2 screenPoint)
        {
            if (ability == null || panel == null) return;

            title.text = ability.displayName;
            body.text = ability.description;

            // Цена и откат в одну строку: это числа, которые игрок сравнивает
            // между приёмами, и рядом их сравнивать проще, чем в столбик.
            string line = ability.energyCost + " энергии";
            if (ability.cooldown > 0.01f) line += "   откат " + ability.cooldown.ToString("0.#") + " с";
            cost.text = line;

            panel.gameObject.SetActive(true);

            // Подсказка всплывает НАД курсором: панель способностей у нижнего
            // края, и вниз ей расти некуда.
            panel.position = new Vector3(screenPoint.x, screenPoint.y + 26f, 0f);

            // Прижимаем к экрану, чтобы крайние кнопки не выталкивали
            // подсказку за край.
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);

            float overflowRight = corners[2].x - Screen.width;
            if (overflowRight > 0f) panel.position -= new Vector3(overflowRight + 8f, 0f, 0f);

            float overflowLeft = -corners[0].x;
            if (overflowLeft > 0f) panel.position += new Vector3(overflowLeft + 8f, 0f, 0f);
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Поверх всего: подсказка перекрывает интерфейс по определению,
            // иначе она бесполезна.
            canvas.sortingOrder = 40;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Tooltip", typeof(Image), typeof(VerticalLayoutGroup),
                                    typeof(ContentSizeFitter));
            panel = (RectTransform)go.transform;
            panel.SetParent((RectTransform)canvasGo.transform, false);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(Width, 0f);

            go.GetComponent<Image>().color = PanelColor;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            // Высота по содержимому: у приёмов описания разной длины, и
            // фиксированная высота оставляла бы то пустоту, то обрезку.
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var edge = new GameObject("Edge", typeof(Image));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(panel, false);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = new Vector2(-1f, -1f);
            edgeRect.offsetMax = new Vector2(1f, 1f);
            edge.transform.SetAsFirstSibling();
            edge.GetComponent<Image>().color = EdgeColor;
            edge.GetComponent<Image>().raycastTarget = false;

            // Рамка не участвует в раскладке: иначе она растянула бы панель.
            var edgeLayout = edge.AddComponent<LayoutElement>();
            edgeLayout.ignoreLayout = true;

            title = MakeLine("Title", 14, TitleColor);
            cost = MakeLine("Cost", 11, CostColor);
            body = MakeLine("Body", 12, BodyColor);

            panel.gameObject.SetActive(false);
        }

        private Text MakeLine(string name, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(panel, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }

    /// <summary>
    /// Ловит наведение на кнопку способности и просит подсказку показаться.
    ///
    /// Отдельным компонентом на самой кнопке, а не общим опросом мыши:
    /// система событий Unity уже знает, над чем курсор, и делает это точнее
    /// любой самодельной проверки — с учётом перекрытий и порядка слоёв.
    /// </summary>
    public sealed class AbilityTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private AbilityDefinition ability;
        private CombatHud hud;

        public void Setup(AbilityDefinition definition, CombatHud owner)
        {
            ability = definition;
            hud = owner;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hud != null) hud.ShowAbilityTooltip(ability, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hud != null) hud.HideAbilityTooltip();
        }
    }
}
