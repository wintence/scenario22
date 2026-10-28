using System.Collections.ObjectModel;

namespace Scenario22.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public ObservableCollection<Scenario> Scenarios { get; set; } = new();
    public AppPreferences Preferences { get; set; } = new();
    public Guid? SelectedScenarioId { get; set; }
}

public sealed class AppPreferences : ObservableObject
{
    private bool _startWithWindows;
    private bool _minimizeToTray = true;
    private bool _showNotifications = true;
    private bool _hotkeysEnabled = true;
    private bool _useDarkTheme;
    private bool _trayHintShown;
    private string _language = "en";

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set => SetProperty(ref _showNotifications, value);
    }

    public bool HotkeysEnabled
    {
        get => _hotkeysEnabled;
        set => SetProperty(ref _hotkeysEnabled, value);
    }

    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set => SetProperty(ref _useDarkTheme, value);
    }

    public bool TrayHintShown
    {
        get => _trayHintShown;
        set => SetProperty(ref _trayHintShown, value);
    }

    /// <summary>
    /// Interface language: "en" (default) or "ru".
    /// </summary>
    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }
}
