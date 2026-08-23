using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.UI
{
    /// <summary>
    /// Всплывающая подсказка: иконка, название, характеристики, описание.
    ///
    /// Одна на всю игру и на все виды содержимого — приёмы, предметы, кнопки.
    /// Причина не в экономии кода: игрок читает подсказку сотни раз за сессию,
    /// и если у меча и у приёма разная вёрстка, каждый раз приходится заново
    /// искать глазами, где тут урон. Одинаковая раскладка читается по памяти.
    ///
    /// Порядок строк везде один: сначала то, что решает «применять или нет»
    /// (урон, цена, откат), потом условия применения, и только в конце
    /// описание. Описание — самое красивое и самое бесполезное в бою.
    /// </summary>
    public sealed class Tooltip : MonoBehaviour
    {
        public static Tooltip Instance { get; private set; }

        // Фон полупрозрачный: под подсказкой остаётся видно, над чем она
        // висит. Совсем прозрачной делать нельзя — текст по пёстрой сцене
        // не читается.
        private static readonly Color PanelColor = new Color32(0x14, 0x12, 0x0F, 0xE6);
        private static readonly Color EdgeColor = new Color32(0x4A, 0x44, 0x36, 0xC0);
        private static readonly Color SubtitleColor = new Color32(0x9A, 0x94, 0x86, 0xFF);
        private static readonly Color LabelColor = new Color32(0x9A, 0x94, 0x86, 0xFF);
        private static readonly Color ValueColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color BodyColor = new Color32(0xA8, 0xA2, 0x94, 0xFF);
        private static readonly Color GoodColor = new Color32(0x7A, 0xC8, 0x6A, 0xFF);
        private static readonly Color WarnColor = new Color32(0xD8, 0x6A, 0x5A, 0xFF);
        private static readonly Color AbilityTitle = new Color32(0xF0, 0xE4, 0xC8, 0xFF);
        private static readonly Color IconBackdrop = new Color32(0x24, 0x21, 0x1B, 0xFF);

        private const float Width = 272f;
        private const float IconSize = 46f;

        private Font font;
        private RectTransform panel;
        private Image icon;
        private Image iconBack;
        private Text title;
        private Text subtitle;
        private RectTransform rows;
        private Text body;

        private readonly List<GameObject> rowPool = new List<GameObject>();
        private int rowsUsed;

        private void Awake()
        {
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Способность

        /// <summary>
        /// Подсказка о приёме. Урон считается по оружию, которое сейчас в
        /// руках: цифра «34» полезна, «умножить на урон оружия» — нет.
        /// </summary>
        public void ShowAbility(AbilityDefinition ability, int weaponDamage, Vector2 at)
        {
            if (ability == null) return;

            Begin();

            SetIcon(ability.icon, ability.iconColor);
            title.text = ability.displayName;
            title.color = AbilityTitle;

            subtitle.text = AbilityKind(ability);
            subtitle.gameObject.SetActive(true);

            if (ability.dealsDamage)
            {
                if (ability.comboRole == ComboRole.Finisher && ability.finisherDamage.Length > 0)
                {
                    // У добивания урон зависит от потраченных очков, и
                    // показывать одно число нечестно: разброс между одним и
                    // пятым очком у нас четырёхкратный.
                    var low = ability.finisherDamage[0];
                    var high = ability.finisherDamage[ability.finisherDamage.Length - 1];

                    int min = Mathf.RoundToInt(weaponDamage * ability.weaponMultiplier) + low.min;
                    int max = Mathf.RoundToInt(weaponDamage * ability.weaponMultiplier) + high.max;

                    AddRow("Урон", min + " — " + max, ValueColor);
                    AddRow("", "за 1 и за 5 очков", SubtitleColor);
                }
                else
                {
                    int hit = ability.ComputeBaseDamage(weaponDamage, 0);
                    AddRow("Урон", "~" + hit, ValueColor);
                }
            }

            if (ability.energyCost > 0) AddRow("Стоимость", ability.energyCost + " энергии", ValueColor);
            if (ability.cooldown > 0.01f) AddRow("Откат", ability.cooldown.ToString("0.#") + " с", ValueColor);
            if (ability.requiresTarget) AddRow("Дистанция", ability.reach.ToString("0.#") + " м", ValueColor);

            // Механика приёма. Это то, ради чего игрок и открывает подсказку:
            // из названия «Коварный удар» не следует, что бить надо со спины
            // и только из скрытности.
            if (ability.comboRole == ComboRole.Generator && ability.comboGain > 0)
                AddRow("", "Даёт " + ability.comboGain + " очко комбо", GoodColor);

            if (ability.comboRole == ComboRole.Finisher)
                AddRow("", "Тратит все очки комбо", GoodColor);

            if (ability.stunBase > 0.01f || ability.stunPerCombo > 0.01f)
            {
                string stun = ability.stunBase.ToString("0.#") + " с";
                if (ability.stunPerCombo > 0.01f)
                    stun += " плюс " + ability.stunPerCombo.ToString("0.#") + " за очко";

                AddRow("Оглушение", stun, GoodColor);
            }

            if (ability.togglesStealth) AddRow("", "Уводит в скрытность", GoodColor);
            if (ability.requiresStealth) AddRow("", "Только из скрытности", WarnColor);
            if (ability.requiresBehindTarget) AddRow("", "Только со спины цели", WarnColor);

            body.text = ability.description;
            body.gameObject.SetActive(!string.IsNullOrEmpty(ability.description));

            End(at);
        }

        // ------------------------------------------------------------------
        // Предмет

        /// <summary>
        /// Подсказка о предмете. Уровень игрока нужен, чтобы подсветить
        /// красным вещь, которую пока не надеть.
        /// </summary>
        public void ShowItem(ItemDefinition item, int playerLevel, Vector2 at)
        {
            if (item == null) return;

            Begin();

            SetIcon(item.icon, item.iconColor);

            title.text = item.displayName;

            // Заголовок красится в цвет редкости: это единственное место, где
            // редкость названа словом и цветом одновременно, — дальше игрок
            // узнаёт её по одному цвету рамки.
            title.color = item.RarityColor;

            subtitle.text = ItemKind(item);
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle.text));

            if (item.IsWeapon)
            {
                AddRow("Урон", item.weaponDamage.ToString(), ValueColor);
                AddRow("Скорость", item.attackInterval.ToString("0.0") + " с", ValueColor);

                // Урон в секунду — единственный способ сравнить быстрый кинжал
                // с медленным топором, не считая в уме.
                float dps = item.weaponDamage / Mathf.Max(0.1f, item.attackInterval);
                AddRow("Урон в секунду", dps.ToString("0.#"), SubtitleColor);
            }

            if (item.armor > 0) AddRow("Броня", item.armor.ToString(), ValueColor);

            if (item.strength > 0) AddRow("Сила", "+" + item.strength, GoodColor);
            if (item.agility > 0) AddRow("Ловкость", "+" + item.agility, GoodColor);
            if (item.stamina > 0) AddRow("Выносливость", "+" + item.stamina, GoodColor);

            if (item.requiredLevel > 1)
            {
                bool ok = playerLevel >= item.requiredLevel;
                AddRow("Требуется", "уровень " + item.requiredLevel, ok ? SubtitleColor : WarnColor);
            }

            if (item.vendorPrice > 0) AddRow("Цена", item.vendorPrice + " золота", SubtitleColor);

            body.text = item.description;
            body.gameObject.SetActive(!string.IsNullOrEmpty(item.description));

            End(at);
        }

        // ------------------------------------------------------------------
        // Талант

        /// <summary>
        /// Подсказка о таланте. Показывает и то, что даёт сейчас, и то, что
        /// даст следующее очко: без второго числа игрок не может решить,
        /// вкладывать ли ещё, — а это единственное решение в этом окне.
        /// </summary>
        public void ShowTalent(IsoRPG.Progression.TalentDefinition talent, int rank,
                               string blockReason, Vector2 at)
        {
            if (talent == null) return;

            Begin();

            SetIcon(talent.icon, Color.white);

            title.text = talent.displayName;
            title.color = IsoRPG.Progression.TalentDefinition.BranchColor(talent.branch);

            subtitle.text = IsoRPG.Progression.TalentDefinition.BranchName(talent.branch)
                            + ",  ранг " + rank + " из " + talent.maxRank;
            subtitle.gameObject.SetActive(true);

            if (rank > 0) AddRow("Сейчас", talent.EffectLine(rank), ValueColor);

            if (rank < talent.maxRank)
                AddRow(rank > 0 ? "Станет" : "Даст", talent.EffectLine(rank + 1), GoodColor);

            if (!string.IsNullOrEmpty(blockReason)) AddRow("", blockReason, WarnColor);

            body.text = talent.description;
            body.gameObject.SetActive(!string.IsNullOrEmpty(talent.description));

            End(at);
        }

        // ------------------------------------------------------------------
        // Простая подсказка: заголовок и строчка пояснения

        public void ShowText(string caption, string hint, Vector2 at)
        {
            Begin();

            SetIcon(null, Color.clear);

            title.text = caption;
            title.color = AbilityTitle;

            subtitle.gameObject.SetActive(false);

            body.text = hint;
            body.gameObject.SetActive(!string.IsNullOrEmpty(hint));

            End(at);
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------

        private static string AbilityKind(AbilityDefinition ability)
        {
            if (ability.togglesStealth) return "Приём: скрытность";

            return ability.comboRole switch
            {
                ComboRole.Finisher => "Приём: добивание",
                ComboRole.Generator => "Приём: набор комбо",
                _ => "Приём",
            };
        }

        private static string ItemKind(ItemDefinition item)
        {
            string place = item.slot switch
            {
                EquipSlot.Head => "На голову",
                EquipSlot.Chest => "На корпус",
                EquipSlot.Hands => "На руки",
                EquipSlot.Legs => "На ноги",
                EquipSlot.Feet => "На ноги",
                EquipSlot.MainHand => "В правую руку",
                EquipSlot.OffHand => "В левую руку",
                EquipSlot.Ring => "Кольцо",
                _ => "",
            };

            string rarity = item.rarity switch
            {
                ItemRarity.Junk => "хлам",
                ItemRarity.Common => "обычное",
                ItemRarity.Uncommon => "необычное",
                ItemRarity.Rare => "редкое",
                ItemRarity.Epic => "эпическое",
                _ => "",
            };

            if (string.IsNullOrEmpty(place)) return rarity;
            if (string.IsNullOrEmpty(rarity)) return place;

            return place + ", " + rarity;
        }

        private void SetIcon(Sprite sprite, Color tint)
        {
            bool has = sprite != null;

            icon.enabled = has;
            iconBack.enabled = has;

            if (!has) return;

            icon.sprite = sprite;
            icon.color = Color.white;
            iconBack.color = IconBackdrop;
        }

        private void Begin()
        {
            rowsUsed = 0;
            foreach (var row in rowPool) row.SetActive(false);
        }

        private void End(Vector2 at)
        {
            panel.gameObject.SetActive(true);

            // Раскладка считается сразу, а не в конце кадра: положение
            // подсказки зависит от её высоты, а высота — от того, сколько
            // строк мы только что добавили.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            Place(at);
        }

        private void Place(Vector2 at)
        {
            panel.position = new Vector3(at.x + 18f, at.y + 18f, 0f);

            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);

            float overRight = corners[2].x - Screen.width;
            if (overRight > 0f) panel.position -= new Vector3(overRight + 10f, 0f, 0f);

            float overLeft = -corners[0].x;
            if (overLeft > 0f) panel.position += new Vector3(overLeft + 10f, 0f, 0f);

            // Не помещается сверху — показываем под курсором. Это лучше, чем
            // прижать к верхнему краю: прижатая подсказка накрывает то, на
            // что игрок навёл.
            panel.GetWorldCorners(corners);

            float overTop = corners[1].y - Screen.height;
            if (overTop > 0f)
            {
                float height = corners[1].y - corners[0].y;
                panel.position -= new Vector3(0f, height + 36f, 0f);
            }
        }

        private void AddRow(string label, string value, Color valueColor)
        {
            GameObject go;

            if (rowsUsed < rowPool.Count)
            {
                go = rowPool[rowsUsed];
                go.SetActive(true);
            }
            else
            {
                go = MakeRow();
                rowPool.Add(go);
            }

            go.transform.SetSiblingIndex(rowsUsed);
            rowsUsed++;

            var texts = go.GetComponentsInChildren<Text>(true);
            texts[0].text = label;
            texts[1].text = value;
            texts[1].color = valueColor;

            // Строка без названия — это пояснение механики, и оно занимает
            // всю ширину, прижатое влево. Иначе одинокое «Только со спины»
            // висело бы у правого края непонятно от чего.
            bool hasLabel = !string.IsNullOrEmpty(label);
            texts[1].alignment = hasLabel ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        private GameObject MakeRow()
        {
            var go = new GameObject("Row", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(rows, false);

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 17f;
            element.minHeight = 17f;

            var label = MakeText(rect, "Label", 12, LabelColor);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleLeft;

            var value = MakeText(rect, "Value", 12, ValueColor);
            var valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            value.alignment = TextAnchor.MiddleRight;

            return go;
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Поверх всего: подсказка по определению перекрывает то, о чём
            // рассказывает.
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Tooltip", typeof(Image), typeof(VerticalLayoutGroup),
                                    typeof(ContentSizeFitter));
            panel = (RectTransform)go.transform;
            panel.SetParent((RectTransform)canvasGo.transform, false);
            panel.pivot = new Vector2(0f, 0f);
            panel.sizeDelta = new Vector2(Width, 0f);

            go.GetComponent<Image>().color = PanelColor;
            go.GetComponent<Image>().raycastTarget = false;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(11, 11, 9, 10);
            layout.spacing = 5f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var edge = new GameObject("Edge", typeof(Image), typeof(LayoutElement));
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.SetParent(panel, false);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = new Vector2(-1f, -1f);
            edgeRect.offsetMax = new Vector2(1f, 1f);
            edge.transform.SetAsFirstSibling();
            edge.GetComponent<Image>().color = EdgeColor;
            edge.GetComponent<Image>().raycastTarget = false;
            edge.GetComponent<LayoutElement>().ignoreLayout = true;

            BuildHeader();

            rows = MakeColumn("Rows", 2f);
            body = MakeText(panel, "Body", 12, BodyColor);

            panel.gameObject.SetActive(false);
        }

        private void BuildHeader()
        {
            var header = new GameObject("Header", typeof(RectTransform),
                                        typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rect = (RectTransform)header.transform;
            rect.SetParent(panel, false);

            var layout = header.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 9f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            header.GetComponent<LayoutElement>().minHeight = IconSize;

            var backGo = new GameObject("IconBack", typeof(Image), typeof(LayoutElement));
            var backRect = (RectTransform)backGo.transform;
            backRect.SetParent(rect, false);

            var backElement = backGo.GetComponent<LayoutElement>();
            backElement.preferredWidth = IconSize;
            backElement.preferredHeight = IconSize;
            backElement.flexibleWidth = 0f;

            iconBack = backGo.GetComponent<Image>();
            iconBack.color = IconBackdrop;
            iconBack.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(backRect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            var column = new GameObject("Titles", typeof(RectTransform),
                                        typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var columnRect = (RectTransform)column.transform;
            columnRect.SetParent(rect, false);

            var columnLayout = column.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 2f;
            columnLayout.childForceExpandHeight = false;
            columnLayout.childControlHeight = true;
            columnLayout.childControlWidth = true;
            columnLayout.childAlignment = TextAnchor.MiddleLeft;

            column.GetComponent<LayoutElement>().flexibleWidth = 1f;

            title = MakeText(columnRect, "Title", 14, AbilityTitle);
            subtitle = MakeText(columnRect, "Subtitle", 11, SubtitleColor);
        }

        private RectTransform MakeColumn(string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            return rect;
        }

        private Text MakeText(RectTransform parent, string name, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;

            return text;
        }
    }
}
