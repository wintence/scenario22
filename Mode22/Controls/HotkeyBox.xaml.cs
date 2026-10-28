using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mode22.Models;

namespace Mode22.Controls;

public partial class HotkeyBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(Hotkey),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    private bool _isRecording;
    private bool _suppressFocusLoss;

    public HotkeyBox()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        LostKeyboardFocus += OnLostKeyboardFocus;
        Loaded += (_, _) => RefreshDisplay();
    }

    public event EventHandler? HotkeyChanged;

    public bool IsRecording => _isRecording;

    public Hotkey? Value
    {
        get => (Hotkey?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
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
        ActionHint.Text = "Esc — отмена";
        PromptText.Text = "Нажмите сочетание…";
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
        if (!keepValue)
        {
            Value = null;
        }

        RefreshDisplay();
    }

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
                : "Нажмите сочетание…";
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Back or Key.Delete)
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
        if (!hasModifier && !isFunctionKey)
        {
            PromptText.Text = "Добавьте Ctrl, Alt, Shift или Win";
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
        _isRecording = false;
        RefreshDisplay();
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
        PromptText.Text = "Нажмите, чтобы записать сочетание";
        ClearButton.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
        ActionHint.Text = hasValue ? "Изменить" : "Записать";
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
