using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Scenario22.Controls;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22.Sheets;

public sealed record ScenarioEditResult(
    Guid Id,
    string Name,
    string Description,
    string IconKey,
    string? CustomIconPath,
    string AccentKey,
    string? CustomAccentFrom,
    string? CustomAccentTo,
    Hotkey? Hotkey,
    bool HotkeyPassthrough);

public partial class ScenarioSheet : UserControl
{
    private readonly Scenario? _existing;
    private readonly IReadOnlyList<Scenario> _allScenarios;
    private readonly Guid _scenarioId;
    private bool _loadedOnce;
    private bool _pickingAccent;
    private string? _customIconPath;
    private string? _customFrom;
    private string? _customTo;
    private AccentOption? _lastAccentOption;

    public ScenarioSheet(Scenario? existing, IReadOnlyList<Scenario> allScenarios)
    {
        InitializeComponent();
        _existing = existing;
        _allScenarios = allScenarios;
        _scenarioId = existing?.Id ?? Guid.NewGuid();

        TitleText.Text = existing is null ? Strings.T("L_NewScenario") : Strings.T("L_EditScenarioTitle");
        DeleteButton.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;

        var hasCustomAccent = existing is { IsCustomAccent: true };
        _customFrom = hasCustomAccent ? existing!.CustomAccentFrom : null;
        _customTo = hasCustomAccent ? existing!.CustomAccentTo : null;

        AccentListBox.ItemsSource = BuildAccentOptions();
        AccentListBox.SelectedItem = hasCustomAccent
            ? AccentPalette.CreateCustom(_customFrom, _customTo)
            : AccentPalette.Get(existing?.AccentKey);
        _lastAccentOption = hasCustomAccent
            ? AccentPalette.Default
            : AccentListBox.SelectedItem as AccentOption ?? AccentPalette.Default;

        IconListBox.ItemsSource = ScenarioIcons.Options;
        IconListBox.SelectedItem = string.IsNullOrWhiteSpace(existing?.CustomIconPath)
            ? ScenarioIcons.Get(existing?.IconKey)
            : null;

        NameBox.Text = existing?.Name ?? Strings.T("L_NewScenario");
        DescriptionBox.Text = existing?.Description ?? string.Empty;
        HotkeyInput.Value = existing?.Hotkey ?? FindFreeHotkey();
        PassthroughToggle.IsChecked = existing?.HotkeyPassthrough ?? false;
        UpdatePassthroughState();

        Loaded += OnLoaded;
        HotkeyInput.HotkeyChanged += (_, _) =>
        {
            HideError();
            UpdatePassthroughState();
        };
        NameBox.TextChanged += (_, _) => HideError();
        UpdatePreview();
    }

    public event EventHandler? Saved;
    public event EventHandler? Cancelled;
    public event EventHandler? DeleteRequested;

    public ScenarioEditResult? Result { get; private set; }

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

    private Hotkey? FindFreeHotkey()
    {
        var used = _allScenarios
            .Where(x => x.Hotkey is { IsEmpty: false })
            .Select(x => x.Hotkey!)
            .ToList();

        for (var i = 1; i <= 9; i++)
        {
            var candidate = Hotkey.Create(
                ModifierKeys.Control | ModifierKeys.Alt,
                0x30 + i);
            if (!used.Any(x => x.Equals(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    private void Passthrough_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePassthroughState();
    }

    /// <summary>
    /// The pass-through option only makes sense together with a hotkey.
    /// </summary>
    private void UpdatePassthroughState()
    {
        if (PassthroughToggle is null)
        {
            return;
        }

        PassthroughToggle.IsEnabled = HotkeyInput.Value is { IsEmpty: false };
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_pickingAccent)
        {
            return;
        }

        if (sender == IconListBox)
        {
            if (IconListBox.SelectedItem is not null)
            {
                _customIconPath = null;
            }
        }
        else if (sender == AccentListBox)
        {
            if (AccentListBox.SelectedItem is not AccentOption accent)
            {
                return;
            }

            if (AccentPalette.IsCustom(accent.Key))
            {
                if (_customFrom is null)
                {
                    PickCustomColors();
                }

                return;
            }

            _lastAccentOption = accent;
        }

        UpdatePreview();
        HideError();
    }

    private IReadOnlyList<AccentOption> BuildAccentOptions()
    {
        var options = AccentPalette.Options.ToList();
        options.Add(AccentPalette.CreateCustom(_customFrom, _customTo));
        return options;
    }

    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (_pickingAccent)
        {
            return;
        }

        PickCustomColors();
    }

    private void PickCustomColors()
    {
        _pickingAccent = true;
        try
        {
            var from = AccentPalette.TryParseColor(_customFrom, out var parsedFrom)
                ? parsedFrom
                : AccentPalette.PlaceholderFrom;
            var to = AccentPalette.TryParseColor(_customTo, out var parsedTo) ? parsedTo : from;

            var owner = Window.GetWindow(this);
            var picker = owner is null
                ? new ColorPickerWindow(from, to)
                : new ColorPickerWindow(from, to) { Owner = owner };
            if (picker.ShowDialog() != true)
            {
                AccentListBox.SelectedItem = _lastAccentOption;
                return;
            }

            _customFrom = picker.FromHex;
            _customTo = picker.ToHex;
        }
        finally
        {
            _pickingAccent = false;
        }

        AccentListBox.ItemsSource = BuildAccentOptions();
        AccentListBox.SelectedItem = AccentPalette.CreateCustom(_customFrom, _customTo);
        UpdatePreview();
        HideError();
    }

    private void CustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("L_IconDialogTitle"),
            Filter = Strings.T("L_FilterImages")
                     + "|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
                     + "|" + Strings.T("L_FilterIcons")
                     + "|*.ico;*.exe;*.dll;*.lnk"
                     + "|" + Strings.T("L_FilterAll"),
            CheckFileExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return;
        }

        var image = IconService.GetCustomIcon(dialog.FileName);
        if (image is null)
        {
            ShowError(Strings.T("L_ErrImage"));
            return;
        }

        _customIconPath = dialog.FileName;
        IconListBox.SelectedItem = null;
        HideError();
        UpdatePreview();
    }

    private bool ShowDialog(OpenFileDialog dialog)
    {
        var owner = Window.GetWindow(this);
        return owner is null ? dialog.ShowDialog() == true : dialog.ShowDialog(owner) == true;
    }

    private void UpdatePreview()
    {
        if (PreviewTile is null)
        {
            return;
        }

        var accent = AccentListBox.SelectedItem as AccentOption ?? AccentPalette.Default;
        PreviewTile.Background = accent.PreviewBrush;
        SaveButton.Background = accent.PreviewBrush;

        var customImage = _customIconPath is null ? null : IconService.GetCustomIcon(_customIconPath);
        if (customImage is not null)
        {
            PreviewTileImage.Source = customImage;
            ShowPreviewImage(true);
            return;
        }

        var icon = IconListBox.SelectedItem as ScenarioIcon ?? ScenarioIcons.Get(_existing?.IconKey);
        PreviewTileIcon.Data = icon.Geometry;
        ShowPreviewImage(false);
    }

    private void ShowPreviewImage(bool isImage)
    {
        PreviewTileImage.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
        PreviewTileIcon.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Strings.T("L_ErrName"));
            NameBox.Focus();
            return;
        }

        var hotkey = HotkeyInput.Value;
        if (hotkey is { IsEmpty: false })
        {
            var duplicate = _allScenarios.FirstOrDefault(x =>
                x.Id != _scenarioId && x.Hotkey is { IsEmpty: false } && x.Hotkey.Equals(hotkey));

            if (duplicate is not null)
            {
                ShowError(Strings.T("L_ErrHotkeyFmt", duplicate.Name));
                HotkeyInput.Focus();
                return;
            }
        }

        var accent = AccentListBox.SelectedItem as AccentOption ?? AccentPalette.Default;
        var isCustomAccent = AccentPalette.IsCustom(accent.Key) && _customFrom is not null;
        var selectedIcon = IconListBox.SelectedItem as ScenarioIcon;
        var iconKey = selectedIcon?.Key ?? ScenarioIcons.Get(_existing?.IconKey).Key;
        var passthrough = hotkey is { IsEmpty: false } && PassthroughToggle.IsChecked == true;
        Result = new ScenarioEditResult(
            _scenarioId,
            name,
            DescriptionBox.Text.Trim(),
            iconKey,
            _customIconPath,
            isCustomAccent ? AccentPalette.CustomKey : accent.Key,
            isCustomAccent ? _customFrom : null,
            isCustomAccent ? _customTo : null,
            hotkey,
            passthrough);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is HotkeyBox { IsRecording: true })
        {
            return;
        }

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
