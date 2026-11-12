using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22.Sheets;

/// <summary>
/// Borderless custom color picker for the "custom" scenario accent: edits the
/// two gradient stops with a saturation/brightness field, hue slider and HEX input.
/// </summary>
public partial class ColorPickerWindow : Window
{
    private bool _isToTab;
    private double _hue;
    private double _sat = 1;
    private double _val = 1;
    private bool _dragging;
    private bool _suppressHexUpdate;

    public ColorPickerWindow(Color from, Color to)
    {
        InitializeComponent();

        FromColor = from;
        ToColor = to;
        FromTabLabel.Text = Strings.T("L_PickFrom");
        ToTabLabel.Text = Strings.T("L_PickTo");

        FromTab.Click += (_, _) => SwitchTab(false);
        ToTab.Click += (_, _) => SwitchTab(true);
        FromTab.IsChecked = true;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => RefreshAll();

        LoadHsv(FromColor);
        RefreshAll();
    }

    public Color FromColor { get; private set; }

    public Color ToColor { get; private set; }

    public string FromHex => ColorHex(FromColor);

    public string ToHex => ColorHex(ToColor);

    private Color CurrentColor => _isToTab ? ToColor : FromColor;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var dark = ThemeService.IsDark ? 1 : 0;
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

    private void SwitchTab(bool toTab)
    {
        if (_isToTab != toTab)
        {
            _isToTab = toTab;
            LoadHsv(CurrentColor);
        }

        if (_isToTab)
        {
            ToTab.IsChecked = true;
        }
        else
        {
            FromTab.IsChecked = true;
        }

        RefreshAll();
    }

    private void FromTab_Click(object sender, RoutedEventArgs e) => SwitchTab(false);

    private void ToTab_Click(object sender, RoutedEventArgs e) => SwitchTab(true);

    private void LoadHsv(Color color)
    {
        ColorToHsv(color, out _hue, out _sat, out _val);
    }

    private void SvCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        SvCanvas.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(SvCanvas));
    }

    private void SvCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            UpdateSvFromPoint(e.GetPosition(SvCanvas));
        }
    }

    private void SvCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        SvCanvas.ReleaseMouseCapture();
    }

    private void UpdateSvFromPoint(Point point)
    {
        _sat = Math.Clamp(point.X / SvCanvas.ActualWidth, 0, 1);
        _val = Math.Clamp(1 - point.Y / SvCanvas.ActualHeight, 0, 1);
        ApplyCurrentColor();
    }

    private void HueCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        HueCanvas.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            UpdateHueFromPoint(e.GetPosition(HueCanvas));
        }
    }

    private void UpdateHueFromPoint(Point point)
    {
        _hue = Math.Clamp(point.Y / HueCanvas.ActualHeight * 360, 0, 359.99);
        ApplyCurrentColor();
    }

    private void ApplyCurrentColor()
    {
        var color = HsvToColor(_hue, _sat, _val);
        if (_isToTab)
        {
            ToColor = color;
        }
        else
        {
            FromColor = color;
        }

        RefreshAll();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHexUpdate)
        {
            return;
        }

        var text = HexBox.Text.Trim().TrimStart('#');
        if (text.Length == 6 && AccentPalette.TryParseColor("#" + text, out var color))
        {
            if (_isToTab)
            {
                ToColor = color;
            }
            else
            {
                FromColor = color;
            }

            LoadHsv(color);
            RefreshAll(skipHex: true);
        }
    }

    private void RefreshAll(bool skipHex = false)
    {
        FromSwatch.Fill = new SolidColorBrush(FromColor);
        ToSwatch.Fill = new SolidColorBrush(ToColor);

        var pure = HsvToColor(_hue, 1, 1);
        SvHueFill.Fill = new LinearGradientBrush(Colors.White, pure, new Point(0, 0), new Point(1, 0));

        UpdateSvPointer();
        UpdateHueThumb();

        if (!skipHex)
        {
            _suppressHexUpdate = true;
            HexBox.Text = ColorHex(CurrentColor);
            _suppressHexUpdate = false;
        }

        PreviewBorder.Background = AccentPalette.CreateGradient(FromColor, ToColor);
    }

    private void UpdateSvPointer()
    {
        var width = SvCanvas.ActualWidth;
        var height = SvCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x = _sat * width - 10;
        var y = (1 - _val) * height - 10;
        Canvas.SetLeft(SvPointer, x);
        Canvas.SetTop(SvPointer, y);
    }

    private void UpdateHueThumb()
    {
        var height = HueCanvas.ActualHeight;
        if (height <= 0)
        {
            return;
        }

        Canvas.SetTop(HueThumb, Math.Clamp(_hue / 360 * height, 0, height) - 10);
        Canvas.SetLeft(HueThumb, -6);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The window may have lost the mouse capture between events.
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            DialogResult = true;
        }
    }

    private static void ColorToHsv(Color color, out double hue, out double sat, out double val)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        val = max;
        sat = max <= 0 ? 0 : delta / max;

        if (delta <= 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * ((b - r) / delta + 2);
        }
        else
        {
            hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }
    }

    private static Color HsvToColor(double hue, double sat, double val)
    {
        hue = ((hue % 360) + 360) % 360;
        var c = val * sat;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = val - c;

        double r;
        double g;
        double b;
        switch (Math.Floor(hue / 60))
        {
            case 0:
                r = c;
                g = x;
                b = 0;
                break;

            case 1:
                r = x;
                g = c;
                b = 0;
                break;

            case 2:
                r = 0;
                g = c;
                b = x;
                break;

            case 3:
                r = 0;
                g = x;
                b = c;
                break;

            case 4:
                r = x;
                g = 0;
                b = c;
                break;

            default:
                r = c;
                g = 0;
                b = x;
                break;
        }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private static string ColorHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
