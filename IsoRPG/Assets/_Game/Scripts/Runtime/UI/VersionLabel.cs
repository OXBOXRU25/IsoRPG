using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.UI
{
    /// <summary>
    /// Подпись с номером версии в углу экрана.
    ///
    /// Нужна не для красоты. Когда игрок говорит «у меня не работает», первый
    /// вопрос — какая у него сборка; без подписи ответить на это нельзя ни ему,
    /// ни нам. По той же причине она висит и в меню, и в самой игре: скриншот
    /// из середины боя должен нести версию так же, как стартовый экран.
    ///
    /// Номер берётся из настроек проекта, куда его перед сборкой кладёт
    /// GameVersion из CHANGELOG.md. Своего числа здесь нет намеренно.
    /// </summary>
    public sealed class VersionLabel : MonoBehaviour
    {
        [Tooltip("Приписка перед номером — например «альфа».")]
        [SerializeField] private string prefix = "альфа";

        private void Start()
        {
            var text = GetComponent<Text>();
            if (text == null) return;

            string version = Application.version;

            text.text = string.IsNullOrEmpty(prefix)
                ? "v" + version
                : prefix + " " + version;
        }

        /// <summary>
        /// Вешает подпись в правый нижний угол переданного холста.
        ///
        /// Собирается кодом, как и остальной интерфейс: так подпись одинакова
        /// в обеих сценах и не может разойтись между ними.
        /// </summary>
        public static VersionLabel Attach(Transform canvas, Font font, string prefix = "альфа")
        {
            var go = new GameObject("VersionLabel", typeof(Text), typeof(VersionLabel));
            go.transform.SetParent(canvas, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 12f);
            rect.sizeDelta = new Vector2(300f, 22f);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.alignment = TextAnchor.LowerRight;

            // Приглушённый белый: подпись должна читаться, но не спорить с
            // содержимым экрана. Полностью белая тянет взгляд на себя.
            text.color = new Color(1f, 1f, 1f, 0.45f);
            text.raycastTarget = false;

            var label = go.GetComponent<VersionLabel>();
            label.prefix = prefix;

            return label;
        }
    }
}
