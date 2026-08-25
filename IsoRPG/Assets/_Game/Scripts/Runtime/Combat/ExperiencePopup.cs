using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Localization;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Всплывающая надпись про полученный опыт.
    ///
    /// Отдельно от урона намеренно: это награда, а не удар. Поднимается
    /// медленнее, живёт дольше и не разлетается в стороны — её должны
    /// успеть прочитать, а не заметить краем глаза.
    /// </summary>
    public sealed class ExperiencePopup : MonoBehaviour
    {
        private static readonly Color ExpColor = new Color32(0x9A, 0x7A, 0xD0, 0xFF);

        private const float Lifetime = 1.6f;
        private const float RiseSpeed = 0.7f;

        private CanvasGroup group;
        private Camera cam;
        private float born;

        public static void Show(Vector3 worldPosition, int amount)
        {
            if (amount <= 0) return;

            var go = new GameObject("ExpPopup", typeof(Canvas), typeof(CanvasGroup));

            // Чуть выше урона, чтобы надписи не наложились друг на друга
            // в момент добивающего удара.
            go.transform.position = worldPosition + Vector3.up * 0.45f;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(3f, 0.6f);

            go.AddComponent<ExperiencePopup>().Build(amount);
        }

        private void Build(int amount)
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
            label.text = Loc.F("+{0} опыта", amount);
            label.color = ExpColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontSize = 34;

            transform.localScale = Vector3.one * 0.008f;
        }

        private void LateUpdate()
        {
            float age = Time.time - born;

            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float fade = Mathf.InverseLerp(Lifetime * 0.6f, Lifetime, age);
            if (group != null) group.alpha = 1f - fade;

            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
