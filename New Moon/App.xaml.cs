using System;
using System.Windows;
using System.Windows.Threading;

namespace P3FESTrainer
{
    public partial class App : Application
    {
        public App()
        {
            // Global exception handlers to catch unhandled errors
            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(e.Exception.ToString(), "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show((e.ExceptionObject as Exception)?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown error",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
