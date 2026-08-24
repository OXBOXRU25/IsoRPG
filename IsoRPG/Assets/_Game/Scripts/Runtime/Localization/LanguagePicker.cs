using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IsoRPG.Localization
{
    /// <summary>
    /// Выбор языка: три кнопки в ряд, выбранная подсвечена.
    ///
    /// Ряд, а не выпадающий список: языков три, и все они помещаются на
    /// экране разом. Список прятал бы выбор за лишним нажатием и заодно
    /// требовал бы объяснения, что за язык там сейчас стоит.
    ///
    /// Названия языков написаны на них самих — «English», «Українська».
    /// Человек, зашедший в игру не на своём языке, ищет глазами знакомое
    /// слово, а не перевод названия на язык, которого не знает.
    /// </summary>
    public sealed class LanguagePicker : MonoBehaviour
    {
        private static readonly Language[] Order =
        {
            Language.Russian, Language.English, Language.Ukrainian,
        };

        private static readonly Color Idle = new Color32(0x2A, 0x27, 0x21, 0xFF);
        private static readonly Color Active = new Color32(0x6B, 0x5A, 0x36, 0xFF);
        private static readonly Color IdleText = new Color32(0xB0, 0xA8, 0x96, 0xFF);
        private static readonly Color ActiveText = new Color32(0xF0, 0xE2, 0xB8, 0xFF);

        private readonly List<Image> plates = new List<Image>();
        private readonly List<Text> labels = new List<Text>();

        /// <summary>
        /// Собирает ряд кнопок внутри переданной области.
        ///
        /// Размеры считаются от ширины области, а не задаются числами: этот
        /// же ряд стоит и в меню, и в окне настроек, а там разная ширина.
        /// </summary>
        public static LanguagePicker Attach(RectTransform parent, Font font,
                                            float width, float height = 30f)
        {
            var go = new GameObject("LanguagePicker", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height);

            var picker = go.AddComponent<LanguagePicker>();
            picker.Build(rect, font, width, height);

            return picker;
        }

        private void Build(RectTransform root, Font font, float width, float height)
        {
            const float Gap = 6f;
            float cell = (width - Gap * (Order.Length - 1)) / Order.Length;

            for (int i = 0; i < Order.Length; i++)
            {
                var language = Order[i];

                var go = new GameObject("Lang_" + language, typeof(Image), typeof(Button));
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, false);

                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(i * (cell + Gap), 0f);
                rect.sizeDelta = new Vector2(cell, height);

                var plate = go.GetComponent<Image>();
                plate.color = Idle;
                plates.Add(plate);

                var textGo = new GameObject("Label", typeof(Text));
                var textRect = (RectTransform)textGo.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var label = textGo.GetComponent<Text>();
                label.font = font;
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleCenter;
                label.text = Loc.NameOf(language);
                label.color = IdleText;
                label.raycastTarget = false;
                labels.Add(label);

                // Обработчик здесь НЕ вешаем.
                //
                // Обычный слушатель живёт в памяти и в сцену не пишется.
                // Сборщик работает в редакторе, сцена сохраняется — и
                // сохраняется с пустыми обработчиками. В редакторе это
                // незаметно, потому что сцена ещё в памяти; в собранной
                // игре кнопки оказываются мёртвыми.
                //
                // Поэтому подписываемся при запуске, в Awake: компонент к
                // тому моменту уже в сцене вместе со своими кнопками.
            }

            Highlight();
        }

        private void Awake()
        {
            Subscribe();
        }

        /// <summary>
        /// Находит свои кнопки и вешает на них выбор языка.
        ///
        /// Ищем по дочерним объектам, а не храним ссылки: список ссылок
        /// тоже пришлось бы сохранять в сцену, и он разошёлся бы с тем,
        /// что там на самом деле, при первой же пересборке меню.
        /// </summary>
        private void Subscribe()
        {
            plates.Clear();
            labels.Clear();

            for (int i = 0; i < Order.Length; i++)
            {
                var child = transform.Find("Lang_" + Order[i]);
                if (child == null) continue;

                var plate = child.GetComponent<Image>();
                if (plate != null) plates.Add(plate);

                var label = child.GetComponentInChildren<Text>();
                if (label != null) labels.Add(label);

                var button = child.GetComponent<Button>();
                if (button == null) continue;

                var captured = Order[i];

                // На всякий случай снимаем прежние: при пересборке меню
                // в редакторе компонент мог что-то унаследовать.
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Choose(captured));
            }

            Highlight();
        }

        private void Choose(Language language)
        {
            Loc.Set(language);
            Highlight();

            IsoRPG.Audio.Sfx.OpenWindow();
        }

        private void OnEnable()
        {
            Loc.Changed += Highlight;
            Highlight();
        }

        private void OnDisable()
        {
            Loc.Changed -= Highlight;
        }

        private void Highlight()
        {
            for (int i = 0; i < Order.Length && i < plates.Count; i++)
            {
                bool chosen = Order[i] == Loc.Current;

                if (plates[i] != null) plates[i].color = chosen ? Active : Idle;
                if (labels[i] != null) labels[i].color = chosen ? ActiveText : IdleText;
            }
        }
    }
}
