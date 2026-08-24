using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Палитра и заготовки элементов.
    ///
    /// Цвета сняты с логотипа игры, а не подобраны заново: лаунчер — первое,
    /// что видит игрок, и если он выкрашен в свои цвета, игра начинается с
    /// ощущения, что это две разные программы.
    ///
    /// Всё собирается кодом, без XAML. Причина простая: XAML требует сборки
    /// через msbuild с генерацией промежуточных файлов, а код компилируется
    /// одним вызовом системного компилятора, который есть в любой Windows.
    /// Лаунчер из-за этого весит сотню килобайт и не тянет за собой рантайм.
    /// </summary>
    internal static class Theme
    {
        // --- Цвета ---------------------------------------------------------

        /// <summary>Подложка всего окна. С фиолетовым уклоном, как знамёна на логотипе.</summary>
        public static readonly Color Background = Rgb(0x14, 0x12, 0x1A);

        /// <summary>Боковая панель — на тон темнее, чтобы читалась как отдельный слой.</summary>
        public static readonly Color Sidebar = Rgb(0x0F, 0x0D, 0x14);

        /// <summary>Карточка новости.</summary>
        public static readonly Color Card = Rgb(0x1F, 0x1B, 0x29);
        public static readonly Color CardHover = Rgb(0x2A, 0x24, 0x38);

        /// <summary>Золото с вывески логотипа. Главный акцент.</summary>
        public static readonly Color Gold = Rgb(0xE8, 0xA9, 0x3A);
        public static readonly Color GoldBright = Rgb(0xF5, 0xC6, 0x63);
        public static readonly Color GoldDark = Rgb(0xB8, 0x7F, 0x22);

        /// <summary>Фиолетовый со знамён — второй акцент, для выделенного пункта.</summary>
        public static readonly Color Purple = Rgb(0x7B, 0x3F, 0xA0);
        public static readonly Color PurpleDeep = Rgb(0x4A, 0x25, 0x63);

        public static readonly Color Text = Rgb(0xEC, 0xE8, 0xF2);
        public static readonly Color TextDim = Rgb(0x9B, 0x93, 0xA8);
        public static readonly Color TextFaint = Rgb(0x6B, 0x64, 0x78);

        public static readonly Color Line = Rgb(0x2C, 0x26, 0x38);
        public static readonly Color Good = Rgb(0x5F, 0xB8, 0x6A);
        public static readonly Color Warn = Rgb(0xD9, 0x7B, 0x3C);

        // --- Кисти ---------------------------------------------------------

        public static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Background));
        public static readonly Brush SidebarBrush = Freeze(new SolidColorBrush(Sidebar));
        public static readonly Brush CardBrush = Freeze(new SolidColorBrush(Card));
        public static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Gold));
        public static readonly Brush TextBrush = Freeze(new SolidColorBrush(Text));
        public static readonly Brush DimBrush = Freeze(new SolidColorBrush(TextDim));
        public static readonly Brush FaintBrush = Freeze(new SolidColorBrush(TextFaint));
        public static readonly Brush LineBrush = Freeze(new SolidColorBrush(Line));

        /// <summary>Заливка кнопки «Играть» — снизу темнее, как металл на логотипе.</summary>
        public static Brush PlayFill()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            };

            brush.GradientStops.Add(new GradientStop(GoldBright, 0));
            brush.GradientStops.Add(new GradientStop(Gold, 0.55));
            brush.GradientStops.Add(new GradientStop(GoldDark, 1));

            return Freeze(brush);
        }

        /// <summary>
        /// Затемнение поверх фонового арта.
        ///
        /// Без него текст поверх картинки нечитаем — не потому, что картинка
        /// светлая, а потому, что она пёстрая: буква попадает то на камень, то
        /// на траву, и контраст скачет вдоль строки.
        /// </summary>
        public static Brush BannerVeil()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            };

            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 0x14, 0x12, 0x1A), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xB0, 0x14, 0x12, 0x1A), 0.55));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x14, 0x12, 0x1A), 1));

            return Freeze(brush);
        }

        // --- Шрифты --------------------------------------------------------

        public static readonly FontFamily Face = new FontFamily("Segoe UI");
        public static readonly FontFamily FaceSemi = new FontFamily("Segoe UI Semibold");

        // --- Сборка элементов ----------------------------------------------

        public static TextBlock Label(string text, double size, Brush colour,
                                      bool semibold = false)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = semibold ? FaceSemi : Face,
                FontSize = size,
                Foreground = colour,
                TextWrapping = TextWrapping.Wrap,
                // Разрядку не ставим нигде: она читается как чужая привычка и
                // раздувает всё, что обжимает текст.
            };
        }

        /// <summary>
        /// Прямоугольник со скруглением. В WPF нет «панели с радиусом» —
        /// её роль играет Border, поэтому фабрика, а не свойство.
        /// </summary>
        public static Border Panel(Brush fill, double radius = 6)
        {
            return new Border
            {
                Background = fill,
                CornerRadius = new CornerRadius(radius),
            };
        }

        public static DropShadowEffect SoftShadow(double depth = 4, double blur = 14,
                                                  double opacity = 0.55)
        {
            return new DropShadowEffect
            {
                ShadowDepth = depth,
                BlurRadius = blur,
                Direction = 270,
                Color = Colors.Black,
                Opacity = opacity,
            };
        }

        // --- Мелочь --------------------------------------------------------

        private static Color Rgb(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// Замораживает кисть. Незамороженная кисть, использованная в разных
        /// местах, тянет за собой уведомления об изменениях и мешает WPF
        /// переиспользовать её — на десятке элементов это незаметно, но
        /// привычка полезная.
        /// </summary>
        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }
    }
}
