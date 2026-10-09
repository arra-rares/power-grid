using gui.Helpers;
using gui.Model.Managers.InfoManager;
using gui.ViewModel;
using Serilog;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

public class InfoViewModel : ViewModelBase
{
    public InfoViewModel()
    {
        // Subscribe to InfoManager changes
        InfoManager.Instance.PropertyChanged += InfoManagerChanged;
        ToggleClockCommand = new RelayCommand(_ => ToggleClock());
        SetClockCommand = new RelayCommand(_ => PromptClock());
        _clockSeconds = LoadClockSeconds();
        _countdown.Interval = TimeSpan.FromSeconds(1);
        _countdown.Tick += (_, _) => TickClock();

        // Initialize properties with current InfoManager values
        _phaseName = InfoManager.Instance.PhaseName;
        _playerName = InfoManager.Instance.PlayerName;
        _optionA = InfoManager.Instance.OptionA;
        _optionB = InfoManager.Instance.OptionB;
        _optionC = InfoManager.Instance.OptionC;
        _optionD = InfoManager.Instance.OptionD;
    }

    private void InfoManagerChanged(object? sender, PropertyChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(InfoManager.Instance.PhaseName): PhaseName = InfoManager.Instance.PhaseName; break;
                case nameof(InfoManager.Instance.PlayerName): PlayerName = InfoManager.Instance.PlayerName; break;
                case nameof(InfoManager.Instance.OptionA): OptionA = InfoManager.Instance.OptionA; break;
                case nameof(InfoManager.Instance.OptionB): OptionB = InfoManager.Instance.OptionB; break;
                case nameof(InfoManager.Instance.OptionC): OptionC = InfoManager.Instance.OptionC; break;
                case nameof(InfoManager.Instance.OptionD): OptionD = InfoManager.Instance.OptionD; break;
            }
        });
    }

    private string _phaseName;
    private string _playerName;
    private string _optionA;
    private string _optionB;
    private string _optionC;
    private string _optionD;

    public string PhaseName
    {
        get => _phaseName;
        set => SetProperty(ref _phaseName, value);
    }

    public string PlayerName
    {
        get => _playerName;
        set => SetProperty(ref _playerName, value);
    }

    public string OptionA
    {
        get => _optionA;
        set => SetProperty(ref _optionA, value);
    }

    public string OptionB
    {
        get => _optionB;
        set => SetProperty(ref _optionB, value);
    }

    public string OptionC
    {
        get => _optionC;
        set => SetProperty(ref _optionC, value);
    }

    public string OptionD
    {
        get => _optionD;
        set => SetProperty(ref _optionD, value);
    }

    private const int DefaultClockSeconds = 300;

    private static string ClockFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PowerGrid",
        "clock.txt");

    private readonly DispatcherTimer _countdown = new();
    private int _clockSeconds;
    private int _remaining;
    private bool _running;
    private bool _expired;

    public ICommand ToggleClockCommand { get; }

    public ICommand SetClockCommand { get; }

    public string ClockButtonText =>
        _running || _expired
            ? TimeSpan.FromSeconds(Math.Max(_remaining, 0)).ToString(@"m\:ss")
            : "Clock";

    public Brush ClockBrush => _expired ? Brushes.Tomato : Brushes.White;

    private void ToggleClock()
    {
        if (_running)
        {
            ResetClock();
            return;
        }

        _remaining = _clockSeconds;
        _running = true;
        _expired = false;
        NotifyClock();
        _countdown.Start();
    }

    private void PromptClock()
    {
        var box = new TextBox
        {
            Text = _clockSeconds.ToString(),
            FontSize = 28,
            Margin = new Thickness(16),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        var set = new Button
        {
            Content = new TextBlock
            {
                Text = "Set",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0),
                Foreground = Brushes.White
            },
            IsDefault = true,
            Height = 48,
            Margin = new Thickness(16, 0, 16, 16)
        };
        var panel = new DockPanel();
        DockPanel.SetDock(set, Dock.Bottom);
        panel.Children.Add(set);
        panel.Children.Add(box);

        var window = new Window
        {
            Title = "Set clock",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Application.Current.Resources["PrimaryBrush"],
            Content = panel
        };
        set.Click += (_, _) => window.DialogResult = true;

        if (window.ShowDialog() != true)
            return;
        if (!int.TryParse(box.Text.Trim(), out var seconds) || seconds <= 0)
            return;

        _clockSeconds = seconds;
        SaveClockSeconds(seconds);
    }

    private void ResetClock()
    {
        _countdown.Stop();
        _running = false;
        _expired = false;
        _remaining = 0;
        NotifyClock();
    }

    private void TickClock()
    {
        if (_remaining > 0)
            _remaining--;

        if (_remaining == 0)
        {
            _countdown.Stop();
            _running = false;
            _expired = true;
        }

        NotifyClock();
    }

    private void NotifyClock()
    {
        OnPropertyChanged(nameof(ClockButtonText));
        OnPropertyChanged(nameof(ClockBrush));
        CommandManager.InvalidateRequerySuggested();
    }

    private static int LoadClockSeconds()
    {
        try
        {
            if (File.Exists(ClockFile)
                && int.TryParse(File.ReadAllText(ClockFile).Trim(), out var seconds)
                && seconds > 0)
                return seconds;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read {ClockFile}", ClockFile);
        }

        return DefaultClockSeconds;
    }

    private static void SaveClockSeconds(int seconds)
    {
        var directory = Path.GetDirectoryName(ClockFile)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(ClockFile, seconds.ToString());
    }
}
