using System.Windows;
using System.Windows.Input;
using gui.Helpers;
using gui.Model.Managers.CardManager;
using gui.Model.Managers.InfoManager;
using gui.Model.Managers.InputManager;
using gui.Model.Managers.RemoteManager;
using gui.Model;
using gui.ViewModel.CardEditor;

namespace gui
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public static CardEditorViewModel CardEditorVM { get; private set; } = new();

        private bool _isFullscreen;
        private WindowStyle _previousStyle;
        private WindowState _previousState;
        private ResizeMode _previousResizeMode;

        public MainWindow()
        {
            var gm = GameManager.Instance;

            Instance = this;

            InitializeComponent();

            var im = InfoManager.Instance;

            InputManager.Instance.Start();
            InputManager.Instance.BtnPressed += RemoteManager.Instance.OnButtonPressed;
            InputManager.Instance.CardScanned += CardManager.Instance.OnCardScanned;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UiScale.EnsureInitialized();
            UiScale.Update(ActualWidth, ActualHeight);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UiScale.Update(e.NewSize.Width, e.NewSize.Height);
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _isFullscreen)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        }

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                _previousStyle = WindowStyle;
                _previousState = WindowState;
                _previousResizeMode = ResizeMode;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                _isFullscreen = true;
            }
            else
            {
                WindowStyle = _previousStyle;
                ResizeMode = _previousResizeMode;
                WindowState = _previousState;
                _isFullscreen = false;
            }
        }
    }
}
