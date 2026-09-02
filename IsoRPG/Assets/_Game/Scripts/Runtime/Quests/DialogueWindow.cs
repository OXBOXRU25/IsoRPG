using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.UI;

namespace IsoRPG.Quests
{
    /// <summary>
    /// Окно разговора с NPC: текст и кнопки выбора.
    ///
    /// Выбор здесь важнее текста. «Взять» и «Отказаться» — первое решение,
    /// которое игра предлагает игроку словами, а не кликом по врагу; без него
    /// квест выдавался бы автоматически и перестал быть согласием на сделку.
    /// </summary>
    public sealed class DialogueWindow : MonoBehaviour, IsoRPG.UI.IHudWindow
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xE0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0x8A);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        /// <summary>Заголовки разделов. В образце они выделены цветом, а не только кеглем.</summary>
        private static readonly Color TitleColor = new Color32(0xE8, 0xC8, 0x7A, 0xFF);
        /// <summary>Пояснение под заголовком награды — тише основного текста.</summary>
        private static readonly Color HintColor = new Color32(0xAE, 0xA6, 0x92, 0xFF);
        private static readonly Color AcceptColor = new Color32(0x4A, 0x7A, 0x4A, 0xFF);
        private static readonly Color DeclineColor = new Color32(0x5A, 0x44, 0x40, 0xFF);

        // Размеры сняты с окна квеста WoW 01.09.2026 по просьбе Павла: там
        // 478 x 590 при экране 1920 x 1200, то есть четверть ширины экрана и
        // половина высоты. Наше было 460 x 430 — почти квадрат, и оттого
        // текст лип к кнопкам, а разделов в нём не читалось вовсе.
        private const float Width = 480f;
        private const float Pad = 18f;
        /// <summary>Сторона портрета собеседника в окне разговора, точек.</summary>
        private const float FaceSize = 56f;
        private const float ButtonHeight = 30f;

        /// <summary>Половина высоты экрана при нашем эталоне 1920 x 1080 — та же доля, что у образца.</summary>
        private const float WindowHeight = 540f;

        private const float ButtonsArea = (ButtonHeight + 6f) * 3f;

        private Font font;
        private GameObject window;
        private Image face;
        private Text title;
        private Text body;
        private RectTransform content;
        private Text questTitle;
        private Text objectivesHeader;
        private Text objectives;
        private Text rewardsHeader;
        private Text rewardsHint;
        private RectTransform buttons;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private QuestGiver current;
        private QuestLog log;

        public bool IsOpen => window != null && window.activeSelf;

        /// <summary>
        /// Разговор начался и кончился. Слушает жестикуляция НПС
        /// (<see cref="IsoRPG.World.NpcGesture"/>): собеседник должен размахивать
        /// руками, пока с ним говорят, а не стоять столбом.
        ///
        /// Статическое, потому что окно на клиенте одно. Иначе каждому НПС
        /// пришлось бы самому мерить расстояние до игрока в кадре — а это ММО,
        /// и такого мы себе не позволяем.
        /// </summary>
        public static event System.Action<QuestGiver> Started;

        public static event System.Action<QuestGiver> Ended;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Esc обрабатывает SettingsWindow за всех: шесть независимых
            // обработчиков в одном кадре спорили за одно нажатие.

            // Отошёл от собеседника — разговор кончился. Иначе окно висит
            // через полкарты, и сдать квест можно было бы издалека.
            if (current == null ||
                Vector3.Distance(transform.position, current.transform.position) > current.TalkRange + 1f)
                Close();
        }

        public void Open(QuestGiver giver)
        {
            // Старое «бормотание» убрано 01.09.2026: у НПС теперь настоящий
            // голос с фразами (NpcVoice), и абстрактный звук поверх него
            // звучал как помеха. Павлон: «его убираем вообще».

            if (giver == null) return;

            current = giver;
            window.SetActive(true);

            Started?.Invoke(giver);

            IsoRPG.Audio.Sfx.OpenWindow();
            Refresh();
        }

        public void Close()
        {
            var was = current;
            current = null;

            if (was != null) Ended?.Invoke(was);
            if (window != null) window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Refresh()
        {
            if (current == null) return;

            var quest = current.Quest;

            // Шапка — собеседник, а не задание: в образце вверху стоит имя
            // того, с кем говоришь.
            LocalizedText.Bind(title, current.DisplayName);

            // Лицо собеседника. Сначала своё, назначенное этому существу,
            // потом общее по имени — тот же порядок, что в окне цели.
            var own = current.GetComponent<IsoRPG.Combat.Targetable>();

            var art = (own != null ? own.Portrait : null)
                      ?? IsoRPG.Combat.Portraits.For(current.DisplayName);

            face.sprite = art;
            face.enabled = art != null;

            bool offering = current.State == QuestState.Available;
            bool turningIn = current.State == QuestState.ReadyToTurnIn;
            bool aboutQuest = quest != null && (offering || turningIn || current.State == QuestState.Active);

            // Название задания — верхним регистром, как в образце: оно там
            // работает не украшением, а разделителем шапки и текста.
            Fill(questTitle, aboutQuest ? quest.title.ToUpperInvariant() : null);
            Fill(body, current.CurrentText());

            // Цели: пока задание в работе или предлагается. После сдачи их
            // показывать незачем — там уже речь про награду.
            Fill(objectivesHeader, aboutQuest && !turningIn ? "ЦЕЛИ ЗАДАНИЯ" : null);
            Fill(objectives, aboutQuest && !turningIn ? quest.ObjectiveLine(ProgressOf(quest)) : null);

            bool hasChoice = quest != null && quest.rewardChoices != null && quest.rewardChoices.Length > 0;

            // Награды показываем сразу, а не только в журнале: игрок решает,
            // браться ли за работу, по тому, что за неё дают.
            Fill(rewardsHeader, aboutQuest ? "НАГРАДЫ" : null);
            Fill(rewardsHint, !aboutQuest ? null
                              : hasChoice ? "Вы сможете выбрать одну из наград:"
                              : Rewards(quest, current.State).TrimStart('\n'));

            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            switch (current.State)
            {
                case QuestState.Available:
                    AddButton(0, "Взять", AcceptColor, () =>
                    {
                        current.Accept();
                        Close();
                    });
                    AddButton(1, "Отказаться", DeclineColor, Close);
                    break;

                case QuestState.ReadyToTurnIn:
                {
                    var choices = quest != null ? quest.rewardChoices : null;

                    if (choices != null && choices.Length > 0)
                    {
                        // Награда на выбор: по кнопке на каждую вещь, как в
                        // WoW. Одна кнопка «Отдать» тут не годится — игрок
                        // должен нажать именно на ту вещь, которую берёт, и
                        // видеть её название в момент нажатия.
                        // Снизу вверх: «Позже» внизу, награды над ней —
                        // чтобы случайный клик по нижней кнопке не забирал
                        // вещь, которую игрок ещё выбирает.
                        AddButton(0, "Позже", DeclineColor, Close, true);

                        for (int i = 0; i < choices.Length && i < 3; i++)
                        {
                            var option = choices[i];
                            if (option == null) continue;

                            AddButton(i + 1, "Взять: " + option.displayName, AcceptColor, () =>
                            {
                                current.TurnIn(option);
                                Close();
                            }, true);
                        }

                        break;
                    }

                    AddButton(0, "Отдать", AcceptColor, () =>
                    {
                        current.TurnIn();
                        Close();
                    });
                    AddButton(1, "Позже", DeclineColor, Close);
                    break;
                }

                default:
                    AddButton(0, "Закрыть", DeclineColor, Close);
                    break;
            }
        }

        /// <summary>
        /// Строка наград под текстом квеста.
        ///
        /// Показывается и при взятии, и при сдаче: в первом случае это
        /// обещание, во втором — напоминание, из чего выбирать. Пока квест в
        /// работе награды не повторяем — там важна только цель.
        /// </summary>
        private static string Rewards(QuestDefinition quest, QuestState state)
        {
            if (quest == null) return string.Empty;
            if (state != QuestState.Available && state != QuestState.ReadyToTurnIn) return string.Empty;

            var lines = new System.Text.StringBuilder();

            if (quest.rewardChoices != null && quest.rewardChoices.Length > 0)
            {
                lines.Append("\n\nНаграда на выбор:");

                foreach (var option in quest.rewardChoices)
                {
                    if (option == null) continue;

                    lines.Append("\n  • ").Append(Loc.T(option.displayName));

                    // Броня — то, ради чего вещь и берут. Без неё выбор
                    // делается вслепую, по одному названию.
                    if (option.armor > 0) lines.Append(", броня ").Append(option.armor);
                }
            }

            if (quest.rewardItem != null)
                lines.Append("\n\nПолучите: ").Append(Loc.T(quest.rewardItem.displayName));

            var extra = new List<string>();
            if (quest.rewardExperience > 0) extra.Add(quest.rewardExperience + " опыта");
            if (quest.rewardGold > 0) extra.Add(quest.rewardGold + " золота");

            if (extra.Count > 0)
                lines.Append("\n\nТакже: ").Append(string.Join(", ", extra));

            return lines.ToString();
        }

        private void AddButton(int index, string label, Color color, System.Action onClick)
            => AddButton(index, label, color, onClick, false);

        /// <summary>
        /// Кнопка внизу окна.
        /// </summary>
        /// <param name="stacked">
        /// Столбиком вверх, во всю ширину. Нужно для наград на выбор:
        /// «Тканевые панталоны бабушки Талина Кини» в кнопку шириной 140
        /// пикселей не влезает никак, а резать название нельзя — игрок
        /// выбирает вещь именно по имени.
        /// </param>
        private void AddButton(int index, string label, Color color,
                               System.Action onClick, bool stacked)
        {
            var go = new GameObject("Button" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(buttons, false);

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);

            if (stacked)
            {
                rect.anchoredPosition = new Vector2(0f, index * (ButtonHeight + 6f));
                rect.sizeDelta = new Vector2(Width - Pad * 2f, ButtonHeight);
            }
            else
            {
                // Кнопки по краям, как в образце: согласие слева, отказ
                // справа. Рядом посередине они читаются как пара равных,
                // и промахнуться по нужной легче.
                float span = Width - Pad * 2f - 150f;
                rect.anchoredPosition = new Vector2(index == 0 ? 0f : span, 0f);
                rect.sizeDelta = new Vector2(150f, ButtonHeight);
            }

            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var text = MakeText(rect, "Label", label, 13, TextColor);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;

            spawned.Add(go);
        }

        private void Build()
        {
            var canvasGo = new GameObject("DialogueHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 14;

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

            var go = new GameObject("DialogueWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // По центру, чуть ниже середины: разговор — то, на чём внимание
            // сосредоточено целиком, ему место в середине экрана.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(Width, WindowHeight);

            // Нарисованная рамка вместо плоской плашки. Заливка и
            // обводка нужны только тому, кому рамки не досталось: у неё
            // есть собственный контур, и вторая линия вокруг него читается
            // как лишний кант.
            if (!WindowChrome.ApplyFrame(go))
            {
                go.GetComponent<Image>().color = PanelColor;

                var edge = new GameObject("Edge", typeof(Image));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(rect, false);
                edgeRect.anchorMin = Vector2.zero;
                edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = new Vector2(-1f, -1f);
                edgeRect.offsetMax = new Vector2(1f, 1f);
                edge.transform.SetAsFirstSibling();
                edge.GetComponent<Image>().color = PanelEdge;
            }

            // Шапка — имя собеседника, по центру: так в образце, и так сразу
            // понятно, с кем говоришь. Название задания уехало внутрь, к
            // тексту, и стало там самым крупным.
            // Лицо собеседника.
            //
            // Портрета в разговоре не было вовсе — Павлон 03.09.2026: «у НПС
            // вообще нет портрета». В окне цели он есть, а в диалоге, то есть
            // ровно там, где на человека и смотрят, — не было.
            //
            // Слева от имени: имя остаётся по центру оставшейся ширины, и
            // раскладка ниже сдвигается на высоту портрета, чтобы текст
            // задания не наезжал на картинку.
            var faceGo = new GameObject("Face", typeof(Image));
            face = faceGo.GetComponent<Image>();

            var faceRect = face.rectTransform;
            faceRect.SetParent(rect, false);
            faceRect.anchorMin = new Vector2(0f, 1f);
            faceRect.anchorMax = new Vector2(0f, 1f);
            faceRect.pivot = new Vector2(0f, 1f);
            faceRect.anchoredPosition = new Vector2(Pad, -Pad);
            faceRect.sizeDelta = new Vector2(FaceSize, FaceSize);
            face.preserveAspect = true;
            face.enabled = false;

            title = MakeText(rect, "Title", "", 15, TextColor);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(Pad + FaceSize + 8f, -Pad);
            titleRect.sizeDelta = new Vector2(-(Pad * 2f + FaceSize + 8f), 22f);
            title.alignment = TextAnchor.MiddleCenter;

            // Разделы колонкой, как в образце: название задания, описание,
            // цели, награды. Раскладку ведёт группа — тогда пустой раздел
            // (у обычного разговора нет ни целей, ни наград) выключается, а
            // остальные сами сдвигаются вверх, без пустых дыр.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content = (RectTransform)contentGo.transform;
            content.SetParent(rect, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(Pad, Pad + ButtonsArea);
            content.offsetMax = new Vector2(-Pad, -(Pad + FaceSize + 6f));

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;

            questTitle = MakeSection(content, "QuestTitle", 19, TitleColor);
            body = MakeSection(content, "Description", 15, TextColor);
            objectivesHeader = MakeSection(content, "ObjectivesHeader", 17, TitleColor);
            objectives = MakeSection(content, "Objectives", 15, TextColor);
            rewardsHeader = MakeSection(content, "RewardsHeader", 17, TitleColor);
            rewardsHint = MakeSection(content, "RewardsHint", 13, HintColor);

            var row = new GameObject("Buttons", typeof(RectTransform));
            buttons = (RectTransform)row.transform;
            buttons.SetParent(rect, false);
            buttons.anchorMin = new Vector2(0f, 0f);
            buttons.anchorMax = new Vector2(0f, 0f);
            buttons.pivot = new Vector2(0f, 0f);
            buttons.anchoredPosition = new Vector2(Pad, Pad);
            buttons.sizeDelta = new Vector2(Width - Pad * 2f, ButtonHeight);

            window = go;
            window.SetActive(false);
        }

        /// <summary>
        /// Раздел окна: строка или абзац, который сам занимает нужную высоту.
        ///
        /// Высоту считает сам текст, а не мы числом: длинное описание иначе
        /// налезает на цели, а короткое оставляет дыру посреди окна.
        /// </summary>
        private Text MakeSection(RectTransform parent, string name, int size, Color color)
        {
            var text = MakeText(parent, name, "", size, color);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return text;
        }

        /// <summary>Заполняет раздел или прячет его, если сказать нечего.</summary>
        /// <summary>
        /// Сколько уже собрано по заданию.
        ///
        /// Журнал живёт на игроке; ищем его один раз и запоминаем — окно
        /// открывается часто, а поиск по сцене дорогой.
        /// </summary>
        private int ProgressOf(QuestDefinition quest)
        {
            if (quest == null) return 0;

            if (log == null) log = Object.FindFirstObjectByType<QuestLog>();

            return log != null ? log.Progress(quest) : 0;
        }

        private static void Fill(Text target, string value)
        {
            if (target == null) return;

            bool has = !string.IsNullOrEmpty(value);
            target.gameObject.SetActive(has);

            if (has) LocalizedText.Bind(target, value);
        }

        private Text MakeText(RectTransform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            LocalizedText.Bind(text, content);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
