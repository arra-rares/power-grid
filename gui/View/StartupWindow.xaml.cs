using System.Windows;
using gui.Model.Persistence;

namespace gui.View
{
    public partial class StartupWindow : Window
    {
        private readonly IReadOnlyList<SavedCheckpoint> _checkpoints;

        public bool StartNew { get; private set; }

        public SavedCheckpoint? Selected { get; private set; }

        public StartupWindow(IReadOnlyList<SavedCheckpoint> checkpoints)
        {
            _checkpoints = checkpoints;
            InitializeComponent();
            Checkpoints.ItemsSource = checkpoints;
            if (checkpoints.Count > 0)
                Checkpoints.SelectedIndex = 0;
        }

        private void NewGame_Click(object sender, RoutedEventArgs e)
        {
            StartNew = true;
            DialogResult = true;
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            Selected = Checkpoints.SelectedItem as SavedCheckpoint ?? _checkpoints.FirstOrDefault();
            if (Selected == null)
                return;

            StartNew = false;
            DialogResult = true;
        }
    }
}
