using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22.Controls;

public partial class HotkeyBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(Hotkey),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty AllowSingleKeyProperty = DependencyProperty.Register(
        nameof(AllowSingleKey),
        typeof(bool),
        typeof(HotkeyBox),
        new PropertyMetadata(false));

    private const int VkBack = 0x08;
    private const int VkEscape = 0x1B;
    private const int VkDelete = 0x2E;

    private bool _isRecording;
    private bool _suppressFocusLoss;
    private bool _captured;
    private IntPtr _hookHandle;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private ModifierKeys _hookModifiers;
    private readonly HashSet<int> _pressedKeys = new();

    public HotkeyBox()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        LostKeyboardFocus += OnLostKeyboardFocus;
        Unloaded += (_, _) => CancelRecording();
        Loaded += (_, _) => RefreshDisplay();
    }

    public event EventHandler? HotkeyChanged;

    public bool IsRecording => _isRecording;

    public Hotkey? Value
    {
        get => (Hotkey?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Allows recording a key without Ctrl, Alt, Shift or Win.
    /// Used by actions that send a single key to the active window.
    /// </summary>
    public bool AllowSingleKey
    {
        get => (bool)GetValue(AllowSingleKeyProperty);
        set => SetValue(AllowSingleKeyProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyBox box)
        {
            box.RefreshDisplay();
            box.HotkeyChanged?.Invoke(box, EventArgs.Empty);
        }
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        BeginRecording();
        e.Handled = true;
    }

    private void BeginRecording()
    {
        _isRecording = true;
        _captured = false;
        _hookModifiers = ModifierKeys.None;
        _pressedKeys.Clear();
        InstallHook();

        ActionHint.Text = Strings.T("L_HbEscHint");
        PromptText.Text = Strings.T("L_HbPress");
        PromptText.Foreground = (Brush)FindResource("TextSecondaryBrush");
        PromptText.Visibility = Visibility.Visible;
        Tokens.Visibility = Visibility.Collapsed;
        ClearButton.Visibility = Visibility.Collapsed;
        RootBorder.BorderBrush = (Brush)FindResource("InputFocusBrush");
        Focus();
        Keyboard.Focus(this);
    }

    private void StopRecording(bool keepValue = true)
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _captured = false;
        if (!keepValue)
        {
            Value = null;
        }

        UninstallHook();
        RefreshDisplay();
    }

    private void CancelRecording()
    {
        _isRecording = false;
        _captured = false;
        _hookModifiers = ModifierKeys.None;
        _pressedKeys.Clear();
        UninstallHook();
        if (RootBorder is not null)
        {
            RefreshDisplay();
        }
    }

    private void InstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookProc ??= KeyboardHookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookProc,
            IntPtr.Zero,
            0);
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
        if (!isDown && !isUp)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        var virtualKey = (int)info.VirtualKeyCode;

        if (isDown)
        {
            _pressedKeys.Add(virtualKey);

            if (_captured)
            {
                return new IntPtr(1);
            }

            if (TryGetModifier(virtualKey, out var modifier))
            {
                _hookModifiers |= modifier;
                UpdateRecordingPrompt();
                return new IntPtr(1);
            }

            if (virtualKey == VkEscape)
            {
                CompleteRecording(null, keepValue: true);
                return new IntPtr(1);
            }

            if (!AllowSingleKey
                && _hookModifiers == ModifierKeys.None
                && virtualKey is VkBack or VkDelete)
            {
                CompleteRecording(null, keepValue: false);
                return new IntPtr(1);
            }

            if (!AllowSingleKey
                && _hookModifiers == ModifierKeys.None
                && !IsFunctionKey(virtualKey))
            {
                ShowNeedModifier();
                return new IntPtr(1);
            }

            CompleteRecording(Hotkey.Create(_hookModifiers, virtualKey), keepValue: true);
            return new IntPtr(1);
        }

        _pressedKeys.Remove(virtualKey);
        if (TryGetModifier(virtualKey, out var released))
        {
            _hookModifiers &= ~released;
        }

        if (_captured && _pressedKeys.Count == 0)
        {
            _captured = false;
            UninstallHook();
        }
        else if (_isRecording)
        {
            UpdateRecordingPrompt();
        }

        return new IntPtr(1);
    }

    private void CompleteRecording(Hotkey? hotkey, bool keepValue)
    {
        if (keepValue)
        {
            if (hotkey is not null)
            {
                Value = hotkey;
            }
        }
        else
        {
            Value = null;
        }

        _isRecording = false;
        _captured = true;
        RefreshDisplay();
    }

    private void UpdateRecordingPrompt()
    {
        if (!_isRecording)
        {
            return;
        }

        var parts = new List<string>(4);
        if (_hookModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (_hookModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (_hookModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (_hookModifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        ActionHint.Text = Strings.T("L_HbEscHint");
        PromptText.Foreground = (Brush)FindResource("TextSecondaryBrush");
        PromptText.Text = parts.Count > 0
            ? string.Join(" + ", parts) + " + …"
            : Strings.T("L_HbPress");
    }

    private void ShowNeedModifier()
    {
        PromptText.Text = Strings.T("L_HbNeedMod");
        PromptText.Foreground = (Brush)FindResource("DangerBrush");
    }

    private static bool TryGetModifier(int virtualKey, out ModifierKeys modifier)
    {
        switch (virtualKey)
        {
            case 0x11 or 0xA2 or 0xA3:
                modifier = ModifierKeys.Control;
                return true;

            case 0x12 or 0xA4 or 0xA5:
                modifier = ModifierKeys.Alt;
                return true;

            case 0x10 or 0xA0 or 0xA1:
                modifier = ModifierKeys.Shift;
                return true;

            case 0x5B or 0x5C:
                modifier = ModifierKeys.Windows;
                return true;

            default:
                modifier = ModifierKeys.None;
                return false;
        }
    }

    private static bool IsFunctionKey(int virtualKey) => virtualKey is >= 0x70 and <= 0x87;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Space) && IsKeyboardFocused)
            {
                BeginRecording();
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            StopRecording();
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            var mods = Keyboard.Modifiers;
            var parts = new List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            PromptText.Text = parts.Count > 0
                ? string.Join(" + ", parts) + " + …"
                : Strings.T("L_HbPress");
            return;
        }

        if (!AllowSingleKey && Keyboard.Modifiers == ModifierKeys.None && key is Key.Back or Key.Delete)
        {
            Value = null;
            _isRecording = false;
            RefreshDisplay();
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var hasModifier = modifiers != ModifierKeys.None;
        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (!hasModifier && !isFunctionKey && !AllowSingleKey)
        {
            PromptText.Text = Strings.T("L_HbNeedMod");
            PromptText.Foreground = (Brush)FindResource("DangerBrush");
            return;
        }

        Value = Hotkey.Create(modifiers, virtualKey);
        _isRecording = false;
        RefreshDisplay();
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_isRecording && !_suppressFocusLoss)
        {
            StopRecording();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _suppressFocusLoss = true;
        Value = null;
        CancelRecording();
        _suppressFocusLoss = false;
    }

    private void RefreshDisplay()
    {
        if (RootBorder is null)
        {
            return;
        }

        if (_isRecording)
        {
            RootBorder.BorderBrush = (Brush)FindResource("InputFocusBrush");
            return;
        }

        RootBorder.BorderBrush = (Brush)FindResource("InputBorderBrush");
        PromptText.Foreground = (Brush)FindResource("InputPlaceholderBrush");

        var hasValue = Value is { IsEmpty: false };
        Tokens.ItemsSource = hasValue ? Value!.Tokens : null;
        Tokens.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
        PromptText.Visibility = hasValue ? Visibility.Collapsed : Visibility.Visible;
        PromptText.Text = Strings.T("L_HbPromptPick");
        ClearButton.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
        ActionHint.Text = Strings.T(hasValue ? "L_HbHintEdit" : "L_HbHintRecord");
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
