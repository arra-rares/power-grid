using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace gui.View.LogPanel
{
    /// <summary>
    /// Interaction logic for LogPanelView.xaml
    /// </summary>
    public partial class LogPanel : UserControl
    {
        public LogPanel()
        {
            InitializeComponent();
            DataContext = App.LogPanelViewModel; // assuming singleton
            App.LogPanelViewModel.PropertyChanged += (_, _) =>
            {
                if (LogListBox.Items.Count == 0)
                    return;

                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            };
        }

        private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new LogPanelDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog(); // or .Show() for non-blocking
        }
    }
}
