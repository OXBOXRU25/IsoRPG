using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;

// System.Windows.Shapes тоже содержит Path — фигуру, а не работу с путями.
// Без этой строки любое обращение к путям файлов становится двусмысленным.
using Path = System.IO.Path;

namespace HighFlyingBird.Launcher
{
    /// <summary>
    /// Окно лаунчера.
    ///
    /// Лаунчер, а не установщик: у игры с обновлениями установщик отрабатывает
    /// один раз и больше не нужен, а лаунчер живёт всё время — он показывает,
    /// что изменилось, и приносит патчи. Для будущей игры вдвоём он же станет
    /// местом, где выбирают сервер.
    ///
    /// Раскладка повторяет привычную по другим играм: узкая панель разделов
    /// слева, баннер сверху, кнопка запуска в левом нижнем углу. Это не
    /// подражание — это то место, где рука ищет кнопку «Играть» не глядя.
    /// </summary>
    internal sealed class LauncherWindow : Window
    {
        private const double SidebarWidth = 84;
        private const double BannerHeight = 196;
        private const double FooterHeight = 92;

        /// <summary>
        /// Версия самого лаунчера. С версией игры не связана.
        ///
        /// Значение приходит из Launcher/CHANGELOG.md — файл BuildInfo.cs
        /// создаётся при сборке. Здесь оставлено имя, которым версия уже
        /// названа в нескольких местах.
        /// </summary>
        public const string LauncherVersion = BuildInfo.Version;

        private readonly GameFinder game = new GameFinder();
        private List<Release> releases;
        private readonly LauncherConfig config = LauncherConfig.Load();

        private ContentControl stage;
        private TextBlock statusText;
        private Border playButton;
        private TextBlock playLabel;

        /// <summary>Полоса хода скачивания. Появляется только на время работы.</summary>
        private Border progressTrack;
        private Border progressFill;

        /// <summary>Что известно про обновление. Пусто — обновляться не нужно.</summary>
        private UpdateInfo pendingUpdate;

        /// <summary>Свежая версия самого лаунчера, если она есть.</summary>
        private SelfUpdateInfo pendingSelf;

        /// <summary>
        /// Номер версии тот же, а файлы разошлись.
        ///
        /// Отдельно от обычного обновления только ради честной подписи на
        /// кнопке: человеку, у которого «уже установлена 0.4.0», слово
        /// «обновить» ничего не объясняет.
        /// </summary>
        private bool repairOnly;

        /// <summary>Какой раздел открыт — чтобы перерисовать его на месте.</summary>
        private Section current = Section.News;

        /// <summary>Идёт установка: второе нажатие кнопки не должно её начать заново.</summary>
        private bool installing;
        private readonly List<SidebarButton> tabs = new List<SidebarButton>();

        public LauncherWindow()
        {
            releases = Changelog.Load(FindChangelog());

            Title = "Приключения разбойника Жени";
            Width = 1000;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;

            // Растягивать нечего: содержимое рассчитано на одну ширину, а
            // окно, которое можно растянуть, обязано на это отвечать. Здесь
            // это была бы работа ради работы.
            ResizeMode = ResizeMode.CanMinimize;

            Background = Theme.BackgroundBrush;

            // До сборки содержимого: стиль кладётся в ресурсы окна, и его
            // подхватят все полосы прокрутки, включая будущие.
            ScrollStyle.Apply(this);

            Content = BuildRoot();

            Show(Section.News);
            UpdateStatus();

            // Проверку обновлений запускаем после того, как окно показано,
            // а не прямо здесь.
            //
            // Окно создаётся до Application.Run, и в конструкторе связь
            // с главным потоком ещё не установлена. Из-за этого код после
            // ожидания ответа сервера продолжался в потоке пула и падал
            // на первой же попытке тронуть надпись — «вызывающий поток не
            // может получить доступ к объекту». Лаунчер при этом закрывался
            // молча, не успевая ничего записать в журнал.
            //
            // Пока адрес обновлений был пустым, метод выходил на первой
            // строке и до сети не доходил — поэтому поломка вылезла только
            // тогда, когда сервер появился.
            Loaded += (sender, args) => CheckForUpdate();
            Loaded += (sender, args) => RefreshNews();
        }

        private enum Section { News, History, Settings }

        // ==================================================================
        //  Каркас
        // ==================================================================

        private UIElement BuildRoot()
        {
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SidebarWidth) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sidebar = BuildSidebar();
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BannerHeight) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FooterHeight) });

            var banner = BuildBanner();
            Grid.SetRow(banner, 0);
            right.Children.Add(banner);

            stage = new ContentControl { Margin = new Thickness(28, 20, 28, 8) };
            Grid.SetRow(stage, 1);
            right.Children.Add(stage);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            right.Children.Add(footer);

            Grid.SetColumn(right, 1);
            root.Children.Add(right);

            return root;
        }

        /// <summary>
        /// Панель разделов. Кнопок ровно столько, сколько у нас есть чем
        /// наполнить: пустой пункт «Форум», ведущий в никуда, обещает игроку
        /// то, чего нет, и это замечают быстрее, чем кажется.
        /// </summary>
        private UIElement BuildSidebar()
        {
            var panel = new Grid { Background = Theme.SidebarBrush };

            var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

            AddTab(stack, "Новости", Section.News, Icons.News);
            AddTab(stack, "История", Section.History, Icons.History);
            AddTab(stack, "Настройки", Section.Settings, Icons.Settings);

            panel.Children.Add(stack);

            // Тонкая линия справа отделяет панель от содержимого. Без неё две
            // тёмные поверхности сливаются, и панель читается как поле.
            var edge = new Rectangle
            {
                Width = 1,
                Fill = Theme.LineBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            panel.Children.Add(edge);

            return panel;
        }

        private void AddTab(Panel host, string caption, Section section,
                            Func<Brush, UIElement> icon)
        {
            var button = new SidebarButton(caption, icon);
            button.Clicked += () => Show(section);
            button.Section = section;

            tabs.Add(button);
            host.Children.Add(button);
        }

        /// <summary>
        /// Баннер: фоновый кадр из игры, затемнение и логотип.
        ///
        /// Логотип показываем крупно и один раз — это единственное место, где
        /// он уместен. Ниже начинается работа: новости, кнопка, версии.
        /// </summary>
        private UIElement BuildBanner()
        {
            var banner = new Grid { ClipToBounds = true };

            var art = LoadImage("background.jpg") ?? LoadImage("background.png");

            if (art != null)
            {
                banner.Children.Add(new Image
                {
                    Source = art,
                    Stretch = Stretch.UniformToFill,
                    // Кадр смещён вверх: в нижней трети любого игрового кадра
                    // обычно пусто, а нам нужна середина сцены.
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            banner.Children.Add(new Rectangle { Fill = Theme.BannerVeil() });

            var logo = LoadImage("logo.png");

            if (logo != null)
            {
                banner.Children.Add(new Image
                {
                    Source = logo,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 14, 0, 26),
                    MaxHeight = BannerHeight - 40,
                    Effect = Theme.SoftShadow(3, 18, 0.7),
                });
            }
            else
            {
                var fallback = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };

                var title = Theme.Label("Приключения разбойника Жени", 30,
                                        new SolidColorBrush(Theme.Gold), true);
                title.TextAlignment = TextAlignment.Center;
                fallback.Children.Add(title);

                var sub = Theme.Label("птицы высокого полёта", 15, Theme.DimBrush);
                sub.TextAlignment = TextAlignment.Center;
                sub.Margin = new Thickness(0, 4, 0, 0);
                fallback.Children.Add(sub);

                banner.Children.Add(fallback);
            }

            // Кнопки окна поверх баннера, как в играх: отдельной серой полосы
            // заголовка нет, картинка идёт до самого верха.
            banner.Children.Add(BuildWindowButtons());

            // Перетаскивание за баннер. Полосы заголовка нет, значит таскать
            // окно можно только за картинку — иначе его не сдвинуть вовсе.
            banner.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            return banner;
        }

        private UIElement BuildWindowButtons()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 8, 0),
            };

            row.Children.Add(new WindowButton(WindowButton.Kind.Minimise,
                                              () => WindowState = WindowState.Minimized));

            row.Children.Add(new WindowButton(WindowButton.Kind.Close, Close));

            return row;
        }

        /// <summary>Низ окна: кнопка запуска слева, версии справа.</summary>
        private UIElement BuildFooter()
        {
            var footer = new Grid { Margin = new Thickness(28, 0, 28, 22) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            playButton = BuildPlayButton();
            Grid.SetColumn(playButton, 0);
            footer.Children.Add(playButton);

            var middle = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
            };

            statusText = Theme.Label(string.Empty, 13, Theme.DimBrush);
            middle.Children.Add(statusText);

            // Полоса хода. Скрыта, пока ничего не качается: пустая полоска
            // в покое читается как «что-то не доделано».
            progressFill = new Border
            {
                Background = Theme.PlayFill(),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
            };

            progressTrack = new Border
            {
                Height = 4,
                Width = 320,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Theme.Line),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed,
                Child = progressFill,
            };

            middle.Children.Add(progressTrack);

            Grid.SetColumn(middle, 1);
            footer.Children.Add(middle);

            string gameStamp;

            if (!game.Found) gameStamp = "не найдена";
            else if (string.IsNullOrEmpty(game.InstalledVersion)) gameStamp = "без версии";
            else gameStamp = game.InstalledVersion;

            string versions = "Лаунчер " + LauncherVersion + "    Игра " + gameStamp;

            var stamp = Theme.Label(versions, 11, new SolidColorBrush(Theme.TextFaint));
            stamp.VerticalAlignment = VerticalAlignment.Bottom;
            stamp.Margin = new Thickness(0, 0, 0, 4);
            Grid.SetColumn(stamp, 2);
            footer.Children.Add(stamp);

            return footer;
        }

        private Border BuildPlayButton()
        {
            playLabel = Theme.Label("ИГРАТЬ", 19, new SolidColorBrush(Color.FromRgb(0x24, 0x18, 0x08)), true);
            playLabel.HorizontalAlignment = HorizontalAlignment.Center;
            playLabel.VerticalAlignment = VerticalAlignment.Center;

            var label = playLabel;

            var button = new Border
            {
                Width = 212,
                Height = 52,
                CornerRadius = new CornerRadius(4),
                Background = Theme.PlayFill(),
                Child = label,
                Cursor = Cursors.Hand,
                Effect = Theme.SoftShadow(3, 12, 0.5),
                VerticalAlignment = VerticalAlignment.Center,
            };

            button.MouseEnter += (s, e) => button.Opacity = 0.9;
            button.MouseLeave += (s, e) => button.Opacity = 1;
            button.MouseLeftButtonUp += (s, e) => OnMainButton();

            return button;
        }

        // ==================================================================
        //  Разделы
        // ==================================================================

        private void Show(Section section)
        {
            // Сравниваем через Equals: раздел хранится в кнопке как object,
            // а для object оператор == сверяет ссылки, и перечисление в
            // коробке никогда не совпадёт само с собой.
            current = section;

            foreach (var tab in tabs) tab.Active = Equals(tab.Section, section);

            switch (section)
            {
                case Section.News: stage.Content = BuildNews(); break;
                case Section.History: stage.Content = BuildHistory(); break;
                case Section.Settings: stage.Content = BuildSettings(); break;
            }
        }

        /// <summary>
        /// Новости — три последние версии карточками.
        ///
        /// Не весь список: лаунчер открывают, чтобы поиграть, а не читать. Три
        /// карточки помещаются в одну строку и охватывают то, что игрок мог
        /// пропустить с прошлого запуска.
        /// </summary>
        private UIElement BuildNews()
        {
            var page = new StackPanel();

            page.Children.Add(Heading(releases.Count > 0
                ? "Что нового в версии " + releases[0].Version
                : "Новостей пока нет"));

            if (releases.Count == 0)
            {
                page.Children.Add(Theme.Label(
                    "Рядом с лаунчером не нашёлся CHANGELOG.md — из него берётся " +
                    "история версий.", 13, Theme.DimBrush));

                return page;
            }

            var row = new UniformGrid
            {
                Rows = 1,
                Columns = Math.Min(3, releases.Count),
                Margin = new Thickness(0, 14, 0, 0),
            };

            for (int i = 0; i < Math.Min(3, releases.Count); i++)
                row.Children.Add(NewsCard(releases[i], i == 0));

            page.Children.Add(row);

            return page;
        }

        private UIElement NewsCard(Release release, bool latest)
        {
            var body = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

            var top = new StackPanel { Orientation = Orientation.Horizontal };

            top.Children.Add(Theme.Label(release.Version, 17,
                new SolidColorBrush(latest ? Theme.Gold : Theme.Text), true));

            if (latest)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Theme.PurpleDeep),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(7, 2, 7, 3),
                    Margin = new Thickness(9, 2, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = Theme.Label("свежая", 10,
                        new SolidColorBrush(Color.FromRgb(0xD8, 0xB8, 0xF0))),
                };

                top.Children.Add(badge);
            }

            body.Children.Add(top);

            if (!string.IsNullOrEmpty(release.Date))
            {
                var date = Theme.Label(release.Date, 11, new SolidColorBrush(Theme.TextFaint));
                date.Margin = new Thickness(0, 2, 0, 0);
                body.Children.Add(date);
            }

            if (!string.IsNullOrEmpty(release.Summary))
            {
                var summary = Theme.Label(release.Summary, 12.5, Theme.DimBrush);
                summary.Margin = new Thickness(0, 9, 0, 0);
                body.Children.Add(summary);
            }

            // Три пункта, не больше: карточка должна дразнить, а не пересказывать.
            int shown = 0;

            foreach (string item in release.AllItems)
            {
                if (shown >= 3) break;

                body.Children.Add(Bullet(item, 12));
                shown++;
            }

            int rest = release.ItemCount - shown;

            if (rest > 0)
            {
                var more = Theme.Label("и ещё " + rest + " " + WordFor(rest), 11.5,
                                       new SolidColorBrush(Theme.Gold));
                more.Margin = new Thickness(0, 8, 0, 0);
                body.Children.Add(more);
            }

            var card = new Border
            {
                Background = Theme.CardBrush,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 12, 0),
                Child = body,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(latest ? Theme.PurpleDeep : Theme.Line),
            };

            return card;
        }

        /// <summary>Полная история — прокручиваемый список всех версий.</summary>
        private UIElement BuildHistory()
        {
            var list = new StackPanel();

            list.Children.Add(Heading("История версий"));

            foreach (var release in releases)
            {
                var block = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(Theme.Label(release.Version, 16,
                    new SolidColorBrush(Theme.Gold), true));

                if (!string.IsNullOrEmpty(release.Date))
                {
                    var date = Theme.Label(release.Date, 12,
                        new SolidColorBrush(Theme.TextFaint));
                    date.Margin = new Thickness(10, 3, 0, 0);
                    head.Children.Add(date);
                }

                block.Children.Add(head);

                foreach (var section in release.Sections)
                {
                    if (!string.IsNullOrEmpty(section.Title))
                    {
                        var title = Theme.Label(section.Title, 12.5, Theme.TextBrush, true);
                        title.Margin = new Thickness(0, 10, 0, 4);
                        block.Children.Add(title);
                    }

                    foreach (string item in section.Items)
                        block.Children.Add(Bullet(item, 12.5));
                }

                list.Children.Add(block);
            }

            if (releases.Count == 0)
                list.Children.Add(Theme.Label("Файл истории не найден.", 13, Theme.DimBrush));

            return new ScrollViewer
            {
                Content = list,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 12, 0),
            };
        }

        private UIElement BuildSettings()
        {
            var page = new StackPanel();

            page.Children.Add(Heading("Настройки"));

            page.Children.Add(Row("Папка игры", game.Found
                ? Path.GetDirectoryName(game.ExecutablePath)
                : "не найдена — положи лаунчер рядом с игрой"));

            page.Children.Add(Row("Версия игры", string.IsNullOrEmpty(game.InstalledVersion)
                ? "неизвестна" : game.InstalledVersion));

            page.Children.Add(Row("Сохранения", GameFinder.SaveFolder));

            page.Children.Add(Row("Проверка обновлений", string.IsNullOrEmpty(config.UpdateUrl)
                ? "не настроена — адрес пишется в launcher.json"
                : config.UpdateUrl));

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 22, 0, 0),
            };

            buttons.Children.Add(SmallButton("Открыть папку игры", () =>
            {
                if (game.Found) OpenFolder(Path.GetDirectoryName(game.ExecutablePath));
            }));

            buttons.Children.Add(SmallButton("Открыть сохранения",
                                             () => OpenFolder(GameFinder.SaveFolder)));

            page.Children.Add(buttons);

            return page;
        }

        // ==================================================================
        //  Мелкие сборки
        // ==================================================================

        private static TextBlock Heading(string text)
        {
            var head = Theme.Label(text, 20, Theme.TextBrush, true);
            head.Margin = new Thickness(0, 0, 0, 2);
            return head;
        }

        private static UIElement Bullet(string text, double size)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(Theme.Gold),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                // Точку опускаем к середине первой строки, а не к её верху:
                // выровненная по верху коробки, она висит над буквами.
                Margin = new Thickness(1, size * 0.55, 0, 0),
            };

            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            var label = Theme.Label(text, size, Theme.DimBrush);
            label.LineHeight = size * 1.45;
            label.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            return row;
        }

        private static UIElement Row(string caption, string value)
        {
            var block = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            block.Children.Add(Theme.Label(caption, 12, new SolidColorBrush(Theme.TextFaint)));

            var text = Theme.Label(value, 13, Theme.TextBrush);
            text.Margin = new Thickness(0, 3, 0, 0);
            block.Children.Add(text);

            return block;
        }

        private static UIElement SmallButton(string caption, Action action)
        {
            var border = new Border
            {
                Background = Theme.CardBrush,
                BorderBrush = Theme.LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 9, 16, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = Theme.Label(caption, 12.5, Theme.TextBrush),
            };

            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Theme.CardHover);
            border.MouseLeave += (s, e) => border.Background = Theme.CardBrush;
            border.MouseLeftButtonUp += (s, e) => action();

            return border;
        }

        private static string WordFor(int count)
        {
            int tail = count % 100;
            if (tail >= 11 && tail <= 14) return "изменений";

            switch (count % 10)
            {
                case 1: return "изменение";
                case 2:
                case 3:
                case 4: return "изменения";
                default: return "изменений";
            }
        }

        // ==================================================================
        //  Действия
        // ==================================================================

        private void Play()
        {
            string error;

            if (game.Launch(out error))
            {
                // Прячем окно, а не закрываем: игра может не подняться, и тогда
                // человеку надо куда-то вернуться. Закрытие оставляет его перед
                // пустым рабочим столом без объяснений.
                WindowState = WindowState.Minimized;
                return;
            }

            statusText.Text = error;
            statusText.Foreground = new SolidColorBrush(Theme.Warn);
        }

        private void UpdateStatus()
        {
            if (!game.Found)
            {
                statusText.Text = "Игра не найдена рядом с лаунчером";
                statusText.Foreground = new SolidColorBrush(Theme.Warn);

                playButton.Opacity = 0.45;
                return;
            }

            statusText.Text = string.IsNullOrEmpty(game.InstalledVersion)
                ? "Готово к запуску"
                : "Установлена версия " + game.InstalledVersion;

            statusText.Foreground = Theme.DimBrush;
        }

        /// <summary>
        /// Спрашивает у сети, нет ли версии свежее.
        ///
        /// Пока адрес не настроен, метод молча ничего не делает: лаунчер должен
        /// работать и без интернета, и до того, как у игры появится сайт.
        /// </summary>
        private async void CheckForUpdate()
        {
            if (string.IsNullOrEmpty(config.UpdateUrl)) return;

            // Лаунчер обновляется первым.
            //
            // Порядок не косметический: правки в самом лаунчере — это и есть
            // правки в том, как он обновляет игру. Ставить игру старым
            // лаунчером значит чинить дорогу той же телегой, которая на ней
            // и застревает.
            if (await CheckSelfUpdate()) return;

            var info = await Updater.Check(config.UpdateUrl);
            if (!info.IsValid) return;

            string local = string.IsNullOrEmpty(game.InstalledVersion)
                ? "0.0.0" : game.InstalledVersion;

            if (Changelog.Compare(info.Version, local) <= 0)
            {
                await CheckGameFiles(local);
                return;
            }

            pendingUpdate = info;
            repairOnly = false;

            statusText.Text = "Нужно обновление до версии " + info.Version +
                              " — у тебя " + local;
            statusText.Foreground = new SolidColorBrush(Theme.Good);

            // Кнопка меняет назначение, а не появляется рядом второй.
            // Пока обновление не поставлено, играть нельзя: старая сборка
            // не сойдётся с сервером, когда игра станет сетевой, а до тех
            // пор просто не покажет того, что мы уже починили.
            playLabel.Text = "ОБНОВИТЬ";
        }

        /// <summary>
        /// Подтягивает историю версий с сервера.
        ///
        /// Рядом с лаунчером лежит своя копия, но она застывает в момент
        /// установки: игрок открывал лаунчер и видел новости позапрошлой
        /// версии, хотя игра у него уже свежая. Патч-ноты — ровно то, ради
        /// чего лаунчер и открывают, и брать их с сервера правильнее.
        ///
        /// Локальная копия остаётся запасным вариантом: без сети лаунчер
        /// показывает то, что знает, а не пустую страницу.
        /// </summary>
        private async void RefreshNews()
        {
            if (string.IsNullOrEmpty(config.UpdateUrl)) return;

            try
            {
                string url = config.UpdateUrl.Replace("update.json", "CHANGELOG.md");

                string markdown = await Net.DownloadString(url);
                if (string.IsNullOrEmpty(markdown)) return;

                var fresh = Changelog.Parse(markdown);
                if (fresh == null || fresh.Count == 0) return;

                releases = fresh;

                // Перерисовываем открытый раздел на месте: человек мог за это
                // время уйти в историю версий, и подменять ему страницу под
                // руками нельзя.
                // Статус трогать нельзя: рядом идёт проверка обновлений, и
                // она уже могла написать туда «нужно обновиться». Новости
                // меняют страницу, а не подпись у кнопки.
                Show(current);
            }
            catch (Exception error)
            {
                Log.Write("Новости с сервера не прочитались: " + error.Message);
            }
        }

        /// <summary>
        /// Нет ли версии лаунчера свежее. Вернёт true, если есть.
        /// </summary>
        private async Task<bool> CheckSelfUpdate()
        {
            string url = config.UpdateUrl.Replace("update.json", "launcher-update.json");

            var info = await SelfUpdate.Check(url);
            if (!info.IsValid) return false;

            if (Changelog.Compare(info.Version, LauncherVersion) <= 0) return false;

            pendingSelf = info;

            statusText.Text = "Новый лаунчер " + info.Version +
                              " — у тебя " + LauncherVersion;
            statusText.Foreground = new SolidColorBrush(Theme.Good);

            playLabel.Text = "ОБНОВИТЬ ЛАУНЧЕР";
            return true;
        }

        /// <summary>
        /// Сверяет состав игры со списком на сервере при том же номере версии.
        ///
        /// Номер версии обещает свежесть, но не доказывает её: исправление,
        /// выпущенное под прежним номером, для лаунчера выглядит как «ничего
        /// не изменилось». Сверка сумм отвечает на вопрос по существу — те ли
        /// файлы лежат на диске.
        /// </summary>
        private async Task CheckGameFiles(string local)
        {
            if (!game.Found) return;

            string filesUrl = config.UpdateUrl.Replace("update.json", "files.json");

            var list = await FileUpdater.Fetch(filesUrl);
            if (!list.IsValid || list.Version != local) return;

            string folder = Path.GetDirectoryName(game.ExecutablePath);

            // Сверка читает всю папку игры — это заметное время, и держать на
            // нём поток окна нельзя: лаунчер выглядел бы зависшим.
            int different = await Task.Run(() => FileUpdater.CountDifferent(list, folder));

            if (different == 0) return;

            Log.Write("Тот же номер " + local + ", а разошлось файлов: " + different);

            pendingUpdate = new UpdateInfo
            {
                Version = list.Version,
                Url = string.Empty,
                Sha256 = string.Empty,
            };

            repairOnly = true;

            statusText.Text = "Файлы игры разошлись с сервером — " + different +
                              (different == 1 ? " файл" : " файлов");
            statusText.Foreground = new SolidColorBrush(Theme.Warn);

            playLabel.Text = "ДОКАЧАТЬ";
        }

        /// <summary>Одна кнопка на три действия — по состоянию.</summary>
        private void OnMainButton()
        {
            if (installing) return;

            if (pendingSelf != null) InstallSelfUpdate();
            else if (pendingUpdate != null) InstallUpdate();
            else Play();
        }

        /// <summary>
        /// Ставит новую версию лаунчера и уходит с дороги.
        ///
        /// Дальше работает уже скачанная копия: она дожидается, пока это окно
        /// закроется, переносит файлы на место и запускает лаунчер снова.
        /// </summary>
        private async void InstallSelfUpdate()
        {
            installing = true;
            playButton.Opacity = 0.5;
            progressTrack.Visibility = Visibility.Visible;

            Action<string, double> show = (text, share) =>
            {
                statusText.Text = text;
                progressFill.Width = progressTrack.Width * Math.Max(0, Math.Min(1, share));
            };

            bool handedOver = await SelfUpdate.Launch(pendingSelf, show);

            if (!handedOver)
            {
                installing = false;
                playButton.Opacity = 1;
                progressTrack.Visibility = Visibility.Collapsed;
                statusText.Foreground = new SolidColorBrush(Theme.Warn);
                return;
            }

            statusText.Text = "Перезапускаюсь";
            Close();
        }

        private async void InstallUpdate()
        {
            if (!game.Found)
            {
                statusText.Text = "Игра не найдена — обновлять нечего";
                statusText.Foreground = new SolidColorBrush(Theme.Warn);
                return;
            }

            installing = true;
            playButton.Opacity = 0.5;
            progressTrack.Visibility = Visibility.Visible;

            string folder = Path.GetDirectoryName(game.ExecutablePath);

            Action<string, double> show = (text, share) =>
            {
                statusText.Text = text;
                progressFill.Width = progressTrack.Width * Math.Max(0, Math.Min(1, share));
            };

            // Сначала пробуем обновиться по одному файлу.
            //
            // Между соседними версиями меняется около десятка файлов из двух
            // сотен: движок в тридцать семь мегабайт не трогается месяцами,
            // а игрок скачивал его каждый раз. Список с суммами позволяет
            // взять только отличающееся — обычно меньше десятой части.
            //
            // Если списка нет или он не от той версии, откатываемся на целый
            // архив: обновиться игрок должен в любом случае.
            string filesUrl = config.UpdateUrl.Replace("update.json", "files.json");

            var list = await FileUpdater.Fetch(filesUrl);

            bool done;

            if (list.IsValid && list.Version == pendingUpdate.Version)
            {
                done = await FileUpdater.Install(list, folder, show);

                // Пофайловое могло сорваться на середине — тогда целый архив
                // приведёт папку в согласованное состояние.
                //
                // Кроме случая, когда архива и нет: при докачке разошедшихся
                // файлов номер версии прежний, скачивать заново шестьдесят
                // мегабайт ради нескольких файлов незачем, и адрес архива в
                // описании не заполняется.
                if (!done && pendingUpdate.Url.Length > 0)
                {
                    Log.Write("Пофайловое не удалось, беру архив целиком.");
                    done = await Updater.Install(pendingUpdate, folder, show);
                }
            }
            else if (pendingUpdate.Url.Length > 0)
            {
                done = await Updater.Install(pendingUpdate, folder, show);
            }
            else
            {
                // Докачивать нечем: списка нет, архива нет. Молча ничего не
                // делать здесь нельзя — кнопка выглядела бы сломанной.
                Log.Write("Список файлов не прочитался, а архива для этой версии нет.");
                show("Сервер не отдал список файлов — попробуй позже", 0);
                done = false;
            }

            installing = false;
            playButton.Opacity = 1;

            if (!done)
            {
                statusText.Foreground = new SolidColorBrush(Theme.Warn);
                progressTrack.Visibility = Visibility.Collapsed;
                return;
            }

            // Перечитываем версию с диска, а не верим той, что обещал сервер:
            // обновление могло встать наполовину, и тогда честнее показать то,
            // что есть на самом деле.
            game.Refresh();

            bool repaired = repairOnly;

            pendingUpdate = null;
            repairOnly = false;
            playLabel.Text = "ИГРАТЬ";
            progressTrack.Visibility = Visibility.Collapsed;

            statusText.Text = repaired
                ? "Файлы игры приведены в порядок, версия " + game.InstalledVersion
                : "Обновлено до версии " + game.InstalledVersion;
            statusText.Foreground = new SolidColorBrush(Theme.Good);
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception error)
            {
                Log.Write("Не открылась папка " + path + ": " + error.Message);
            }
        }

        // ==================================================================
        //  Ресурсы
        // ==================================================================

        private static string Home
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>
        /// Ищет историю версий рядом с лаунчером, а при разработке — в корне
        /// репозитория. Второе нужно, чтобы гонять окно не собирая пакет.
        /// </summary>
        private static string FindChangelog()
        {
            var candidates = new[]
            {
                Path.Combine(Home, "CHANGELOG.md"),
                Path.Combine(Home, "assets", "CHANGELOG.md"),
                Path.Combine(Home, "..", "CHANGELOG.md"),
                Path.Combine(Home, "..", "..", "CHANGELOG.md"),
                Path.Combine(Home, "..", "..", "..", "CHANGELOG.md"),
            };

            foreach (string candidate in candidates)
            {
                try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); }
                catch { }
            }

            return string.Empty;
        }

        private static BitmapImage LoadImage(string name)
        {
            var candidates = new[]
            {
                Path.Combine(Home, "assets", name),
                Path.Combine(Home, name),
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (!File.Exists(path)) continue;

                    var image = new BitmapImage();
                    image.BeginInit();

                    // Читаем в память целиком: иначе файл остаётся занятым, и
                    // обновление лаунчера не сможет его заменить.
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path);
                    image.EndInit();
                    image.Freeze();

                    return image;
                }
                catch (Exception error)
                {
                    Log.Write("Не загрузилась картинка " + path + ": " + error.Message);
                }
            }

            return null;
        }
    }
}
