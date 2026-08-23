using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IsoRPG.Combat;

namespace IsoRPG.Items
{
    /// <summary>
    /// Окно добычи: что лежит в мешке и что из этого брать.
    ///
    /// Смысл окна не в удобстве, а в выборе. Автоматический подбор экономит
    /// клик, но отнимает решение: игрок не смотрит на добычу и не оценивает
    /// её — вещи просто накапливаются в сумке. Окно возвращает момент, ради
    /// которого добыча вообще нужна.
    ///
    /// Закрывается само, когда игрок отходит: мешок остаётся лежать, к нему
    /// можно вернуться, а окно, висящее через полкарты, — мусор на экране.
    /// </summary>
    public sealed class LootWindow : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color32(0x1C, 0x1A, 0x16, 0xD2);
        private static readonly Color PanelEdge = new Color32(0x3A, 0x36, 0x2C, 0x8A);
        private static readonly Color RowColor = new Color32(0x2A, 0x27, 0x21, 0xC0);
        private static readonly Color TextColor = new Color32(0xE8, 0xE2, 0xD4, 0xFF);
        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        private const float Width = 230f;
        private const float RowHeight = 30f;
        private const float Pad = 10f;
        private const float TitleHeight = 24f;

        [Tooltip("Дальше этого расстояния окно закрывается само.")]
        [SerializeField] private float maxDistance = 4.5f;

        private Inventory inventory;
        private Font font;

        private GameObject window;
        private RectTransform rows;
        private LootDrop current;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private void Awake()
        {
            inventory = GetComponentInParent<Inventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();
        }

        private void Update()
        {
            if (current == null)
            {
                if (window != null && window.activeSelf) Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Отошёл — закрываем. Мешок остаётся лежать.
            if (Vector3.Distance(transform.position, current.transform.position) > maxDistance)
                Close();
        }

        /// <summary>Открыть мешок. Вызывается по клику.</summary>
        public void Open(LootDrop drop)
        {
            if (drop == null) return;

            if (current != null)
            {
                current.Changed -= Refresh;
                current.Emptied -= Close;
            }

            current = drop;
            current.Changed += Refresh;
            current.Emptied += Close;

            window.SetActive(true);
            Refresh();

            IsoRPG.Audio.Sfx.OpenWindow();
        }

        public void Close()
        {
            if (current != null)
            {
                current.Changed -= Refresh;
                current.Emptied -= Close;
                current = null;
            }

            if (window != null) window.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Refresh()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            if (current == null) return;

            int index = 0;

            if (current.Gold > 0)
            {
                AddRow(index++, current.Gold + " золота", GoldColor, () =>
                {
                    int taken = current.TakeGold(inventory);
                    if (taken <= 0) return;

                    CombatLog.GainedGold(taken);
                    IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.gold,
                                          transform.position, 0.45f, 0.11f);
                });
            }

            var contents = current.Contents;

            for (int i = 0; i < contents.Count; i++)
            {
                int slot = i;
                var stack = contents[i];

                AddRow(index++, stack.ToString(), stack.Item.RarityColor, () =>
                {
                    if (!current.TakeItem(slot, inventory, out var taken)) return;

                    CombatLog.Looted(taken.ToString(), taken.Item.RarityColor);
                    IsoRPG.Audio.Sfx.Play(IsoRPG.Audio.Sfx.Bank?.pickup,
                                          transform.position, 0.4f, 0.11f);
                });
            }

            // Высота окна по содержимому: пустое место под последней строкой
            // выглядит так, будто там что-то не отрисовалось.
            var rect = (RectTransform)window.transform;
            rect.sizeDelta = new Vector2(Width, TitleHeight + index * RowHeight + Pad * 2f);
        }

        private void AddRow(int index, string label, Color color, System.Action onClick)
        {
            var go = new GameObject("Row" + index, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(rows, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rect.sizeDelta = new Vector2(0f, RowHeight - 3f);

            go.GetComponent<Image>().color = RowColor;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var text = MakeText(rect, "Label", label, 12, color);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            spawned.Add(go);
        }

        private void Build()
        {
            var canvasGo = new GameObject("LootHUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("LootWindow", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent((RectTransform)canvasGo.transform, false);

            // По центру справа: не закрывает ни персонажа, ни лог боя.
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 60f);
            rect.sizeDelta = new Vector2(Width, 120f);

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

            var title = MakeText(rect, "Title", "Добыча", 13, TextColor);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -Pad * 0.5f);
            titleRect.sizeDelta = new Vector2(0f, TitleHeight);
            title.alignment = TextAnchor.MiddleCenter;

            var list = new GameObject("Rows", typeof(RectTransform));
            rows = (RectTransform)list.transform;
            rows.SetParent(rect, false);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0f, 1f);
            rows.anchoredPosition = new Vector2(Pad, -(TitleHeight + Pad * 0.5f));
            rows.sizeDelta = new Vector2(-Pad * 2f, 0f);

            window = go;
            window.SetActive(false);
        }

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
    }
}
