using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22.Sheets;

public sealed record ActionEditResult(
    Guid Id,
    ActionKind Kind,
    string Name,
    string Target,
    string Arguments,
    string ProcessName,
    int DelaySeconds,
    bool IsEnabled,
    MusicSource MusicSource,
    WindowsTheme DesktopTheme);

public partial class ActionSheet : UserControl
{
    private readonly ScenarioAction? _existing;
    private bool _loadedOnce;
    private string _closeIconPath;

    public ActionSheet(ScenarioAction? existing)
    {
        InitializeComponent();
        _existing = existing;
        _closeIconPath = existing?.Target ?? string.Empty;

        TitleText.Text = existing is null ? Strings.T("L_NewActionTitle") : Strings.T("L_EditActionTitle");
        SaveButton.Content = existing is null ? Strings.T("L_Add") : Strings.T("L_Save");
        NameBox.Text = existing?.Name ?? string.Empty;
        TargetBox.Text = existing?.Target ?? string.Empty;
        ArgumentsBox.Text = existing?.Arguments ?? string.Empty;
        ProcessBox.Text = existing?.ProcessName ?? string.Empty;
        MusicTargetBox.Text = existing?.Target ?? string.Empty;
        YouTubeBox.Text = existing?.Target ?? string.Empty;
        WallpaperBox.Text = existing?.Target ?? string.Empty;
        DelayBox.Text = (existing?.DelaySeconds ?? 0).ToString();

        var kind = existing?.Kind ?? ActionKind.Launch;
        LaunchChoice.IsChecked = kind == ActionKind.Launch;
        CloseChoice.IsChecked = kind == ActionKind.Close;
        MusicChoice.IsChecked = kind == ActionKind.Music;
        YouTubeChoice.IsChecked = kind == ActionKind.YouTube;
        WallpaperChoice.IsChecked = kind == ActionKind.Wallpaper;
        ThemeChoice.IsChecked = kind == ActionKind.Theme;

        var source = existing?.MusicSource ?? MusicSource.Spotify;
        SpotifyChoice.IsChecked = source == MusicSource.Spotify;
        YandexMusicChoice.IsChecked = source == MusicSource.YandexMusic;
        SoundCloudChoice.IsChecked = source == MusicSource.SoundCloud;
        YouTubeMusicChoice.IsChecked = source == MusicSource.YouTubeMusic;

        var theme = existing?.DesktopTheme ?? WindowsTheme.Dark;
        ThemeLightChoice.IsChecked = theme == WindowsTheme.Light;
        ThemeDarkChoice.IsChecked = theme == WindowsTheme.Dark;

        UpdateKindUi();
        HideError();

        foreach (var box in new[]
                 {
                     NameBox, TargetBox, ArgumentsBox, ProcessBox, MusicTargetBox,
                     YouTubeBox, WallpaperBox, DelayBox
                 })
        {
            box.TextChanged += (_, _) => HideError();
        }

        Loaded += OnLoaded;
    }

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    public ActionEditResult? Result { get; private set; }

    private ActionKind SelectedKind
    {
        get
        {
            if (CloseChoice.IsChecked == true)
            {
                return ActionKind.Close;
            }

            if (MusicChoice.IsChecked == true)
            {
                return ActionKind.Music;
            }

            if (YouTubeChoice.IsChecked == true)
            {
                return ActionKind.YouTube;
            }

            if (WallpaperChoice.IsChecked == true)
            {
                return ActionKind.Wallpaper;
            }

            if (ThemeChoice.IsChecked == true)
            {
                return ActionKind.Theme;
            }

            return ActionKind.Launch;
        }
    }

    private MusicSource SelectedMusicSource
    {
        get
        {
            if (YandexMusicChoice.IsChecked == true)
            {
                return MusicSource.YandexMusic;
            }

            if (SoundCloudChoice.IsChecked == true)
            {
                return MusicSource.SoundCloud;
            }

            if (YouTubeMusicChoice.IsChecked == true)
            {
                return MusicSource.YouTubeMusic;
            }

            return MusicSource.Spotify;
        }
    }

    private WindowsTheme SelectedDesktopTheme =>
        ThemeLightChoice.IsChecked == true ? WindowsTheme.Light : WindowsTheme.Dark;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    private void ActionKind_Checked(object sender, RoutedEventArgs e)
    {
        UpdateKindUi();
        HideError();
    }

    private void UpdateKindUi()
    {
        if (LaunchPanel is null || ClosePanel is null)
        {
            return;
        }

        var kind = SelectedKind;
        LaunchPanel.Visibility = kind == ActionKind.Launch ? Visibility.Visible : Visibility.Collapsed;
        ClosePanel.Visibility = kind == ActionKind.Close ? Visibility.Visible : Visibility.Collapsed;
        MusicPanel.Visibility = kind == ActionKind.Music ? Visibility.Visible : Visibility.Collapsed;
        YouTubePanel.Visibility = kind == ActionKind.YouTube ? Visibility.Visible : Visibility.Collapsed;
        WallpaperPanel.Visibility = kind == ActionKind.Wallpaper ? Visibility.Visible : Visibility.Collapsed;
        ThemePanel.Visibility = kind == ActionKind.Theme ? Visibility.Visible : Visibility.Collapsed;

        SubtitleText.Text = Strings.T(kind switch
        {
            ActionKind.Launch => "L_SubLaunch",
            ActionKind.Close => "L_SubClose",
            ActionKind.Music => "L_SubMusic",
            ActionKind.YouTube => "L_SubYouTube",
            ActionKind.Wallpaper => "L_SubWallpaper",
            _ => "L_SubTheme"
        });

        NameBox.Tag = Strings.T(kind switch
        {
            ActionKind.Launch => "L_NamePhSteam",
            ActionKind.Close => "L_NamePhChrome",
            ActionKind.Music => "L_NamePhMusic",
            ActionKind.YouTube => "L_NamePhYouTube",
            ActionKind.Wallpaper => "L_NamePhWallpaper",
            _ => "L_NamePhTheme"
        });
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("L_FileDialogTarget"),
            Filter = Strings.T("L_FilterApps"),
            CheckFileExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return;
        }

        TargetBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void BrowseLaunchProcess_Click(object sender, RoutedEventArgs e)
    {
        PickRunningProcess(forClosing: false);
    }

    private void BrowseProcess_Click(object sender, RoutedEventArgs e)
    {
        PickRunningProcess(forClosing: true);
    }

    private void BrowseProcessFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("L_FileDialogProcess"),
            Filter = Strings.T("L_FilterProcess"),
            CheckFileExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return;
        }

        ApplyCloseTarget(dialog.FileName, ScenarioAction.NormalizeProcessName(dialog.FileName));
    }

    private void BrowseWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("L_FileDialogWallpaper"),
            Filter = Strings.T("L_FilterWallpaper"),
            CheckFileExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return;
        }

        WallpaperBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void PickRunningProcess(bool forClosing)
    {
        var picker = new ProcessPickerWindow(forClosing);
        var owner = Window.GetWindow(this);
        if (owner is not null)
        {
            picker.Owner = owner;
        }

        if (picker.ShowDialog() != true)
        {
            return;
        }

        if (forClosing)
        {
            ApplyCloseTarget(picker.ProcessPath, picker.ProcessName);
            return;
        }

        if (string.IsNullOrWhiteSpace(picker.ProcessPath))
        {
            return;
        }

        TargetBox.Text = picker.ProcessPath;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = Path.GetFileNameWithoutExtension(picker.ProcessPath);
        }
    }

    private void ApplyCloseTarget(string iconPath, string processName)
    {
        _closeIconPath = iconPath;
        ProcessBox.Text = processName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = processName;
        }
    }

    private bool ShowDialog(OpenFileDialog dialog)
    {
        var owner = Window.GetWindow(this);
        return owner is null ? dialog.ShowDialog() == true : dialog.ShowDialog(owner) == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var delayText = DelayBox.Text.Trim();
        if (!string.IsNullOrEmpty(delayText) && !int.TryParse(delayText, out _))
        {
            ShowError(Strings.T("L_ErrDelayInt"));
            DelayBox.Focus();
            return;
        }

        var delay = string.IsNullOrEmpty(delayText) ? 0 : int.Parse(delayText);
        if (delay is < 0 or > 600)
        {
            ShowError(Strings.T("L_ErrDelayRange"));
            DelayBox.Focus();
            return;
        }

        switch (SelectedKind)
        {
            case ActionKind.Close:
                SaveClose(delay);
                break;

            case ActionKind.Music:
                SaveMusic(delay);
                break;

            case ActionKind.YouTube:
                SaveYouTube(delay);
                break;

            case ActionKind.Wallpaper:
                SaveWallpaper(delay);
                break;

            case ActionKind.Theme:
                SaveTheme(delay);
                break;

            default:
                SaveLaunch(delay);
                break;
        }

        if (Result is not null)
        {
            Saved?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SaveLaunch(int delay)
    {
        var target = TargetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ShowError(Strings.T("L_ErrTarget"));
            TargetBox.Focus();
            return;
        }

        Result = CreateResult(
            ActionKind.Launch,
            string.IsNullOrWhiteSpace(NameBox.Text) ? DeriveName(target) : NameBox.Text.Trim(),
            target,
            ArgumentsBox.Text.Trim(),
            string.Empty,
            delay);
    }

    private void SaveClose(int delay)
    {
        var process = ScenarioAction.NormalizeProcessName(ProcessBox.Text);
        if (string.IsNullOrWhiteSpace(process))
        {
            ShowError(Strings.T("L_ErrProcess"));
            ProcessBox.Focus();
            return;
        }

        if (ProcessSafety.IsProtected(process))
        {
            ShowError(Strings.T("L_ErrProtected"));
            ProcessBox.Focus();
            return;
        }

        var iconPath = _closeIconPath;
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            try
            {
                if (!Path.GetFileNameWithoutExtension(iconPath).Equals(process, StringComparison.OrdinalIgnoreCase))
                {
                    iconPath = string.Empty;
                }
            }
            catch
            {
                iconPath = string.Empty;
            }
        }

        Result = CreateResult(
            ActionKind.Close,
            string.IsNullOrWhiteSpace(NameBox.Text) ? process : NameBox.Text.Trim(),
            iconPath,
            string.Empty,
            process,
            delay);
    }

    private void SaveMusic(int delay)
    {
        var target = MusicTargetBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ShowError(Strings.T("L_ErrMusicTarget"));
            MusicTargetBox.Focus();
            return;
        }

        var source = SelectedMusicSource;
        Result = CreateResult(
            ActionKind.Music,
            string.IsNullOrWhiteSpace(NameBox.Text) ? DeriveMusicName(source, target) : NameBox.Text.Trim(),
            target,
            string.Empty,
            string.Empty,
            delay);
    }

    private void SaveYouTube(int delay)
    {
        var target = YouTubeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ShowError(Strings.T("L_ErrYouTube"));
            YouTubeBox.Focus();
            return;
        }

        Result = CreateResult(
            ActionKind.YouTube,
            string.IsNullOrWhiteSpace(NameBox.Text) ? DeriveYouTubeName(target) : NameBox.Text.Trim(),
            target,
            string.Empty,
            string.Empty,
            delay);
    }

    private void SaveWallpaper(int delay)
    {
        var target = WallpaperBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            ShowError(Strings.T("L_ErrWallpaper"));
            WallpaperBox.Focus();
            return;
        }

        var extension = Path.GetExtension(target);
        if (!extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            ShowError(Strings.T("L_ErrWallpaperFormat"));
            WallpaperBox.Focus();
            return;
        }

        Result = CreateResult(
            ActionKind.Wallpaper,
            string.IsNullOrWhiteSpace(NameBox.Text) ? Path.GetFileNameWithoutExtension(target) : NameBox.Text.Trim(),
            target,
            string.Empty,
            string.Empty,
            delay);
    }

    private void SaveTheme(int delay)
    {
        var theme = SelectedDesktopTheme;
        Result = CreateResult(
            ActionKind.Theme,
            string.IsNullOrWhiteSpace(NameBox.Text)
                ? Strings.T(theme == WindowsTheme.Light ? "L_ThemeLight" : "L_ThemeDark")
                : NameBox.Text.Trim(),
            string.Empty,
            string.Empty,
            string.Empty,
            delay);
    }

    private ActionEditResult CreateResult(
        ActionKind kind,
        string name,
        string target,
        string arguments,
        string processName,
        int delay)
    {
        return new ActionEditResult(
            _existing?.Id ?? Guid.NewGuid(),
            kind,
            name,
            target,
            arguments,
            processName,
            delay,
            _existing?.IsEnabled ?? true,
            SelectedMusicSource,
            SelectedDesktopTheme);
    }

    private static string DeriveMusicName(MusicSource source, string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return Strings.T(source switch
            {
                MusicSource.Spotify => "L_MusicSpotify",
                MusicSource.YandexMusic => "L_MusicYandex",
                MusicSource.SoundCloud => "L_MusicSoundCloud",
                _ => "L_MusicYouTube"
            });
        }

        return Truncate(target, 60);
    }

    private static string DeriveYouTubeName(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return "YouTube";
        }

        return Truncate(target, 60);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string DeriveName(string target)
    {
        try
        {
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return string.IsNullOrWhiteSpace(uri.Host) ? target : uri.Host;
            }

            var name = Path.GetFileNameWithoutExtension(target);
            return string.IsNullOrWhiteSpace(name) ? target : name;
        }
        catch
        {
            return target;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            Save_Click(sender, e);
        }
    }

    private void DelayBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        if (ErrorBanner is not null)
        {
            ErrorBanner.Visibility = Visibility.Collapsed;
        }
    }
}
