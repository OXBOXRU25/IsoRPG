using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Тонкая полоса прокрутки вместо системной.
    ///
    /// Системная полоса в WPF светло-серая и с кнопками-стрелками по краям —
    /// она осталась из времён, когда так выглядела вся Windows. На тёмном окне
    /// это единственный элемент, выкрашенный чужой рукой, и глаз находит его
    /// первым.
    ///
    /// Собирается разбором XAML в строке, а не сборкой объектов. Причина в
    /// том, что шаблон элемента управления — дерево из десятка узлов со
    /// связями между ними; собранный кодом, он занимает страницу и читается
    /// как ребус. Здесь же видно ровно то, что получится.
    ///
    /// В XAML внутри используются одинарные кавычки: тогда в C#-строке не
    /// появляется ни одной экранированной кавычки, и правка шаблона не
    /// превращается в подсчёт слешей.
    /// </summary>
    internal static class ScrollStyle
    {
        /// <summary>
        /// Вешает стиль на всё окно. Ключ — тип элемента, поэтому стиль
        /// подхватывают все полосы прокрутки внутри, включая те, что появятся
        /// позже вместе с новым содержимым.
        /// </summary>
        public static void Apply(Window window)
        {
            try
            {
                var style = (Style)XamlReader.Parse(Xaml());
                window.Resources.Add(typeof(ScrollBar), style);
            }
            catch (Exception error)
            {
                // Не смогли — остаётся системная полоса. Некрасиво, но окно
                // открывается: внешний вид не повод не пустить человека в игру.
                Log.Write("Не применился стиль прокрутки: " + error.Message);
            }
        }

        private static string Xaml()
        {
            string thumb = Hex(Theme.Line);
            string thumbHover = Hex(Theme.CardHover);
            string thumbDrag = Hex(Theme.Purple);

            var lines = new[]
            {
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'",
                "       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'",
                "       TargetType='ScrollBar'>",
                "  <Setter Property='Width' Value='10'/>",
                "  <Setter Property='Background' Value='Transparent'/>",
                "  <Setter Property='Template'>",
                "    <Setter.Value>",
                "      <ControlTemplate TargetType='ScrollBar'>",
                "        <Grid Background='{TemplateBinding Background}'>",
                "          <Track x:Name='PART_Track' IsDirectionReversed='True'>",
                "            <Track.Thumb>",
                "              <Thumb>",
                "                <Thumb.Template>",
                "                  <ControlTemplate TargetType='Thumb'>",
                // Ползунок уже самой полосы: поля по бокам дают ему воздух,
                // иначе он читается как вторая граница окна.
                "                    <Border x:Name='piece' CornerRadius='3'",
                "                            Margin='3,2,3,2'",
                "                            Background='" + thumb + "'/>",
                "                    <ControlTemplate.Triggers>",
                "                      <Trigger Property='IsMouseOver' Value='True'>",
                "                        <Setter TargetName='piece' Property='Background'",
                "                                Value='" + thumbHover + "'/>",
                "                      </Trigger>",
                "                      <Trigger Property='IsDragging' Value='True'>",
                "                        <Setter TargetName='piece' Property='Background'",
                "                                Value='" + thumbDrag + "'/>",
                "                      </Trigger>",
                "                    </ControlTemplate.Triggers>",
                "                  </ControlTemplate>",
                "                </Thumb.Template>",
                "              </Thumb>",
                "            </Track.Thumb>",
                // Кнопки листания страницами остаются — это клик по пустой
                // части полосы. Прозрачные, но не выключенные: убрать их
                // значит отнять привычное действие ради вида.
                "            <Track.IncreaseRepeatButton>",
                "              <RepeatButton Command='ScrollBar.PageDownCommand'",
                "                            Focusable='False'>",
                "                <RepeatButton.Template>",
                "                  <ControlTemplate TargetType='RepeatButton'>",
                "                    <Border Background='Transparent'/>",
                "                  </ControlTemplate>",
                "                </RepeatButton.Template>",
                "              </RepeatButton>",
                "            </Track.IncreaseRepeatButton>",
                "            <Track.DecreaseRepeatButton>",
                "              <RepeatButton Command='ScrollBar.PageUpCommand'",
                "                            Focusable='False'>",
                "                <RepeatButton.Template>",
                "                  <ControlTemplate TargetType='RepeatButton'>",
                "                    <Border Background='Transparent'/>",
                "                  </ControlTemplate>",
                "                </RepeatButton.Template>",
                "              </RepeatButton>",
                "            </Track.DecreaseRepeatButton>",
                "          </Track>",
                "        </Grid>",
                "      </ControlTemplate>",
                "    </Setter.Value>",
                "  </Setter>",
                "</Style>",
            };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Цвет в вид «#RRGGBB» — XAML понимает именно такую запись.</summary>
        private static string Hex(System.Windows.Media.Color colour)
        {
            return "#" + colour.R.ToString("X2") +
                         colour.G.ToString("X2") +
                         colour.B.ToString("X2");
        }
    }
}
