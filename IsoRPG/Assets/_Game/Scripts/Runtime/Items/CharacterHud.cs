using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Окно персонажа: надетые вещи и итоговые характеристики.
    ///
    /// Без него экипировка работает вслепую — не видно ни что надето, ни
    /// что это дало. А смысл всей системы предметов именно в том, чтобы
    /// игрок видел, как растёт его персонаж.
    /// </summary>
    public sealed class CharacterHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xF0);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0xFF);
        /// <summary>Силуэт пустого слота: заметен, но не спорит с вещами.</summary>
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.28f);

        private static readonly Color SlotEmpty = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color TextDim = new Color32(0xA8, 0xA0, 0x90, 0xFF);
        private static readonly Color StatColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);

        private const float Margin = 18f;
        private const float Width = 250f;
        private const float RowHeight = 30f;
        private const float Pad = 12f;
        private const float TitleHeight = 24f;
        private const float StatsHeight = 86f;

        // Слоты, которые показываем. Порядок сверху вниз — как на человеке.
        private static readonly EquipSlot[] Slots =
        {
            EquipSlot.Head,
            EquipSlot.Chest,
            EquipSlot.Hands,
            EquipSlot.Legs,
            EquipSlot.Feet,
            EquipSlot.MainHand,
            EquipSlot.OffHand,
            EquipSlot.Ring,
        };

        private static readonly Dictionary<EquipSlot, string> SlotNames = new Dictionary<EquipSlot, string>
        {
            { EquipSlot.Head, "Голова" },
            { EquipSlot.Chest, "Грудь" },
            { EquipSlot.Hands, "Кисти" },
            { EquipSlot.Legs, "Ноги" },
            { EquipSlot.Feet, "Ступни" },
            { EquipSlot.MainHand, "Правая рука" },
            { EquipSlot.OffHand, "Левая рука" },
            { EquipSlot.Ring, "Кольцо" },
        };

        [SerializeField] private Equipment equipment;
        [SerializeField] private WeaponStats weapon;
        [SerializeField] private DefenseStats defense;
        [SerializeField] private Experience experience;

        private Font font;
        private GameObject window;
        private Text statsText;

        private readonly Dictionary<EquipSlot, Image> slotIcons = new Dictionary<EquipSlot, Image>();
        private readonly Dictionary<EquipSlot, Text> slotLabels = new Dictionary<EquipSlot, Text>();

        /// <summary>Рисунок надетой вещи поверх плашки слота.</summary>
        private readonly Dictionary<EquipSlot, Image> slotArt = new Dictionary<EquipSlot, Image>();

        /// <summary>Подсказки пустых слотов: силуэт шлема, сапог, кольца.</summary>
        private readonly Dictionary<EquipSlot, Sprite> slotHints = new Dictionary<EquipSlot, Sprite>();

        /// <summary>Задаётся сборщиком сцены: какой силуэт какому слоту.</summary>
        public void SetupSlotHints(EquipSlot slot, Sprite hint) => slotHints[slot] = hint;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponentInParent<Equipment>();
            if (weapon == null) weapon = GetComponentInParent<WeaponStats>();
            if (defense == null) defense = GetComponentInParent<DefenseStats>();
            if (experience == null) experience = GetComponentInParent<Experience>();

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void OnEnable()
        {
            if (equipment != null) equipment.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.Changed -= Refresh;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || window == null) return;

            // C — как в играх жанра.
            if (keyboard.cKey.wasPressedThisFrame)
            {
                window.SetActive(!window.activeSelf);

                if (window.activeSelf)
                {
                    Refresh();
                    IsoRPG.Audio.Sfx.OpenWindow();
                }
                else IsoRPG.Audio.Sfx.CloseWindow();
            }

            if (keyboard.escapeKey.wasPressedThisFrame && window.activeSelf)
                window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("CharacterHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            float height = TitleHeight + Slots.Length * RowHeight + StatsHeight + Pad * 2f;

            var go = new GameObject("CharacterWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // Слева, под панелями игрока и цели — как окно персонажа в WoW.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Margin, -(Margin + 90f));
            rect.sizeDelta = new Vector2(Width, height);

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

            var title = MakeText(rect, "Title", "Персонаж", 14, TextColor);
            Place(title, new Vector2(Pad, -Pad), new Vector2(Width - Pad * 2f, TitleHeight));
            title.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < Slots.Length; i++)
                BuildRow(rect, Slots[i], -(Pad + TitleHeight + i * RowHeight));

            statsText = MakeText(rect, "Stats", "", 12, StatColor);
            Place(statsText, new Vector2(Pad, -(Pad + TitleHeight + Slots.Length * RowHeight + 4f)),
                  new Vector2(Width - Pad * 2f, StatsHeight));
            statsText.alignment = TextAnchor.UpperLeft;

            window = go;
            window.SetActive(false);
        }

        private void BuildRow(RectTransform parent, EquipSlot slot, float y)
        {
            // Квадратик предмета
            var iconGo = new GameObject(slot + "Icon", typeof(Image), typeof(Button));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(Pad, y);
            iconRect.sizeDelta = new Vector2(26f, 26f);

            var icon = iconGo.GetComponent<Image>();
            icon.color = SlotEmpty;
            slotIcons[slot] = icon;

            // Картинка отдельным слоем: плашка остаётся цветом редкости, а
            // рисунок сверху не перекрашивается вместе с ней.
            var art = new GameObject("Art", typeof(Image));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(iconRect, false);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(1.5f, 1.5f);
            artRect.offsetMax = new Vector2(-1.5f, -1.5f);

            var artImage = art.GetComponent<Image>();
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;
            artImage.enabled = false;

            slotArt[slot] = artImage;

            var captured = slot;
            iconGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (equipment != null) equipment.Unequip(captured);
            });

            // Подпись: название слота или предмета
            var label = MakeText(parent, slot + "Label", SlotNames[slot], 12, TextDim);
            Place(label, new Vector2(Pad + 32f, y - 4f), new Vector2(Width - Pad * 2f - 32f, 20f));
            slotLabels[slot] = label;
        }

        private void Refresh()
        {
            if (equipment == null) return;

            foreach (var slot in Slots)
            {
                var stack = equipment.GetSlot(slot);

                if (stack.IsEmpty)
                {
                    slotIcons[slot].color = SlotEmpty;
                    slotLabels[slot].text = SlotNames[slot];
                    slotLabels[slot].color = TextDim;

                    // В пустом слоте показываем приглушённый силуэт того, что
                    // сюда надевается. Пустой квадрат ничего не сообщает, а
                    // силуэт объясняет назначение без единого слова.
                    if (slotArt.TryGetValue(slot, out var emptyArt))
                    {
                        bool hasHint = slotHints.TryGetValue(slot, out var hint) && hint != null;

                        emptyArt.sprite = hasHint ? hint : null;
                        emptyArt.color = HintColor;
                        emptyArt.enabled = hasHint;
                    }
                }
                else
                {
                    slotIcons[slot].color = stack.Item.RarityColor;
                    slotLabels[slot].text = stack.Item.displayName;
                    slotLabels[slot].color = stack.Item.RarityColor;

                    if (slotArt.TryGetValue(slot, out var wornArt))
                    {
                        wornArt.sprite = stack.Item.icon;
                        wornArt.color = Color.white;
                        wornArt.enabled = stack.Item.icon != null;
                    }
                }
            }

            if (statsText == null) return;

            var bonus = equipment.TotalStatBonus();
            string weaponLine = weapon != null
                ? $"{weapon.WeaponName}: урон {weapon.WeaponDamage}, раз в {weapon.AttackInterval:0.0} с"
                : "оружия нет";

            statsText.text =
                $"Уровень: {(experience != null ? experience.Level : 1)}\n" +
                $"{weaponLine}\n" +
                $"Броня: {(defense != null ? defense.Armor : 0)}\n" +
                $"Сила {bonus.Strength}, ловкость {bonus.Agility}, выносливость {bonus.Stamina}";
        }

        // ------------------------------------------------------------------

        private Text MakeText(RectTransform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
