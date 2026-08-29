using System;
using System.Windows;

namespace HighFlyingBird.Launcher
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Второй режим работы: поставить себя на место старой версии.
            //
            // Сюда программа попадает, когда её запустил ПРЕДЫДУЩИЙ лаунчер,
            // скачавший обновление: заменить собственный файл работающая
            // программа не может, поэтому замену делает новая копия. Окна в
            // этом режиме нет вовсе — оно откроется через секунду уже на
            // установленном месте.
            if (args != null && args.Length >= 2 && args[0] == SelfUpdate.ApplyFlag)
            {
                int waitFor;
                int.TryParse(args.Length > 2 ? args[2] : "0", out waitFor);

                SelfUpdate.Apply(args[1], waitFor);
                return;
            }

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
            application.DispatcherUnhandledException += (sender, failure) =>
            {
                Log.Write("Необработанная ошибка: " + failure.Exception);

                MessageBox.Show(
                    "Лаунчер споткнулся: " + failure.Exception.Message +
                    Environment.NewLine + Environment.NewLine +
                    "Подробности записаны в launcher.log рядом с программой.",
                    "Приключения разбойника Жени",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                failure.Handled = true;
            };

            application.Run(new LauncherWindow());
        }
    }
}
