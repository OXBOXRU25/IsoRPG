using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Localization;

namespace IsoRPG.Items
{
    /// <summary>
    /// Всплывающая надпись о подобранной вещи: «Кинжал», «12 золота».
    ///
    /// Цвет берётся от редкости предмета — это первое, что игрок замечает.
    /// Синяя надпись читается как удача ещё до того, как прочитано название.
    /// </summary>
    public sealed class LootPopup : MonoBehaviour
    {
        private const float Lifetime = 2f;
        private const float RiseSpeed = 0.55f;

        // Сколько уже висит надписей — чтобы новые не ложились на старые.
        private static int activeCount;

        private CanvasGroup group;
        private Camera cam;
        private float born;

        public static void Show(Vector3 worldPosition, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            var go = new GameObject("LootPopup", typeof(Canvas), typeof(CanvasGroup));

            // Каждая следующая надпись чуть выше предыдущей: при подборе
            // нескольких вещей сразу они иначе печатаются друг на друге.
            go.transform.position = worldPosition + Vector3.up * (1.4f + activeCount * 0.32f);
            activeCount++;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(4f, 0.6f);

            go.AddComponent<LootPopup>().Build(text, color);
        }

        private void Build(string text, Color color)
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            cam = Camera.main;
            born = Time.time;

            var textGo = new GameObject("Text", typeof(Text));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = Loc.T(text);
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontSize = 32;

            transform.localScale = Vector3.one * 0.008f;
        }

        private void LateUpdate()
        {
            float age = Time.time - born;

            if (age >= Lifetime)
            {
                activeCount = Mathf.Max(0, activeCount - 1);
                Destroy(gameObject);
                return;
            }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float fade = Mathf.InverseLerp(Lifetime * 0.65f, Lifetime, age);
            if (group != null) group.alpha = 1f - fade;

            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
