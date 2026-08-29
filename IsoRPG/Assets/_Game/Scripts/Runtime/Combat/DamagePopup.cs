using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Localization;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Всплывающее число урона над целью.
    ///
    /// Без него бой читается плохо: полоска дёрнулась — а насколько, был ли
    /// крит, попал ли вообще, непонятно. Число отвечает на всё это сразу и
    /// стоит десяти строк кода.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color32(0xF0, 0xE8, 0xD8, 0xFF);
        private static readonly Color CritColor = new Color32(0xFF, 0xC4, 0x4A, 0xFF);
        private static readonly Color MissColor = new Color32(0x9A, 0x96, 0x8E, 0xFF);

        /// <summary>Зелёный лечения. Светлее травы, иначе теряется на земле.</summary>
        private static readonly Color HealColor = new Color32(0x7A, 0xD8, 0x72, 0xFF);

        /// <summary>
        /// Урон по герою — красным.
        ///
        /// Тот же цвет, что у своей полосы здоровья: в свалке из шести
        /// монстров числа летят со всех сторон, и по цвету должно быть
        /// понятно, чьё здоровье уходит, не читая, над кем цифра.
        /// </summary>
        private static readonly Color TakenColor = new Color32(0xE8, 0x5A, 0x4A, 0xFF);
        private static readonly Color TakenCritColor = new Color32(0xFF, 0x7A, 0x3A, 0xFF);

        private const float Lifetime = 0.9f;
        private const float RiseSpeed = 1.4f;
        private const float SideDrift = 0.35f;

        private Text label;
        private CanvasGroup group;
        private Camera cam;
        private float born;
        private Vector3 drift;

        /// <summary>
        /// Показать восстановленное здоровье.
        ///
        /// Отдельный метод, а не ещё одно значение в перечислении попаданий:
        /// лечение — не разновидность удара, и складывать их в один список
        /// значило бы потом всюду проверять «а это точно урон».
        /// </summary>
        public static void ShowHeal(Vector3 worldPosition, int amount)
        {
            Spawn(worldPosition, amount, HitResult.Normal, true);
        }

        /// <summary>Показать число над точкой мира.</summary>
        /// <summary>
        /// Урон, полученный героем. Отдельно от Show, потому что читается
        /// иначе: красным и крупнее обычного.
        /// </summary>
        public static void ShowTaken(Vector3 worldPosition, int amount, HitResult result)
        {
            Spawn(worldPosition, amount, result, heal: false, taken: true);
        }

        public static void Show(Vector3 worldPosition, int amount, HitResult result)
        {
            Spawn(worldPosition, amount, result, false);
        }

        private static void Spawn(Vector3 worldPosition, int amount,
                                  HitResult result, bool heal, bool taken = false)
        {
            var go = new GameObject("DamagePopup", typeof(Canvas), typeof(CanvasGroup));
            go.transform.position = worldPosition;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(2f, 0.6f);

            var popup = go.AddComponent<DamagePopup>();
            popup.Build(amount, result, heal, taken);
        }

        private void Build(int amount, HitResult result, bool heal, bool taken = false)
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            cam = Camera.main;
            born = Time.time;

            // Разлёт в стороны: без него числа при частых ударах ложатся
            // ровно друг на друга и превращаются в мельтешение.
            drift = new Vector3(Random.Range(-SideDrift, SideDrift), 0f, 0f);

            var textGo = new GameObject("Text", typeof(Text));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label = textGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Три исхода читаются по-разному: крит крупный с восклицанием,
            // отражённый удар тусклый и мелкий, обычный посередине.
            label.text = result == HitResult.Crit ? amount + "!" : amount.ToString();
            label.color = heal ? HealColor
                        : taken ? (result == HitResult.Crit ? TakenCritColor : TakenColor)
                        : result == HitResult.Crit ? CritColor
                        : result == HitResult.Miss ? MissColor
                        : NormalColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // Мировой Canvas меряется в метрах, поэтому шрифт берём крупный,
            // а размер задаём масштабом — иначе текст выйдет размытым.
            label.fontSize = 40;
            label.fontStyle = result == HitResult.Crit ? FontStyle.Bold : FontStyle.Normal;

            // Крит вдвое крупнее обычного удара.
            //
            // Раньше разница была в полтора раза — и в бою, где числа летят
            // очередью, она терялась: крит отличался только оттенком. Размер
            // читается быстрее цвета и не зависит от того, на каком фоне
            // оказалась цифра.
            float scale = result == HitResult.Crit ? 0.018f
                        : result == HitResult.Miss ? 0.007f
                        : 0.009f;
            transform.localScale = Vector3.one * scale;
        }

        private void LateUpdate()
        {
            float age = Time.time - born;

            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3.up * RiseSpeed + drift) * Time.deltaTime;

            // Гаснет во второй половине жизни: сразу начинать таять нельзя,
            // число не успеет прочитаться.
            float fade = Mathf.InverseLerp(Lifetime * 0.5f, Lifetime, age);
            if (group != null) group.alpha = 1f - fade;

            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
