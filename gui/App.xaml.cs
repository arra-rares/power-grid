using gui.Model;
using gui.Model.Managers.CardManager;
using gui.Model.Managers.InputManager;
using gui.Model.Persistence;
using gui.View;
using gui.ViewModel.LogWindow;
using Serilog;
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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var store = new CheckpointStore();
            var checkpoints = store.LoadValid(CardManager.Instance.Contains);
            PhaseKind? resumePhase = null;

            if (checkpoints.Count == 0)
            {
                GameManager.Instance.NewGame();
            }
            else
            {
                var dialog = new StartupWindow(checkpoints);
                var accepted = dialog.ShowDialog() == true;
                if (!accepted)
                {
                    Shutdown();
                    return;
                }

                if (dialog.StartNew || dialog.Selected == null)
                {
                    GameManager.Instance.NewGame();
                }
                else
                {
                    store.RetireNewerThan(dialog.Selected.Generation);
                    dialog.Selected.Snapshot.Apply();
                    resumePhase = dialog.Selected.Snapshot.PhaseKind;
                }
            }

            var window = new MainWindow();
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();

            if (!InputManager.Instance.Start())
            {
                MessageBox.Show(
                    "UDP port 8081 is already in use. The game is open, but button and card input is disabled.",
                    "Power Grid");
            }

            if (resumePhase != null)
                GameManager.Instance.StartGame(resumePhase);
        }
    }

}
