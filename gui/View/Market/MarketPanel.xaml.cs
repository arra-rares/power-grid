using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using gui.ViewModel.Market;

namespace gui.View.Market
{
    public partial class MarketPanel : UserControl
    {
        public MarketPanel()
        {
            InitializeComponent();
            DataContext = new MarketViewModel(); // Bind ViewModel
        }

        private void MarketPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
                return;

            MarketItems.LayoutTransform = Transform.Identity;
            MarketItems.Measure(new Size(e.NewSize.Width, double.PositiveInfinity));
            var needed = MarketItems.DesiredSize.Height;
            if (needed <= 0)
                return;

            var fit = needed > e.NewSize.Height ? e.NewSize.Height / needed : 1.0;
            MarketItems.LayoutTransform = fit < 0.999
                ? new ScaleTransform(fit, fit)
                : Transform.Identity;
        }
    }
}
