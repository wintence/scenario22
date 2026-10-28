using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Scenario22.Models;
using Scenario22.Services;
using Scenario22.Sheets;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Scenario22;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AppState _state;
    private readonly SettingsService _settings;
    private readonly HotkeyService _hotkeys = new();
    private readonly ScenarioRunner _runner = new();
    private readonly TrayService _tray;
    private readonly ToastService _toasts;
    private readonly DispatcherTimer _saveTimer;

    private HwndSource? _hwndSource;
    private uint _activationMessage;
    private Scenario? _selectedScenario;
    private bool _isSettingsOpen;
    private bool _isExiting;
    private bool _startHidden;
    private bool _cleanedUp;
    private bool _suppressStateEvents;
    private bool _suppressPreferenceEvents;
    private int _sheetGeneration;
    private string? _pendingWarning;

    public MainWindow(AppState state, SettingsService settings, bool startHidden)
    {
        InitializeComponent();

        _state = state;
        _settings = settings;
        _startHidden = startHidden;
        _selectedScenario = state.Scenarios.FirstOrDefault(x => x.Id == state.SelectedScenarioId)
                            ?? state.Scenarios.FirstOrDefault();

        _tray = new TrayService();
        _toasts = new ToastService();

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _saveTimer.Tick += (_, _) => SaveNow();

        _tray.ShowRequested += OnTrayShowRequested;
        _tray.SettingsRequested += OnTraySettingsRequested;
        _tray.ExitRequested += OnTrayExitRequested;
        _tray.RunRequested += OnTrayRunRequested;

        _state.Scenarios.CollectionChanged += Scenarios_CollectionChanged;
        _state.Preferences.PropertyChanged += Preferences_PropertyChanged;

        foreach (var scenario in _state.Scenarios)
        {
            AttachScenario(scenario);
        }

        Closed += OnWindowClosed;
        if (WpfApplication.Current is { } application)
        {
            application.SessionEnding += OnSessionEnding;
        }

        DataContext = this;

        if (_startHidden)
        {
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
            ShowActivated = false;
        }

        UpdateTray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Scenario> Scenarios => _state.Scenarios;

    public AppPreferences Preferences => _state.Preferences;

    public Scenario? SelectedScenario
    {
        get => _selectedScenario;
        set
        {
            if (value is null || !Scenarios.Contains(value))
            {
                value = Scenarios.FirstOrDefault();
            }

            if (ReferenceEquals(_selectedScenario, value))
            {
                return;
            }

            _selectedScenario = value;
            _state.SelectedScenarioId = value?.Id;
            OnPropertyChanged();
            QueueSave();
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            if (_isSettingsOpen == value)
            {
                return;
            }

            _isSettingsOpen = value;
            OnPropertyChanged();
        }
    }

    public string HotkeyStatusLabel
    {
        get
        {
            if (!Preferences.HotkeysEnabled)
            {
                return Strings.T("L_HsPaused");
            }

            var failing = Scenarios.Count(x => x.HasHotkeyError);
            if (failing > 0)
            {
                return failing == 1 ? Strings.T("L_HsConflict1") : Strings.T("L_HsConflictFmt", failing);
            }

            var active = Scenarios.Count(x =>
                x.IsEnabled && x.Hotkey is { IsEmpty: false } && !x.HasHotkeyError);

            if (active == 0)
            {
                return Strings.T("L_HsNone");
            }

            if (LanguageService.IsRu)
            {
                var scenarioWord = Plural(active, "сценарий", "сценария", "сценариев");
                var readyWord = Plural(active, "готов", "готовы", "готовы");
                return $"{active} {scenarioWord} {readyWord}";
            }

            var scenarioNoun = Strings.T(active == 1 ? "L_HsScenario1" : "L_HsScenarioN");
            return $"{active} {scenarioNoun} {Strings.T("L_HsReady")}";
        }
    }

    public WpfBrush HotkeyStatusBrush
    {
        get
        {
            if (!Preferences.HotkeysEnabled)
            {
                return ResolveBrush("WarningBrush", System.Windows.Media.Brushes.Orange);
            }

            if (Scenarios.Any(x => x.HasHotkeyError))
            {
                return ResolveBrush("WarningBrush", System.Windows.Media.Brushes.Orange);
            }

            if (Scenarios.Any(x => x.IsEnabled && x.Hotkey is { IsEmpty: false }))
            {
                return ResolveBrush("SuccessBrush", System.Windows.Media.Brushes.MediumSeaGreen);
            }

            return ResolveBrush("TextTertiaryBrush", System.Windows.Media.Brushes.Gray);
        }
    }

    public void QueueLoadWarning(string message)
    {
        _pendingWarning = message;
        if (IsLoaded)
        {
            ShowPendingWarning();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DataFolderPath.Text = _settings.DataDirectory;
        DataFolderPath.ToolTip = _settings.SettingsPath;
        LanguageEnChoice.IsChecked = !LanguageService.IsRu;
        LanguageRuChoice.IsChecked = LanguageService.IsRu;
        UpdateHotkeyStatus();
        UpdateTray();
        ShowPendingWarning();

        if (_startHidden)
        {
            _startHidden = false;
            HideToTray();
            WindowState = WindowState.Normal;
            return;
        }

        if (SelectedScenario is not null)
        {
            ScenarioListBox.Focus();
        }
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
        _activationMessage = NativeMethods.RegisterWindowMessage("Scenario22.ActivateWindow");
        _hotkeys.Attach(handle);

        ApplyWindowAppearance();
        RegisterHotkeys();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey)
        {
            var scenario = _hotkeys.Resolve(wParam.ToInt32());
            if (scenario is not null)
            {
                handled = true;
                _ = RunScenarioAsync(scenario);
            }

            return IntPtr.Zero;
        }

        if (_activationMessage != 0 && (uint)msg == _activationMessage)
        {
            handled = true;
            ShowMainWindow();
        }

        return IntPtr.Zero;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        if (Preferences.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _isExiting = true;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Cleanup();
        WpfApplication.Current?.Shutdown();
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        _isExiting = true;
        SaveNow();
    }

    private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (OverlayLayer.Visibility == Visibility.Visible)
        {
            return;
        }

        var control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (control && e.Key == Key.N)
        {
            e.Handled = true;
            OpenScenarioSheet(null);
            return;
        }

        if (control && e.Key == Key.OemComma)
        {
            e.Handled = true;
            IsSettingsOpen = true;
            return;
        }

        if (control && e.Key == Key.E && !IsSettingsOpen && SelectedScenario is not null)
        {
            e.Handled = true;
            OpenScenarioSheet(SelectedScenario);
            return;
        }

        if ((control && e.Key == Key.Enter) || e.Key == Key.F5)
        {
            if (!IsSettingsOpen && SelectedScenario is { } scenario)
            {
                e.Handled = true;
                _ = RunScenarioAsync(scenario);
            }

            return;
        }

        if (e.Key == Key.Escape && IsSettingsOpen)
        {
            e.Handled = true;
            IsSettingsOpen = false;
        }
    }

    private void ActionRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<WpfButtonBase>(source) is not null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: ScenarioAction action })
        {
            OpenActionSheet(action);
        }
    }

    private void MoveActionUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ScenarioAction action }
            || SelectedScenario is not { } scenario)
        {
            return;
        }

        var index = scenario.Actions.IndexOf(action);
        if (index > 0)
        {
            scenario.Actions.Move(index, index - 1);
        }
    }

    private void MoveActionDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ScenarioAction action }
            || SelectedScenario is not { } scenario)
        {
            return;
        }

        var index = scenario.Actions.IndexOf(action);
        if (index >= 0 && index < scenario.Actions.Count - 1)
        {
            scenario.Actions.Move(index, index + 1);
        }
    }

    private void DeleteAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ScenarioAction action }
            || SelectedScenario is not { } scenario
            || !scenario.Actions.Contains(action))
        {
            return;
        }

        ShowConfirm(
            Strings.T("L_ConfirmDeleteActionT"),
            Strings.T("L_ConfirmDeleteActionM", action.DisplayName, scenario.Name),
            () => scenario.Actions.Remove(action));
    }

    private void Topbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<WpfButtonBase>(source) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The window may have lost the mouse capture between events.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsOpen = true;
    }

    private void NewScenario_Click(object sender, RoutedEventArgs e)
    {
        OpenScenarioSheet(null);
    }

    private void ScenarioList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedScenario is null)
        {
            return;
        }

        if (IsSettingsOpen)
        {
            IsSettingsOpen = false;
        }

        ScenarioScrollViewer.ScrollToTop();
    }

    private async void RunSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedScenario is { } scenario)
        {
            await RunScenarioAsync(scenario);
        }
    }

    private void EditScenario_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedScenario is { } scenario)
        {
            OpenScenarioSheet(scenario);
        }
    }

    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        OpenActionSheet(null);
    }

    private void BackToScenarios_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsOpen = false;
    }

    private void LanguageChoice_Checked(object sender, RoutedEventArgs e)
    {
        SetLanguage(ReferenceEquals(sender, LanguageRuChoice) ? "ru" : "en");
    }

    private void SetLanguage(string language)
    {
        if (string.Equals(Preferences.Language, language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Preferences.Language = language;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.OpenDataFolder();
        }
        catch (Exception ex)
        {
            _toasts.ShowInfo(
                Strings.T("L_TFolderFailed"),
                ex.Message,
                "📁",
                null,
                isError: true);
        }
    }

    private async Task RunScenarioAsync(Scenario scenario)
    {
        if (_cleanedUp || scenario.IsRunning)
        {
            return;
        }

        var enabledCount = scenario.Actions.Count(x => x.IsEnabled);
        if (enabledCount == 0)
        {
            _toasts.ShowInfo(
                Strings.T("L_TNothingToRun"),
                Strings.T("L_TNothingToRunText"),
                string.IsNullOrWhiteSpace(scenario.Emoji) ? "⚡" : scenario.Emoji,
                scenario.AccentBrush,
                isError: true);
            return;
        }

        scenario.IsRunning = true;
        var notify = Preferences.ShowNotifications;
        var progress = new Progress<ScenarioRunProgress>(update =>
        {
            if (notify)
            {
                _toasts.UpdateProgress(update);
            }
        });

        try
        {
            if (notify)
            {
                _toasts.ShowRunning(scenario, enabledCount);
            }

            var result = await _runner.RunAsync(scenario, progress);
            scenario.LastRun = DateTimeOffset.Now;

            if (notify)
            {
                _toasts.ShowCompleted(result);
            }
        }
        catch (Exception ex)
        {
            if (notify)
            {
                _toasts.ShowInfo(
                    Strings.T("L_TRunFailed"),
                    ex.Message,
                    "⚠️",
                    scenario.AccentBrush,
                    isError: true);
            }
        }
        finally
        {
            scenario.IsRunning = false;
            QueueSave();
            UpdateTray();
        }
    }

    private void OpenScenarioSheet(Scenario? existing)
    {
        if (OverlayLayer.Visibility == Visibility.Visible)
        {
            return;
        }

        var sheet = new ScenarioSheet(existing, Scenarios.ToList());
        sheet.Saved += (_, _) =>
        {
            if (sheet.Result is { } result)
            {
                ApplyScenarioEdit(existing, result);
            }

            CloseSheet();
        };
        sheet.Cancelled += (_, _) => CloseSheet();
        sheet.DeleteRequested += (_, _) =>
        {
            CloseSheet();
            if (existing is not null)
            {
                ShowConfirm(
                    Strings.T("L_ConfirmDeleteScenarioT"),
                    Strings.T("L_ConfirmDeleteScenarioM", existing.Name),
                    () => DeleteScenario(existing));
            }
        };

        ShowSheet(sheet);
    }

    private void ApplyScenarioEdit(Scenario? existing, ScenarioEditResult result)
    {
        var scenario = existing ?? new Scenario { Id = result.Id };

        _suppressStateEvents = true;
        try
        {
            scenario.Name = result.Name;
            scenario.Description = result.Description;
            scenario.IconKey = result.IconKey;
            scenario.CustomIconPath = result.CustomIconPath;
            scenario.AccentKey = result.AccentKey;
            scenario.CustomAccentFrom = result.CustomAccentFrom;
            scenario.CustomAccentTo = result.CustomAccentTo;
            scenario.Hotkey = result.Hotkey?.Clone();
        }
        finally
        {
            _suppressStateEvents = false;
        }

        if (existing is null)
        {
            Scenarios.Add(scenario);
        }

        SelectedScenario = scenario;
        IsSettingsOpen = false;
        RegisterHotkeys();
        QueueSave();
        UpdateTray();
    }

    private void DeleteScenario(Scenario scenario)
    {
        if (!Scenarios.Remove(scenario))
        {
            return;
        }

        if (SelectedScenario is null || !Scenarios.Contains(SelectedScenario))
        {
            SelectedScenario = Scenarios.FirstOrDefault();
        }

        RegisterHotkeys();
        QueueSave();
        UpdateTray();
    }

    private void OpenActionSheet(ScenarioAction? existing)
    {
        if (OverlayLayer.Visibility == Visibility.Visible || SelectedScenario is not { } scenario)
        {
            return;
        }

        var sheet = new ActionSheet(existing);
        sheet.Saved += (_, _) =>
        {
            if (sheet.Result is { } result)
            {
                ApplyActionEdit(scenario, existing, result);
            }

            CloseSheet();
        };
        sheet.Cancelled += (_, _) => CloseSheet();

        ShowSheet(sheet);
    }

    private void ApplyActionEdit(Scenario scenario, ScenarioAction? existing, ActionEditResult result)
    {
        var action = existing ?? new ScenarioAction { Id = result.Id };

        action.Kind = result.Kind;
        action.Name = result.Name;
        action.Target = result.Target;
        action.Arguments = result.Arguments;
        action.ProcessName = result.ProcessName;
        action.DelaySeconds = result.DelaySeconds;
        action.IsEnabled = result.IsEnabled;
        action.RefreshIcon();

        if (existing is null)
        {
            scenario.Actions.Add(action);
        }

        QueueSave();
        UpdateTray();
    }

    private void ShowConfirm(string title, string message, Action confirmed)
    {
        var sheet = new ConfirmSheet(title, message);
        sheet.Confirmed += (_, _) =>
        {
            CloseSheet();
            confirmed();
        };
        sheet.Cancelled += (_, _) => CloseSheet();
        ShowSheet(sheet);
    }

    private void ShowSheet(FrameworkElement sheet)
    {
        _sheetGeneration++;
        SheetHost.Content = sheet;
        OverlayLayer.Visibility = Visibility.Visible;
        OverlayLayer.BeginAnimation(OpacityProperty, null);
        SheetTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        OverlayLayer.Opacity = 0;
        SheetTranslate.Y = 14;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        OverlayLayer.BeginAnimation(OpacityProperty, fade);
        SheetTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void CloseSheet()
    {
        if (OverlayLayer.Visibility != Visibility.Visible)
        {
            return;
        }

        var generation = ++_sheetGeneration;
        var fade = new DoubleAnimation(OverlayLayer.Opacity, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var slide = new DoubleAnimation(SheetTranslate.Y, 10, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fade.Completed += (_, _) =>
        {
            if (generation != _sheetGeneration)
            {
                return;
            }

            OverlayLayer.Visibility = Visibility.Collapsed;
            SheetHost.Content = null;
            OverlayLayer.Opacity = 1;

            if (!IsSettingsOpen)
            {
                ScenarioListBox.Focus();
            }
        };

        OverlayLayer.BeginAnimation(OpacityProperty, fade);
        SheetTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void Scenarios_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Scenario scenario in e.OldItems)
            {
                DetachScenario(scenario);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (Scenario scenario in e.NewItems)
            {
                AttachScenario(scenario);
            }
        }

        if (_suppressStateEvents)
        {
            return;
        }

        if (SelectedScenario is null || !Scenarios.Contains(SelectedScenario))
        {
            SelectedScenario = Scenarios.FirstOrDefault();
        }

        RegisterHotkeys();
        QueueSave();
        UpdateTray();
    }

    private void AttachScenario(Scenario scenario)
    {
        scenario.PropertyChanged += Scenario_PropertyChanged;
        scenario.ActionChanged += Scenario_ActionChanged;
        scenario.Actions.CollectionChanged += Scenario_ActionsChanged;
    }

    private void DetachScenario(Scenario scenario)
    {
        scenario.PropertyChanged -= Scenario_PropertyChanged;
        scenario.ActionChanged -= Scenario_ActionChanged;
        scenario.Actions.CollectionChanged -= Scenario_ActionsChanged;
    }

    private void Scenario_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressStateEvents || _cleanedUp)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(Scenario.Hotkey):
            case nameof(Scenario.IsEnabled):
                RegisterHotkeys();
                break;
            case nameof(Scenario.IsRunning):
            case nameof(Scenario.Name):
                UpdateTray();
                break;
            case nameof(Scenario.HotkeyError):
                UpdateHotkeyStatus();
                break;
        }

        QueueSave();
    }

    private void Scenario_ActionChanged(object? sender, ScenarioAction action)
    {
        if (_suppressStateEvents || _cleanedUp)
        {
            return;
        }

        QueueSave();
        UpdateTray();
    }

    private void Scenario_ActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressStateEvents || _cleanedUp)
        {
            return;
        }

        QueueSave();
        UpdateTray();
    }

    private void Preferences_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressPreferenceEvents || _cleanedUp)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppPreferences.UseDarkTheme):
                ThemeService.Apply(Preferences.UseDarkTheme);
                ApplyWindowAppearance();
                OnPropertyChanged(nameof(HotkeyStatusBrush));
                break;
            case nameof(AppPreferences.Language):
                LanguageService.Apply(Preferences.Language);
                UpdateHotkeyStatus();
                UpdateTray();
                RefreshLanguageDependent();
                break;
            case nameof(AppPreferences.StartWithWindows):
                var (success, error) = StartupService.SetEnabled(Preferences.StartWithWindows);
                if (!success)
                {
                    _suppressPreferenceEvents = true;
                    try
                    {
                        Preferences.StartWithWindows = !Preferences.StartWithWindows;
                    }
                    finally
                    {
                        _suppressPreferenceEvents = false;
                    }

                    _toasts.ShowInfo(
                        Strings.T("L_TAutorun"),
                        string.IsNullOrWhiteSpace(error)
                            ? Strings.T("L_TAutorunFail")
                            : error,
                        "⚠️",
                        null,
                        isError: true);
                }

                break;
            case nameof(AppPreferences.HotkeysEnabled):
                RegisterHotkeys();
                break;
        }

        QueueSave();
    }

    private void RegisterHotkeys()
    {
        if (_hwndSource is null || _cleanedUp)
        {
            return;
        }

        _hotkeys.RegisterAll(Scenarios, Preferences.HotkeysEnabled);
        UpdateHotkeyStatus();
        UpdateTray();
    }

    private void UpdateHotkeyStatus()
    {
        OnPropertyChanged(nameof(HotkeyStatusLabel));
        OnPropertyChanged(nameof(HotkeyStatusBrush));
    }

    private void RefreshLanguageDependent()
    {
        foreach (var scenario in Scenarios)
        {
            scenario.RefreshLanguageDependent();
        }
    }

    private void UpdateTray()
    {
        if (_cleanedUp)
        {
            return;
        }

        _tray.Update(Scenarios.ToList(), Preferences.HotkeysEnabled);
    }

    private void QueueSave()
    {
        if (_cleanedUp)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        _state.SelectedScenarioId = SelectedScenario?.Id;
        _settings.Save(_state);
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;

        if (Preferences.TrayHintShown)
        {
            return;
        }

        Preferences.TrayHintShown = true;
        _tray.ShowTrayTip(
            Strings.T("L_TrTipTitle"),
            Strings.T("L_TrTipText"));
    }

    private void ShowMainWindow()
    {
        if (_isExiting || _cleanedUp)
        {
            return;
        }

        _startHidden = false;
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(handle);
        }
    }

    private void ShowSettings()
    {
        ShowMainWindow();
        IsSettingsOpen = true;
    }

    private void ExitApplication()
    {
        if (_isExiting || _cleanedUp)
        {
            return;
        }

        _isExiting = true;
        SaveNow();
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ApplyWindowAppearance()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var dark = Preferences.UseDarkTheme ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(
                handle,
                NativeMethods.DwmUseImmersiveDarkMode,
                ref dark,
                sizeof(int));

            var corner = NativeMethods.DwmWindowCornerDoNotRound;
            NativeMethods.DwmSetWindowAttribute(
                handle,
                NativeMethods.DwmWindowCornerPreference,
                ref corner,
                sizeof(int));
        }
        catch
        {
            // DWM attributes are best-effort.
        }
    }

    private void ShowPendingWarning()
    {
        if (_pendingWarning is null)
        {
            return;
        }

        _pendingWarning = null;
        _toasts.ShowInfo(
            Strings.T("L_TSettingsRestored"),
            Strings.T("L_TSettingsRestoredText"),
            "⚠️",
            null,
            isError: true);
    }

    private void OnTrayShowRequested()
    {
        Dispatch(ShowMainWindow);
    }

    private void OnTraySettingsRequested()
    {
        Dispatch(ShowSettings);
    }

    private void OnTrayExitRequested()
    {
        Dispatch(ExitApplication);
    }

    private void OnTrayRunRequested(Scenario scenario)
    {
        Dispatch(() => _ = RunScenarioAsync(scenario));
    }

    private void Dispatch(Action action)
    {
        if (_cleanedUp || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    private void Cleanup()
    {
        if (_cleanedUp)
        {
            return;
        }

        _cleanedUp = true;
        _saveTimer.Stop();
        SaveNow();

        _state.Scenarios.CollectionChanged -= Scenarios_CollectionChanged;
        _state.Preferences.PropertyChanged -= Preferences_PropertyChanged;

        foreach (var scenario in _state.Scenarios)
        {
            DetachScenario(scenario);
        }

        _tray.ShowRequested -= OnTrayShowRequested;
        _tray.SettingsRequested -= OnTraySettingsRequested;
        _tray.ExitRequested -= OnTrayExitRequested;
        _tray.RunRequested -= OnTrayRunRequested;

        if (WpfApplication.Current is { } application)
        {
            application.SessionEnding -= OnSessionEnding;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;

        _hotkeys.Dispose();
        _tray.Dispose();
        _toasts.Dispose();
    }

    private static string Plural(int value, string one, string few, string many)
    {
        var mod10 = value % 10;
        var mod100 = value % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return one;
        }

        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14)
        {
            return few;
        }

        return many;
    }

    private static WpfBrush ResolveBrush(string key, WpfBrush fallback)
    {
        return WpfApplication.Current?.TryFindResource(key) as WpfBrush ?? fallback;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
