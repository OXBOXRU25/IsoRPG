using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Кнопка раздела в левой панели: значок и подпись под ним.
    ///
    /// Своя, а не стандартная Button, по одной причине: у стандартной есть
    /// оформление Windows, и перекрасить его — значит написать шаблон, то есть
    /// ту же разметку, только через XAML. Кнопка тут — прямоугольник, который
    /// реагирует на мышь, и честнее собрать её из прямоугольника.
    /// </summary>
    internal sealed class SidebarButton : Border
    {
        public event Action Clicked;

        /// <summary>К какому разделу ведёт. Хранится здесь, чтобы окно не
        /// заводило параллельный список соответствий.</summary>
        public object Section;

        private readonly TextBlock caption;
        private readonly ContentControl glyph;
        private readonly Rectangle marker;
        private bool active;

        public SidebarButton(string text, Func<Brush, UIElement> icon)
        {
            Height = 74;
            Cursor = Cursors.Hand;
            Background = Brushes.Transparent;

            var body = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            glyph = new ContentControl
            {
                Content = icon(new SolidColorBrush(Theme.TextDim)),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            body.Children.Add(glyph);

            caption = Theme.Label(text, 10.5, new SolidColorBrush(Theme.TextDim));
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(caption);

            var host = new Grid();
            host.Children.Add(body);

            // Полоска слева у выбранного пункта. Заливка всей кнопки читалась
            // бы как нажатие, а полоска — как «ты здесь».
            marker = new Rectangle
            {
                Width = 3,
                Fill = new SolidColorBrush(Theme.Gold),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Hidden,
            };

            host.Children.Add(marker);

            Child = host;

            MouseEnter += (s, e) => { if (!active) Paint(Theme.Text); };
            MouseLeave += (s, e) => { if (!active) Paint(Theme.TextDim); };
            MouseLeftButtonUp += (s, e) => { var handler = Clicked; if (handler != null) handler(); };
        }

        public bool Active
        {
            get { return active; }
            set
            {
                active = value;

                marker.Visibility = value ? Visibility.Visible : Visibility.Hidden;
                Background = value
                    ? new SolidColorBrush(Color.FromRgb(0x1A, 0x16, 0x22))
                    : Brushes.Transparent;

                Paint(value ? Theme.Gold : Theme.TextDim);
            }
        }

        private void Paint(Color colour)
        {
            var brush = new SolidColorBrush(colour);

            caption.Foreground = brush;

            // Значок перерисовываем целиком: он собран из фигур со своей
            // заливкой, и перекрасить его «сверху» нечем.
            var icon = glyph.Content as UIElement;
            if (icon != null) Icons.Repaint(icon, brush);
        }
    }

    /// <summary>Свернуть и закрыть. Рисуются линиями, без шрифтовых символов.</summary>
    internal sealed class WindowButton : Border
    {
        public enum Kind { Minimise, Close }

        public WindowButton(Kind kind, Action action)
        {
            Width = 34;
            Height = 28;
            Cursor = Cursors.Hand;
            Background = Brushes.Transparent;
            CornerRadius = new CornerRadius(3);

            var stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xEC, 0xE8, 0xF2));
            var canvas = new Canvas { Width = 34, Height = 28 };

            if (kind == Kind.Minimise)
            {
                canvas.Children.Add(Line(12, 15, 22, 15, stroke));
            }
            else
            {
                canvas.Children.Add(Line(12, 10, 22, 20, stroke));
                canvas.Children.Add(Line(22, 10, 12, 20, stroke));
            }

            Child = canvas;

            // Закрытие краснеет, сворачивание светлеет. Разный отклик на
            // одинаковые с виду кнопки — самый дешёвый способ не дать
            // промахнуться по той, что закрывает.
            var hover = kind == Kind.Close
                ? Color.FromRgb(0xC0, 0x39, 0x2B)
                : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);

            MouseEnter += (s, e) => Background = new SolidColorBrush(hover);
            MouseLeave += (s, e) => Background = Brushes.Transparent;

            // Нажатие гасим здесь же. Иначе оно всплывает до баннера, тот
            // на нажатии начинает тащить окно, забирает мышь в свой цикл —
            // и отпускания кнопка уже не увидит. Со стороны выглядит как
            // мёртвая кнопка, хотя обработчик на месте.
            MouseLeftButtonDown += (s, e) => e.Handled = true;

            MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                action();
            };
        }

        private static Line Line(double x1, double y1, double x2, double y2, Brush stroke)
        {
            return new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke,
                StrokeThickness = 1.4,
                SnapsToDevicePixels = true,
            };
        }
    }

    /// <summary>
    /// Значки разделов.
    ///
    /// Собраны из прямоугольников и кругов, а не взяты картинками. Значок в
    /// 22 пикселя — это десяток пикселей на смысл, и растровая иконка на
    /// экране с другим масштабом мылится; фигуры остаются чёткими на любом.
    /// </summary>
    internal static class Icons
    {
        private const double Size = 22;

        public static UIElement News(Brush colour)
        {
            var canvas = NewCanvas();

            canvas.Children.Add(Box(2, 3, 18, 16, colour));
            canvas.Children.Add(Bar(5, 7, 9, colour));
            canvas.Children.Add(Bar(5, 11, 12, colour));
            canvas.Children.Add(Bar(5, 15, 7, colour));

            return canvas;
        }

        public static UIElement History(Brush colour)
        {
            var canvas = NewCanvas();

            var circle = new Ellipse
            {
                Width = 17, Height = 17,
                Stroke = colour,
                StrokeThickness = 1.5,
            };

            Canvas.SetLeft(circle, 2.5);
            Canvas.SetTop(circle, 2.5);
            canvas.Children.Add(circle);

            // Стрелки часов: без них круг читается как что угодно круглое.
            canvas.Children.Add(Stick(11, 11, 11, 6.5, colour));
            canvas.Children.Add(Stick(11, 11, 14.5, 12.5, colour));

            return canvas;
        }

        public static UIElement Settings(Brush colour)
        {
            var canvas = NewCanvas();

            // Три ползунка: шестерёнку в этом размере не нарисовать линиями
            // так, чтобы она осталась шестерёнкой.
            for (int i = 0; i < 3; i++)
            {
                double y = 5 + i * 6;

                canvas.Children.Add(Bar(2, y, 18, colour));

                var knob = new Ellipse
                {
                    Width = 5, Height = 5,
                    Fill = colour,
                };

                Canvas.SetLeft(knob, i == 1 ? 12 : 5);
                Canvas.SetTop(knob, y - 2);
                canvas.Children.Add(knob);
            }

            return canvas;
        }

        /// <summary>Перекрашивает готовый значок — фигуры внутри держат свою заливку.</summary>
        public static void Repaint(UIElement icon, Brush colour)
        {
            var canvas = icon as Canvas;
            if (canvas == null) return;

            foreach (UIElement child in canvas.Children)
            {
                var shape = child as Shape;
                if (shape == null) continue;

                if (shape.Fill != null) shape.Fill = colour;
                if (shape.Stroke != null) shape.Stroke = colour;
            }
        }

        private static Canvas NewCanvas()
        {
            return new Canvas { Width = Size, Height = Size };
        }

        private static Shape Box(double x, double y, double w, double h, Brush colour)
        {
            var box = new Rectangle
            {
                Width = w, Height = h,
                Stroke = colour,
                StrokeThickness = 1.5,
                RadiusX = 2, RadiusY = 2,
            };

            Canvas.SetLeft(box, x);
            Canvas.SetTop(box, y);

            return box;
        }

        private static Shape Bar(double x, double y, double w, Brush colour)
        {
            var bar = new Rectangle
            {
                Width = w, Height = 1.6,
                Fill = colour,
                RadiusX = 0.8, RadiusY = 0.8,
            };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);

            return bar;
        }

        private static Shape Stick(double x1, double y1, double x2, double y2, Brush colour)
        {
            return new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = colour,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
        }
    }
}
