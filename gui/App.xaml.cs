using gui.ViewModel.LogWindow;
using Serilog;
using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;


namespace gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static LogPanelViewModel LogPanelViewModel { get; } = new();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Helpers.UiScale.EnsureInitialized();

            // Attach console for logging
            AllocConsole();

            // Initialize Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}][{Level:u3}][Thread:{ThreadId}][{filePath}::{memberName}:{lineNumber}] {Message}{NewLine}{Exception}")
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Logging initialized.");

        }
    }

}
