using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace FingersApp
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static void LocateUltraleapSdk()
        {
            // Support direct FingersApp.exe launches: GitHub build output does not ship
            // Ultraleap's native LeapC.dll. Windows must be able to find the installed SDK.
            string appDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LeapC.dll");
            if (File.Exists(appDll)) return;

            string sdkDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Ultraleap", "LeapSDK", "lib", "x64");
            if (File.Exists(Path.Combine(sdkDirectory, "LeapC.dll")))
                SetDllDirectory(sdkDirectory);
        }
        public App()
        {
            LocateUltraleapSdk();
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
