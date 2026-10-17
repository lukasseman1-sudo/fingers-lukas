using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace FingersApp
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnUiException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => WriteCrash(e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                WriteCrash(e.Exception);
                e.SetObserved();
            };
        }

        private static void WriteCrash(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FingersMSFS");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "startup_errors.txt"),
                    DateTime.Now.ToString("O") + Environment.NewLine +
                    (ex == null ? "Unknown fatal error" : ex.ToString()) +
                    Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }

        private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrash(e.Exception);
            MessageBox.Show("Chyba pri spusteni Fingers. Pozri %APPDATA%\\FingersMSFS\\startup_errors.txt\n\n" +
                e.Exception.Message, "Fingers", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
