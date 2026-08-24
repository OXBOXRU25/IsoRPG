using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.Localization
{
    /// <summary>
    /// Держит подпись на выбранном языке.
    ///
    /// Нужен там, где текст попадает в сцену при сборке и потом уже не
    /// пересоздаётся — прежде всего в главном меню. Компонент запоминает
    /// русский оригинал и подставляет перевод при запуске и при каждой смене
    /// языка.
    ///
    /// Оригинал хранится отдельно, а не читается из самой подписи: после
    /// первого же переключения там будет английский, и обратного пути к
    /// русскому ключу не осталось бы.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Русский текст — он же ключ перевода.")]
        [SerializeField] private string russian = string.Empty;

        private Text label;

        public void Setup(string original)
        {
            russian = original;
            Apply();
        }

        private void Awake()
        {
            label = GetComponent<Text>();

            // Если оригинал не задали явно, берём то, что стоит в подписи
            // сейчас: при сборке там русский текст.
            if (string.IsNullOrEmpty(russian) && label != null) russian = label.text;
        }

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Loc.Changed -= Apply;
        }

        private void Apply()
        {
            if (label == null) label = GetComponent<Text>();
            if (label == null || string.IsNullOrEmpty(russian)) return;

            label.text = Loc.T(russian);
        }
    }
}
