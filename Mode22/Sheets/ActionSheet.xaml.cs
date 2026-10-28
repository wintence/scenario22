using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Mode22.Models;
using Mode22.Services;

namespace Mode22.Sheets;

public sealed record ActionEditResult(
    Guid Id,
    ActionKind Kind,
    string Name,
    string Target,
    string Arguments,
    string ProcessName,
    int DelaySeconds,
    bool IsEnabled);

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
        DelayBox.Text = (existing?.DelaySeconds ?? 0).ToString();

        if (existing?.Kind == ActionKind.Close)
        {
            CloseChoice.IsChecked = true;
        }
        else
        {
            LaunchChoice.IsChecked = true;
        }

        UpdateKindUi();
        HideError();

        foreach (var box in new[] { NameBox, TargetBox, ArgumentsBox, ProcessBox, DelayBox })
        {
            box.TextChanged += (_, _) => HideError();
        }

        Loaded += OnLoaded;
    }

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;

    public ActionEditResult? Result { get; private set; }

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

        var isClose = CloseChoice.IsChecked == true;
        LaunchPanel.Visibility = isClose ? Visibility.Collapsed : Visibility.Visible;
        ClosePanel.Visibility = isClose ? Visibility.Visible : Visibility.Collapsed;
        SubtitleText.Text = isClose
            ? Strings.T("L_SubClose")
            : Strings.T("L_SubLaunch");
        NameBox.Tag = isClose ? Strings.T("L_NamePhChrome") : Strings.T("L_NamePhSteam");
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

    private void BrowseProcess_Click(object sender, RoutedEventArgs e)
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

        _closeIconPath = dialog.FileName;
        var process = ScenarioAction.NormalizeProcessName(dialog.FileName);
        ProcessBox.Text = process;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = process;
        }
    }

    private bool ShowDialog(OpenFileDialog dialog)
    {
        var owner = Window.GetWindow(this);
        return owner is null ? dialog.ShowDialog() == true : dialog.ShowDialog(owner) == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var isClose = CloseChoice.IsChecked == true;
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

        if (isClose)
        {
            var process = ScenarioAction.NormalizeProcessName(ProcessBox.Text);
            if (string.IsNullOrWhiteSpace(process))
            {
                ShowError(Strings.T("L_ErrProcess"));
                ProcessBox.Focus();
                return;
            }

            var name = string.IsNullOrWhiteSpace(NameBox.Text) ? process : NameBox.Text.Trim();
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

            Result = new ActionEditResult(
                _existing?.Id ?? Guid.NewGuid(),
                ActionKind.Close,
                name,
                iconPath,
                string.Empty,
                process,
                delay,
                _existing?.IsEnabled ?? true);
        }
        else
        {
            var target = TargetBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                ShowError(Strings.T("L_ErrTarget"));
                TargetBox.Focus();
                return;
            }

            var name = string.IsNullOrWhiteSpace(NameBox.Text)
                ? DeriveName(target)
                : NameBox.Text.Trim();

            Result = new ActionEditResult(
                _existing?.Id ?? Guid.NewGuid(),
                ActionKind.Launch,
                name,
                target,
                ArgumentsBox.Text.Trim(),
                string.Empty,
                delay,
                _existing?.IsEnabled ?? true);
        }

        Saved?.Invoke(this, EventArgs.Empty);
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
