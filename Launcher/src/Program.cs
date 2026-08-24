using System;
using System.Windows;

namespace HighFlyingBird.Launcher
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            var application = new Application
            {
                // Закрылось единственное окно — закрылась программа. Без этого
                // процесс лаунчера остаётся висеть в памяти после закрытия
                // окна, и второй запуск открывает второе окно поверх первого.
                ShutdownMode = ShutdownMode.OnLastWindowClose,
            };

            // Падение в окне без обработчика показывает системный отчёт об
            // ошибке — игроку он не говорит ничего. Своё сообщение хотя бы
            // называет, куда смотреть, и оставляет след в файле.
            application.DispatcherUnhandledException += (sender, args) =>
            {
                Log.Write("Необработанная ошибка: " + args.Exception);

                MessageBox.Show(
                    "Лаунчер споткнулся: " + args.Exception.Message +
                    Environment.NewLine + Environment.NewLine +
                    "Подробности записаны в launcher.log рядом с программой.",
                    "Приключения разбойника Жени",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                args.Handled = true;
            };

            application.Run(new LauncherWindow());
        }
    }
}
