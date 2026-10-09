using System;
using System.Windows;

namespace gui.Helpers
{
    /// <summary>
    /// Updates shared DynamicResource sizes from the window's DIP size
    /// relative to the 2560×1440 design baseline. Layout still uses Grids;
    /// this only scales typography and fixed chrome.
    /// </summary>
    public static class UiScale
    {
        public const double DesignWidth = 2560;
        public const double DesignHeight = 1440;
        public const double MinScale = 0.75;
        public const double MaxScale = 1.5;

        public static double Current { get; private set; } = 1.0;

        public static void Update(double widthDip, double heightDip)
        {
            if (widthDip <= 0 || heightDip <= 0)
                return;

            double scale = Math.Min(widthDip / DesignWidth, heightDip / DesignHeight);
            scale = Math.Clamp(scale, MinScale, MaxScale);

            // Ignore tiny float jitter from repeated SizeChanged events.
            if (Math.Abs(scale - Current) < 0.01)
                return;

            Current = scale;
            ApplyResources(scale);
        }

        public static void EnsureInitialized()
        {
            if (Application.Current?.Resources.Contains("FontSizeDefault") == true)
                return;

            ApplyResources(Current);
        }

        private static void ApplyResources(double scale)
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
                return;

            resources["UiScale"] = scale;

            resources["FontSizeSmall"] = 18 * scale;
            resources["FontSizeDefault"] = 40 * scale;
            resources["FontSizeMedium"] = 60 * scale;
            resources["FontSizeLarge"] = 80 * scale;
            resources["FontSizeDialogHeading"] = 24 * scale;

            resources["ResourceButtonWidth"] = 50 * scale;
            resources["ResourceButtonHeight"] = 55 * scale;
            resources["ResourceRowHeight"] = 62 * scale;

            resources["SupplyCellWidth"] = 120 * scale;
            resources["SupplyIconWidth"] = 125 * scale;
            resources["SupplyIconHeight"] = 120 * scale;

            resources["PhaseImageHeight"] = 100 * scale;
            resources["PhaseButtonWidth"] = 400 * scale;
            resources["PhaseButtonHeight"] = 100 * scale;
            resources["BureaucracyButtonWidth"] = 300 * scale;

            resources["SpacingTight"] = 5 * scale;
            resources["SpacingDefault"] = 10 * scale;
        }
    }
}
