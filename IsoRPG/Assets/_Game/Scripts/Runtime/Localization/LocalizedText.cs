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

        /// <summary>
        /// Оригинал задан явно через Setup, пусть даже пустой.
        ///
        /// Различать это обязательно. Подписи в подсказках берутся из пула и
        /// показываются повторно, и пустая подпись означает «здесь ничего не
        /// пиши», а не «оригинал забыли». Без этого различия строка молча
        /// оставляла текст от прошлого показа: в левой колонке висело
        /// «Стоимость» от предыдущего приёма и накладывалось на пояснение,
        /// которое в такой строке прижимается влево.
        /// </summary>
        private bool configured;

        public void Setup(string original)
        {
            russian = original == null ? string.Empty : original;
            configured = true;
            Apply();
        }

        /// <summary>
        /// Ставит подпись и запоминает её оригинал.
        ///
        /// Разница с простым переводом на месте: там строка переводится один
        /// раз, в момент постройки окна, и остаётся на прежнем языке до тех
        /// пор, пока окно не пересоберут. Человек, переключивший язык при
        /// открытой сумке, видел бы прежние надписи и решил, что кнопка не
        /// работает, — а она работает.
        ///
        /// Здесь подпись помнит свой русский оригинал и переводит себя заново
        /// при каждой смене языка. Компонент заводится один раз и остаётся
        /// на объекте.
        /// </summary>
        public static void Bind(Text label, string russian)
        {
            if (label == null) return;

            var holder = label.GetComponent<LocalizedText>();
            if (holder == null) holder = label.gameObject.AddComponent<LocalizedText>();

            holder.Setup(russian);
        }

        private void Awake()
        {
            label = GetComponent<Text>();

            // Если оригинал не задали явно, берём то, что стоит в подписи
            // сейчас: при сборке там русский текст.
            if (!configured && string.IsNullOrEmpty(russian) && label != null)
            {
                russian = label.text;
            }
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
            if (label == null) return;

            // Пустой оригинал — это пустая подпись, а не «нечего делать».
            label.text = string.IsNullOrEmpty(russian) ? string.Empty : Loc.T(russian);
        }
    }
}
