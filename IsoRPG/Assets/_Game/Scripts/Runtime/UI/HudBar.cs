using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.UI;
using IsoRPG.Items;

namespace IsoRPG.UI
{
    /// <summary>
    /// Ряд кнопок в правом нижнем углу: сумка, персонаж, журнал, настройки.
    ///
    /// Клавиши I, C и J знает тот, кто уже играл в игры жанра. Все остальные
    /// не знают, что окна вообще существуют, — и никакая подсказка на экране
    /// этого не исправит, потому что читать её будут после того, как найдут.
    /// Ряд иконок решает вопрос сам: видно, что окон четыре, и видно, какие.
    ///
    /// Сумка крайняя справа — там, где она была до появления ряда. Привычка
    /// к углу экрана нарабатывается за первые же полчаса, и переставлять
    /// кнопку ради красоты ряда — плохая сделка.
    /// </summary>
    public sealed class HudBar : MonoBehaviour
    {
        private static readonly Color HintColor = new Color32(0xD8, 0xC8, 0xA8, 0xE0);
        private static readonly Color HintShadow = new Color32(0x00, 0x00, 0x00, 0xC0);
        private static readonly Color Idle = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color Hover = new Color32(0xFF, 0xF0, 0xD0, 0xFF);
        private static readonly Color Pressed = new Color32(0xC0, 0xB0, 0x98, 0xFF);
        private static readonly Color Fallback = new Color32(0x2A, 0x27, 0x21, 0xFF);

        /// <summary>
        /// 48 пикселей — минимальная цель для пальца и с запасом для мыши.
        /// Иконка занимает кнопку целиком: поле вокруг рисунка съедает цель,
        /// ничего не добавляя.
        /// </summary>
        private const float ButtonSize = 48f;

        private const float Gap = 6f;
        private const float Margin = 18f;
        private const float BottomOffset = 28f;

        [Tooltip("Иконки кнопок нижнего ряда.")]
        [SerializeField] private Sprite bagIcon;
        [SerializeField] private Sprite characterIcon;
        [SerializeField] private Sprite journalIcon;
        [SerializeField] private Sprite settingsIcon;
        [SerializeField] private Sprite talentsIcon;
        [SerializeField] private Sprite mapIcon;
        [SerializeField] private Sprite guildIcon;

        private Font font;

        public void SetupIcons(Sprite bag, Sprite character, Sprite journal,
                               Sprite talents, Sprite settings,
                               Sprite map, Sprite guild)
        {
            bagIcon = bag;
            characterIcon = character;
            journalIcon = journal;
            talentsIcon = talents;
            settingsIcon = settings;
            mapIcon = map;
            guildIcon = guild;
        }

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Start, а не Awake: кнопки дёргают окна, и к этому моменту все
            // окна на игроке уже построены.
            Build();
        }

        private void Build()
        {
            var canvasGo = new GameObject("HudBar",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Под окнами: открытая сумка перекрывает свою же кнопку, и это
            // правильно — иначе кнопка торчала бы поверх содержимого.
            canvas.sortingOrder = 9;

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

            var inventory = GetComponent<InventoryHud>();
            var character = GetComponent<CharacterHud>();
            var journal = GetComponent<QuestJournal>();
            var settings = GetComponent<SettingsWindow>();
            var talents = GetComponent<TalentWindow>();

            // Порядок справа налево, выбран Павлоном 03.09.2026:
            // сумка, настройки, карта мира, квесты, гильдия, таланты,
            // персонаж. Нулевой слот — самый правый, у угла экрана.
            int slot = 0;

            MakeButton(root, slot++, bagIcon, "I", "Сумка",
                "Добыча, снаряжение и золото", () => { if (inventory != null) inventory.Toggle(); });

            MakeButton(root, slot++, settingsIcon, "Esc", "Настройки",
                "Громкость и управление", () => { if (settings != null) settings.Toggle(); });

            // Карта и гильдия — кнопки есть, окон за ними пока нет.
            //
            // Ставим их сейчас, потому что порядок ряда — решение о виде, и
            // менять его потом означало бы переучивать руку. Нажатие честно
            // говорит, что раздела ещё нет: молчащая кнопка читается как
            // поломка, а не как «пока не сделано».
            MakeButton(root, slot++, mapIcon, "M", "Карта мира",
                "Пока не готова", () => IsoRPG.Combat.CombatLog.Add(
                    Loc.T("Карта мира ещё не готова."), IsoRPG.Combat.LogKind.System));

            MakeButton(root, slot++, journalIcon, "J", "Журнал заданий",
                "Взятые задания и награды", () => { if (journal != null) journal.Toggle(); });

            MakeButton(root, slot++, guildIcon, "G", "Гильдия",
                "Пока не готова", () => IsoRPG.Combat.CombatLog.Add(
                    Loc.T("Гильдии ещё не готовы."), IsoRPG.Combat.LogKind.System));

            MakeButton(root, slot++, talentsIcon, "N", "Таланты",
                "Три ветки, очко за уровень", () => { if (talents != null) talents.Toggle(); });

            MakeButton(root, slot++, characterIcon, "C", "Персонаж",
                "Надетые вещи и характеристики", () => { if (character != null) character.Toggle(); });
        }

        private void MakeButton(RectTransform root, int slot, Sprite icon, string key,
                                string caption, string hint, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(caption, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(
                -(Margin + slot * (ButtonSize + Gap)), BottomOffset);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            var image = go.GetComponent<Image>();

            // Иконки идут без подложки — Павлон нарисовал новый набор
            // 03.09.2026 прозрачными PNG. Отдельная плашка под ними не
            // заводится нарочно: рамка вокруг каждой кнопки спорила бы с
            // рисунком и утяжеляла угол экрана.
            //
            // Если иконки нет — рисуем хотя бы квадрат, чтобы кнопка не
            // превратилась в пустоту.
            if (icon != null)
            {
                image.sprite = icon;
                image.color = Idle;
                image.preserveAspect = true;
            }
            else image.color = Fallback;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = icon != null ? Idle : Fallback;
            colors.highlightedColor = Hover;
            colors.pressedColor = Pressed;
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var tooltip = go.AddComponent<TextTooltipTrigger>();
            // Переводим название до склейки с клавишей: «Настройки   [Esc]»
            // целиком в словаре искать бессмысленно — таких строк было бы
            // столько же, сколько кнопок, и каждая со своей скобкой.
            tooltip.Setup(Loc.T(caption) + "   [" + key + "]", hint);

            // Буквы клавиш под иконками сняты 03.09.2026 по решению Павлона:
            // на новых иконках без подложки они читались как грязь под
            // рисунком. Клавиша осталась во всплывающей подсказке — там она
            // и нужна, когда её ищут.
        }

        /// <summary>
        /// Буква клавиши в углу кнопки. С тенью: подсказка ложится прямо на
        /// рисунок, и на светлом месте иконки светлая буква пропадает.
        /// </summary>
        private void AddKeyHint(RectTransform parent, string key)
        {
            var shadow = MakeHintText(parent, "KeyShadow", key, HintShadow);
            ((RectTransform)shadow.transform).anchoredPosition = new Vector2(4f, 2f);

            var text = MakeHintText(parent, "Key", key, HintColor);
            ((RectTransform)text.transform).anchoredPosition = new Vector2(3f, 3f);
        }

        private Text MakeHintText(RectTransform parent, string name, string value, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(26f, 12f);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = 10;
            text.color = color;
            LocalizedText.Bind(text, value);
            text.raycastTarget = false;
            text.alignment = TextAnchor.LowerLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
