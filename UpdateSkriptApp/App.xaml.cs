using System.Windows;

namespace UpdateSkriptApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"Critical Error: {ex.Exception.Message}\n\nStack Trace: {ex.Exception.StackTrace}", 
                                "UpdateSkriptApp Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
                Shutdown();
            };

            // Global handler for non-UI threads
            System.AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                MessageBox.Show($"Background Error: {ex.ExceptionObject}", "UpdateSkriptApp Fatal Error");
            };
        }
    }
}
