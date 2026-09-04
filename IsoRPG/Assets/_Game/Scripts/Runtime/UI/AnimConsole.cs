using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IsoRPG.UI
{
    /// <summary>
    /// Консоль анимаций: окно со списком всех клипов и показом на манекене.
    ///
    /// Просьба Павла 04.09.2026: «нам нужно что-то типа консоли разработчика,
    /// окно, в котором я могу выбирать и листать все анимации, смотря их на
    /// манекене». До этого варианты выводились по клавишам — и это работало,
    /// пока их было восемнадцать. Когда выяснилось, что в проекте их больше
    /// тысячи, клавиши перестали быть способом смотреть.
    ///
    /// Инструмент разработчика, а не игровая механика: он знает про манекен,
    /// подменяет ему контроллер и живёт только в рабочей сборке.
    ///
    /// Список рисуется НЕ целиком: в кадре два десятка строк, остальное
    /// прокручивается сдвигом окна по массиву. Тысяча живых кнопок съела бы
    /// кадр на ровном месте, а видно всё равно двадцать.
    /// </summary>
    public sealed class AnimConsole : MonoBehaviour
    {
        [Tooltip("Все клипы, какие собрало задание anim-console.")]
        [SerializeField] private AnimationClip[] clips = new AnimationClip[0];

        /// <summary>
        /// Имена клипов, которые сейчас РЕАЛЬНО стоят в дереве хода.
        ///
        /// Заполняет сборщик консоли тем же списком, по которому строится
        /// дерево. Нужно вкладке «В деле»: во вкладке по слову набирается
        /// шесть десятков похожих клипов, и понять, какой из них играет в
        /// игре, по имени нельзя.
        /// </summary>
        [SerializeField] private string[] inUse = new string[0];

        [Tooltip("Контроллер-заготовка с одним состоянием: в него и подставляем клип.")]
        [SerializeField] private RuntimeAnimatorController previewController;

        /// <summary>Сколько строк видно разом.</summary>
        private const int Rows = 22;

        private const float Width = 560f;
        private const float RowHeight = 22f;

        private GameObject window;
        private Text[] labels;
        private InputField search;
        private Text status;

        private readonly List<int> shown = new List<int>();
        private int top;
        private int picked = -1;

        private Animator dummy;
        private AnimatorOverrideController over;
        private AnimationClip slot;

        public void Setup(AnimationClip[] all, RuntimeAnimatorController preview, string[] used = null)
        {
            clips = all;
            previewController = preview;
            inUse = used ?? new string[0];
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;

            if (keys.f9Key.wasPressedThisFrame) Toggle();

            if (window == null || !window.activeSelf) return;

            // Колесо листает список. Мышью по строкам — выбор, но колесо
            // привычнее: список длинный, а рука уже на мыши.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f)) Scroll(scroll > 0f ? -3 : 3);
            }

            if (keys.pageUpKey.wasPressedThisFrame) Scroll(-Rows);
            if (keys.pageDownKey.wasPressedThisFrame) Scroll(Rows);
        }

        public void Toggle()
        {
            if (window == null) Build();

            bool on = !window.activeSelf;
            window.SetActive(on);

            if (on) Refilter();
        }

        private void Scroll(int by)
        {
            top = Mathf.Clamp(top + by, 0, Mathf.Max(0, shown.Count - Rows));
            Redraw();
        }

        /// <summary>
        /// Разделы-вкладки. Просьба Павла 04.09.2026: «сделаем внутреннюю
        /// сортировку по разделам, типа вкладок — додж, кик и тому подобное».
        ///
        /// Слова, а не папки: одно и то же движение лежит у трёх наборов в
        /// трёх разных местах, зато называется всюду похоже. Отбор по имени
        /// собирает их вместе, а отбор по папке развёл бы обратно.
        /// </summary>
        private static readonly (string title, string[] words)[] Tabs =
        {
            ("Всё",        new string[0]),
            ("Удары",      new[] { "attack" }),
            ("Пинки",      new[] { "kick" }),
            ("Уклонения",  new[] { "dodge", "roll" }),
            ("Стойки",     new[] { "idle" }),
            ("Ход",        new[] { "walk", "run", "sprint" }),

            // Стороны — просьба Павла 04.09.2026 после того, как нашлось
            // кольцо направлений Synty: «выведи в консоль отдельную вкладку
            // со всеми этими анимациями, чтобы я посмотрел». Сюда попадает
            // всё, чем герой двигается не прямо вперёд: восемь сторон хода и
            // бега, повороты на месте и мелкие переступания.
            ("Стороны",    new[] { "strafe", "turn_standing", "shuffle" }),

            // «В деле» — не по слову, а по списку: ровно те клипы, что стоят
            // в дереве хода. Павлон 04.09.2026: «в сторонах куча анимаций
            // перемешалось, не знаю какие смотреть». Пустой массив здесь
            // означает особый отбор, см. Refilter.
            ("В деле",     null),
            ("Ножны",      new[] { "sheath", "switch" }),
            ("Прыжок",     new[] { "jump", "fall", "air" }),
            ("Реакции",    new[] { "hit", "block", "stun", "knock" }),
            ("Смерть",     new[] { "death", "dead", "getup", "revive" }),
        };

        private int tab;

        /// <summary>Отобрать по вкладке и строке поиска.</summary>
        private void Refilter()
        {
            string mask = search != null ? search.text.Trim().ToLowerInvariant() : "";
            var words = Tabs[Mathf.Clamp(tab, 0, Tabs.Length - 1)].words;

            shown.Clear();

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;

                string name = clips[i].name.ToLowerInvariant();

                if (mask.Length > 0 && !name.Contains(mask)) continue;

                // Вкладка «В деле»: отбор по списку, а не по слову.
                if (words == null)
                {
                    bool used = false;

                    for (int u = 0; u < inUse.Length; u++)
                        if (string.Equals(inUse[u], clips[i].name, System.StringComparison.OrdinalIgnoreCase))
                        { used = true; break; }

                    if (!used) continue;

                    shown.Add(i);
                    continue;
                }

                if (words.Length > 0)
                {
                    bool fits = false;
                    foreach (string word in words)
                        if (name.Contains(word)) { fits = true; break; }

                    if (!fits) continue;
                }

                shown.Add(i);
            }

            top = 0;
            Redraw();
        }

        private void Redraw()
        {
            if (labels == null) return;

            for (int r = 0; r < labels.Length; r++)
            {
                int at = top + r;

                if (at >= shown.Count)
                {
                    labels[r].text = "";
                    continue;
                }

                var clip = clips[shown[at]];

                labels[r].text = (shown[at] == picked ? "► " : "   ") +
                                 clip.name + "   " + clip.length.ToString("0.00") + " с";

                labels[r].color = shown[at] == picked
                    ? new Color32(0xFF, 0xD9, 0x8A, 0xFF)
                    : new Color32(0xC8, 0xC2, 0xB0, 0xFF);
            }

            if (status != null)
                status.text = $"Найдено {shown.Count} из {clips.Length}. " +
                              "Колесо и PageUp/Down — листать, клик — показать на манекене.";
        }

        /// <summary>
        /// Показать клип на манекене.
        ///
        /// Через подмену в контроллере-заготовке: у него одно состояние, и
        /// клип в нём меняется как угодно часто. Трогать контроллер героя
        /// нельзя — он боевой, и подмена в нём означала бы, что смотришь ты
        /// одно, а играет игра другое.
        /// </summary>
        private void Show(int index)
        {
            picked = index;
            Redraw();

            if (dummy == null)
            {
                var target = GameObject.Find("Манекен");
                if (target == null)
                {
                    Say("Манекена нет в сцене — показывать не на ком.");
                    return;
                }

                dummy = target.GetComponentInChildren<Animator>(true);

                if (dummy == null)
                {
                    Say("У манекена нет аниматора.");
                    return;
                }

                if (previewController == null)
                {
                    Say("Нет контроллера показа — прогони задание anim-console.");
                    return;
                }

                over = new AnimatorOverrideController(previewController);

                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                over.GetOverrides(pairs);

                if (pairs.Count == 0)
                {
                    Say("В контроллере показа нет клипа-затычки — подменять нечего.");
                    return;
                }

                slot = pairs[0].Key;
                dummy.runtimeAnimatorController = over;

                // Модель манекена анимируется, только когда её видит камера;
                // окно консоли может её загораживать, а смотреть надо всегда.
                dummy.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                dummy.applyRootMotion = false;
            }

            if (over == null || slot == null) return;

            over[slot] = clips[index];

            dummy.Play("Preview", 0, 0f);
            dummy.Update(0f);

            Say($"Играю «{clips[index].name}» ({clips[index].length:0.00} с) на манекене.");
        }

        private void Say(string text)
        {
            if (status != null) status.text = text;
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("AnimConsole",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;

            window = new GameObject("Окно", typeof(Image));
            var rect = (RectTransform)window.transform;
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(Width, Rows * RowHeight + 122f);

            window.GetComponent<Image>().color = new Color32(0x14, 0x16, 0x12, 0xF2);

            search = MakeSearch(rect);
            MakeTabs(rect);
            labels = new Text[Rows];

            for (int r = 0; r < Rows; r++)
            {
                var row = MakeRow(rect, r);
                labels[r] = row;
            }

            status = MakeText(rect, new Vector2(10f, -(Rows * RowHeight + 98f)),
                              new Vector2(Width - 20f, 20f), 11);

            status.color = new Color32(0x8A, 0x86, 0x76, 0xFF);
        }

        private InputField MakeSearch(RectTransform parent)
        {
            var go = new GameObject("Поиск", typeof(Image), typeof(InputField));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.sizeDelta = new Vector2(Width - 20f, 24f);

            go.GetComponent<Image>().color = new Color32(0x24, 0x26, 0x20, 0xFF);

            var text = MakeText(rect, new Vector2(6f, -3f), new Vector2(Width - 40f, 18f), 13);
            text.alignment = TextAnchor.MiddleLeft;

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.onValueChanged.AddListener(_ => Refilter());

            return field;
        }

        /// <summary>
        /// Ряд вкладок под строкой поиска.
        ///
        /// Двумя рядами: десять названий в одну строку не влезают, а резать
        /// их до трёх букв — значит заставить читать ребусы.
        /// </summary>
        private void MakeTabs(RectTransform parent)
        {
            tabLabels = new Text[Tabs.Length];

            // Шесть в ряд, а не пять: с одиннадцатой вкладкой третий ряд
            // наехал бы на список — он начинается с −88, а ряд лёг бы на −84.
            const float w = 88f;
            const float h = 20f;
            const int perRow = 6;

            for (int i = 0; i < Tabs.Length; i++)
            {
                int row = i / perRow;
                int col = i % perRow;

                var go = new GameObject("Вкладка" + i, typeof(Image), typeof(Button));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(10f + col * (w + 2f), -(40f + row * (h + 2f)));
                rect.sizeDelta = new Vector2(w, h);

                go.GetComponent<Image>().color = new Color32(0x24, 0x26, 0x20, 0xFF);

                int at = i;
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    tab = at;
                    Refilter();
                    PaintTabs();
                });

                var text = MakeText(rect, Vector2.zero, new Vector2(w, h), 11);
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.text = Tabs[i].title;

                tabLabels[i] = text;
            }

            PaintTabs();
        }

        private Text[] tabLabels;

        private void PaintTabs()
        {
            if (tabLabels == null) return;

            for (int i = 0; i < tabLabels.Length; i++)
                tabLabels[i].color = i == tab
                    ? new Color32(0xFF, 0xD9, 0x8A, 0xFF)
                    : new Color32(0x9A, 0x96, 0x86, 0xFF);
        }

        private Text MakeRow(RectTransform parent, int index)
        {
            var go = new GameObject("Строка" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -(88f + index * RowHeight));
            rect.sizeDelta = new Vector2(Width - 20f, RowHeight);

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            int row = index;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                int at = top + row;
                if (at < shown.Count) Show(shown[at]);
            });

            var text = MakeText(rect, new Vector2(4f, 0f), new Vector2(Width - 28f, RowHeight), 12);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            return text;
        }

        private Text MakeText(RectTransform parent, Vector2 at, Vector2 size, int fontSize)
        {
            var go = new GameObject("Текст", typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color32(0xC8, 0xC2, 0xB0, 0xFF);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            return text;
        }
    }
}
