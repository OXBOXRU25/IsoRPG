using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Audio;

namespace IsoRPG.UI
{
    /// <summary>
    /// Настройки: громкость и напоминание об управлении.
    ///
    /// Список клавиш живёт именно здесь, а не в отдельной справке. Игрок ищет
    /// «как открыть персонажа» ровно тогда, когда полез в настройки, и держать
    /// это в двух разных окнах — лишний повод не найти.
    ///
    /// Значения сохраняются: настройка, слетающая при перезапуске, читается
    /// как сломанная, даже если работала.
    /// </summary>
    public sealed class SettingsWindow : MonoBehaviour, IHudWindow
    {
        private const string MusicKey = "isorpg.volume.music";

        /// <summary>
        /// Общая громкость всего, кроме музыки. Осталась от времён двух
        /// ползунков и теперь работает как общий потолок под каналами.
        /// </summary>
        private const string SfxKey = "isorpg.volume.sfx";

        private const string EffectsKey = "isorpg.volume.effects";
        private const string SystemKey = "isorpg.volume.system";
        private const string AmbienceKey = "isorpg.volume.ambience";

        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF2);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        private static readonly Color TitleColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color LabelColor = new Color32(0xC8, 0xC2, 0xB4, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x84, 0x76, 0xFF);
        private static readonly Color KeyColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);
        private static readonly Color TrackColor = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color FillColor = new Color32(0x8A, 0x6A, 0x3A, 0xFF);
        private static readonly Color HandleColor = new Color32(0xD8, 0xC8, 0xA8, 0xFF);

        /// <summary>Выход из игры: приглушённо-красный, но не кричащий.</summary>
        private static readonly Color DangerColor = new Color32(0x4A, 0x26, 0x22, 0xFF);

        private const float Width = 340f;

        private Font font;
        private GameObject window;
        private RectTransform content;
        private Text musicValue;
        private Text sfxValue;
        private Text ambienceValue;
        private Text systemValue;

        private IHudWindow[] others;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Громкость восстанавливается ДО постройки окна: ползунки должны
            // встать на сохранённые значения, а не на середину.
            Sfx.MasterVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
            Sfx.EffectsVolume = PlayerPrefs.GetFloat(EffectsKey, 1f);
            Sfx.SystemVolume = PlayerPrefs.GetFloat(SystemKey, 0.8f);
            Sfx.AmbienceVolume = PlayerPrefs.GetFloat(AmbienceKey, 0.6f);

            Build();
        }

        private void Start()
        {
            // Соседи ищутся в Start, а не в Awake: к этому моменту все окна
            // на игроке уже созданы.
            var found = GetComponents<MonoBehaviour>();
            var list = new System.Collections.Generic.List<IHudWindow>();

            foreach (var component in found)
                if (component is IHudWindow window && !ReferenceEquals(component, this))
                    list.Add(window);

            others = list.ToArray();

            if (AudioSetup.Instance != null)
                AudioSetup.Instance.MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.22f);

            RefreshValues();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || window == null) return;

            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            if (IsOpen)
            {
                Close();
                return;
            }

            // Esc за всех: окна закрываем сами, а не оставляем это каждому.
            // Пока обработчиков было шесть, порядок их Update решал, что
            // случится за одно нажатие, — а порядок Unity не гарантирует.
            if (CloseOthers()) return;

            Open();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (window == null) return;

            RefreshValues();
            window.SetActive(true);
            Sfx.OpenWindow();
        }

        public void Close()
        {
            if (window == null || !window.activeSelf) return;

            window.SetActive(false);
            Sfx.CloseWindow();

            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Вернуться в главное меню.
        ///
        /// Настройки сохраняем перед уходом: сцена выгружается вместе с этим
        /// окном, и записывать их будет уже некому.
        /// </summary>
        private void ToMainMenu()
        {
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void QuitGame()
        {
            PlayerPrefs.Save();

#if UNITY_EDITOR
            // В редакторе выхода нет: останавливаем проигрывание, иначе
            // кнопка выглядит сломанной при проверке.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Закрывает все открытые окна. True — что-то было открыто, значит
        /// нажатие израсходовано и настройки открывать не надо.
        ///
        /// Закрываем все разом, а не верхнее: окон немного, а человек жмёт
        /// Esc, чтобы убрать интерфейс с экрана, а не разбирать его по слоям.
        /// </summary>
        private bool CloseOthers()
        {
            if (others == null) return false;

            bool closed = false;

            foreach (var other in others)
            {
                if (other == null || !other.IsOpen) continue;

                other.Close();
                closed = true;
            }

            return closed;
        }

        private void RefreshValues()
        {
            if (musicValue != null && AudioSetup.Instance != null)
                LocalizedText.Bind(musicValue, Percent(AudioSetup.Instance.MusicVolume));

            if (sfxValue != null) sfxValue.text = Percent(Sfx.EffectsVolume);
            if (ambienceValue != null) ambienceValue.text = Percent(Sfx.AmbienceVolume);
            if (systemValue != null) systemValue.text = Percent(Sfx.SystemVolume);
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("SettingsHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Выше прочих окон: настройки открываются последними и должны
            // лечь поверх, а не под сумкой.
            canvas.sortingOrder = 20;

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

            var go = new GameObject("SettingsWindow", typeof(Image), typeof(VerticalLayoutGroup),
                                    typeof(ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Width, 0f);

            // Нарисованная рамка вместо плашки. Если картинки нет — остаётся
            // прежняя заливка: окно без фона хуже некрасивого окна.
            bool framed = WindowChrome.ApplyFrame(go);
            if (!framed) go.GetComponent<Image>().color = PanelColor;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 16);
            layout.spacing = 7f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            content = rect;

            // Обводка нужна только плоской плашке: у нарисованной рамки свой
            // контур, и вторая линия вокруг него читается как лишний кант.
            if (!framed)
            {
                var edge = new GameObject("Edge", typeof(Image), typeof(LayoutElement));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(rect, false);
                edgeRect.anchorMin = Vector2.zero;
                edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = new Vector2(-1f, -1f);
                edgeRect.offsetMax = new Vector2(1f, 1f);
                edge.transform.SetAsFirstSibling();
                edge.GetComponent<Image>().color = PanelEdge;
                edge.GetComponent<LayoutElement>().ignoreLayout = true;
            }

            var title = MakeText("Title", "Настройки", 15, TitleColor);
            title.alignment = TextAnchor.MiddleCenter;

            MakeGap(4f);

            musicValue = MakeSlider("Музыка", PlayerPrefs.GetFloat(MusicKey, 0.22f), value =>
            {
                if (AudioSetup.Instance != null) AudioSetup.Instance.MusicVolume = value;
                PlayerPrefs.SetFloat(MusicKey, value);

                if (musicValue != null) musicValue.text = Percent(value);
            });

            ambienceValue = MakeSlider("Окружение", Sfx.AmbienceVolume, value =>
            {
                Sfx.AmbienceVolume = value;
                PlayerPrefs.SetFloat(AmbienceKey, value);

                if (ambienceValue != null) ambienceValue.text = Percent(value);
            });

            sfxValue = MakeSlider("Действия", Sfx.EffectsVolume, value =>
            {
                Sfx.EffectsVolume = value;
                PlayerPrefs.SetFloat(EffectsKey, value);

                if (sfxValue != null) sfxValue.text = Percent(value);
            });

            systemValue = MakeSlider("Интерфейс", Sfx.SystemVolume, value =>
            {
                Sfx.SystemVolume = value;
                PlayerPrefs.SetFloat(SystemKey, value);

                if (systemValue != null) systemValue.text = Percent(value);
            });

            MakeGap(10f);

            // Язык между звуком и управлением: это тоже настройка «как
            // игра со мной разговаривает», и место ей рядом с громкостью,
            // а не в конце списка клавиш.
            var languageTitle = MakeText("LanguageTitle", "Язык", 13, TitleColor);
            languageTitle.alignment = TextAnchor.MiddleLeft;

            MakeGap(4f);

            BuildLanguageRow();

            MakeGap(10f);

            var controls = MakeText("ControlsTitle", "Управление", 13, TitleColor);
            controls.alignment = TextAnchor.MiddleLeft;

            AddKey("Левая кнопка мыши", "идти, выбрать цель, атаковать");
            AddKey("1 — 4", "приёмы");
            AddKey("Пробел", "подпрыгнуть");
            AddKey("I", "сумка");
            AddKey("C", "персонаж");
            AddKey("J", "журнал заданий");
            AddKey("N", "таланты");
            AddKey("Esc", "закрыть окно, настройки");

            MakeGap(8f);

            MakeButton("Закрыть", Close);
            MakeGap(4f);

            // Две разные двери, и путать их нельзя: одна возвращает в меню,
            // другая закрывает игру совсем. Поэтому у выхода из игры свой
            // цвет — тревожный, чтобы палец не промахнулся мимо безобидной.
            MakeButton("В главное меню", ToMainMenu);
            MakeGap(4f);

            MakeButton("Выйти из игры", QuitGame, DangerColor);

            window = go;
            window.SetActive(false);
        }

        private void AddKey(string key, string what)
        {
            var row = MakeRow(17f);

            var keyText = MakeTextIn(row, "Key", key, 12, KeyColor);
            keyText.alignment = TextAnchor.MiddleLeft;
            Stretch((RectTransform)keyText.transform);

            var whatText = MakeTextIn(row, "What", what, 12, DimColor);
            whatText.alignment = TextAnchor.MiddleRight;
            Stretch((RectTransform)whatText.transform);
        }

        /// <summary>Ряд выбора языка внутри окна настроек.</summary>
        private void BuildLanguageRow()
        {
            var host = new GameObject("LanguageRow", typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(content, false);
            rect.sizeDelta = new Vector2(0f, 34f);

            // Высоту объявляем раскладке отдельно.
            //
            // Дети ряда расставлены вручную, поэтому сам по себе он для
            // вертикального списка «пустой» и получает нулевую высоту: ряд
            // наползал на заголовок над собой. По-русски это было почти
            // незаметно, а «Language» с хвостиком у «g» упёрлось в кнопки
            // сразу.
            var element = host.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;
            element.minHeight = 34f;

            var picker = IsoRPG.Localization.LanguagePicker.Attach(
                rect, font, Width - 40f, 28f);

            var pickerRect = (RectTransform)picker.transform;
            pickerRect.anchorMin = new Vector2(0f, 0.5f);
            pickerRect.anchorMax = new Vector2(0f, 0.5f);
            pickerRect.pivot = new Vector2(0f, 0.5f);
            pickerRect.anchoredPosition = Vector2.zero;
        }

        private Text MakeSlider(string label, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            var header = MakeRow(17f);

            var labelText = MakeTextIn(header, "Label", label, 12, LabelColor);
            labelText.alignment = TextAnchor.MiddleLeft;
            Stretch((RectTransform)labelText.transform);

            var valueText = MakeTextIn(header, "Value", Percent(value), 12, DimColor);
            valueText.alignment = TextAnchor.MiddleRight;
            Stretch((RectTransform)valueText.transform);

            var sliderGo = new GameObject("Slider", typeof(Image), typeof(Slider), typeof(LayoutElement));
            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.SetParent(content, false);

            var element = sliderGo.GetComponent<LayoutElement>();
            element.preferredHeight = 14f;
            element.minHeight = 14f;

            sliderGo.GetComponent<Image>().color = TrackColor;

            // Заливка и ползунок собираются руками: готового префаба у нас нет,
            // а Slider без fillRect рисует пустую полосу и выглядит сломанным.
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.SetParent(sliderRect, false);
            Stretch(fillAreaRect);

            var fill = new GameObject("Fill", typeof(Image));
            var fillRect = (RectTransform)fill.transform;
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = FillColor;

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.SetParent(sliderRect, false);
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(5f, 0f);
            handleAreaRect.offsetMax = new Vector2(-5f, 0f);

            var handle = new GameObject("Handle", typeof(Image));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = new Vector2(10f, 18f);
            handle.GetComponent<Image>().color = HandleColor;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChange);

            return valueText;
        }

        private void MakeButton(string caption, UnityEngine.Events.UnityAction onClick)
            => MakeButton(caption, onClick, TrackColor);

        private void MakeButton(string caption, UnityEngine.Events.UnityAction onClick, Color plateColor)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);

            var element = go.GetComponent<LayoutElement>();

            // Не меньше 48 по высоте: это нажимаемая цель, а не строчка текста.
            element.preferredHeight = 34f;
            element.minHeight = 34f;

            go.GetComponent<Image>().color = plateColor;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.highlightedColor = new Color32(0x3A, 0x36, 0x2C, 0xFF);
            colors.pressedColor = FillColor;
            button.colors = colors;

            var text = MakeTextIn(rect, "Label", caption, 13, LabelColor);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch((RectTransform)text.transform);
        }

        private RectTransform MakeRow(float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            return rect;
        }

        private void MakeGap(float height)
        {
            var go = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(content, false);

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Text MakeText(string name, string value, int size, Color color)
        {
            var text = MakeTextIn(content, name, value, size, color);

            var element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = size + 8f;
            element.minHeight = size + 8f;

            return text;
        }

        private Text MakeTextIn(RectTransform parent, string name, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            LocalizedText.Bind(text, value);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
