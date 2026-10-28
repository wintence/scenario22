using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _hideTimer;
    private int _hideGeneration;

    public ToastWindow()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideAnimated();
        };
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GwlExStyle,
                style | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow);
        }
        catch
        {
            // The toast is still usable as a normal topmost window.
        }
    }

    public void ShowRunning(Scenario scenario, int totalActions)
    {
        _hideTimer.Stop();
        SetScenarioIcon(scenario);
        TitleText.Text = scenario.Name;
        SubtitleText.Text = totalActions == 1 ? Strings.T("L_ToastAction1") : Strings.T("L_ToastActionsN");
        IconTile.Background = scenario.AccentGradient;
        Spinner.Stroke = scenario.AccentBrush;
        ProgressFill.Background = scenario.AccentGradient;
        ProgressScale.ScaleX = 0;
        SetState(ToastState.Running);
        StartSpinner();
        ShowToast();
    }

    public void UpdateProgress(ScenarioRunProgress progress)
    {
        var percent = Math.Clamp(progress.Percent, 0, 100);
        SubtitleText.Text = Strings.T("L_ToastProgressFmt", progress.CurrentAction);
        var animation = new DoubleAnimation(
            ProgressScale.ScaleX,
            percent / 100d,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    public void ShowCompleted(ScenarioRunResult result)
    {
        _hideTimer.Stop();
        StopSpinner();
        SetScenarioIcon(result.Scenario);
        IconTile.Background = result.Scenario.AccentGradient;

        if (result.Success)
        {
            TitleText.Text = result.Scenario.Name;
            var seconds = result.Duration.TotalSeconds;
            var duration = seconds < 1
                ? Strings.T("L_ToastLessSecond")
                : Strings.T("L_DurSecFmt", seconds.ToString("0.0", LanguageService.Culture));
            SubtitleText.Text = result.TotalActions == 0
                ? Strings.T("L_ToastNoActions")
                : Strings.T("L_ToastDone", duration);
            ProgressFill.Background = ResolveBrush("SuccessBrush", Brushes.MediumSeaGreen);
            SetState(ToastState.Success);
            AnimateProgressTo(1);
            _hideTimer.Interval = TimeSpan.FromSeconds(3.2);
        }
        else
        {
            TitleText.Text = Strings.T("L_ToastPartial");
            var first = result.Errors[0];
            SubtitleText.Text = $"{first.ActionName}: {first.Message}";
            ProgressFill.Background = ResolveBrush("WarningBrush", Brushes.Orange);
            SetState(ToastState.Error);
            AnimateProgressTo(1);
            _hideTimer.Interval = TimeSpan.FromSeconds(5.2);
        }

        ShowToast();
        _hideTimer.Start();
    }

    public void ShowInfo(string title, string message, string emoji, Brush? accent = null, bool isError = false)
    {
        _hideTimer.Stop();
        StopSpinner();
        SetInfoIcon(emoji);
        TitleText.Text = title;
        SubtitleText.Text = message;
        IconTile.Background = accent ?? ResolveBrush("AccentStaticBrush", Brushes.CornflowerBlue);
        ProgressFill.Background = isError
            ? ResolveBrush("WarningBrush", Brushes.Orange)
            : (accent ?? ResolveBrush("AccentStaticBrush", Brushes.CornflowerBlue));
        SetState(isError ? ToastState.Error : ToastState.Success);
        AnimateProgressTo(1);
        ShowToast();
        _hideTimer.Interval = TimeSpan.FromSeconds(isError ? 4.8 : 3.4);
        _hideTimer.Start();
    }

    private void SetScenarioIcon(Scenario scenario)
    {
        var image = scenario.TileImage;
        if (image is not null)
        {
            CustomIcon.Source = image;
            ShowGlyph(ToastIconState.Image);
            return;
        }

        GlyphIcon.Data = scenario.TileGeometry;
        ShowGlyph(ToastIconState.Glyph);
    }

    private void SetInfoIcon(string emoji)
    {
        EmojiText.Text = emoji;
        ShowGlyph(ToastIconState.Emoji);
    }

    private void ShowGlyph(ToastIconState state)
    {
        EmojiText.Visibility = state == ToastIconState.Emoji ? Visibility.Visible : Visibility.Collapsed;
        GlyphIcon.Visibility = state == ToastIconState.Glyph ? Visibility.Visible : Visibility.Collapsed;
        CustomIcon.Visibility = state == ToastIconState.Image ? Visibility.Visible : Visibility.Collapsed;
    }

    private enum ToastIconState
    {
        Emoji,
        Glyph,
        Image
    }

    private void ShowToast()
    {
        _hideGeneration++;
        _hideTimer.Stop();

        var wasVisible = IsVisible;
        if (!wasVisible)
        {
            AnimRoot.Opacity = 0;
            SlideTransform.Y = 16;
            Show();
        }

        UpdatePosition();
        Topmost = false;
        Topmost = true;

        AnimRoot.BeginAnimation(OpacityProperty, null);
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, null);
        AnimRoot.Opacity = 0;
        SlideTransform.Y = 16;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        AnimRoot.BeginAnimation(OpacityProperty, fade);
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void HideAnimated()
    {
        var generation = ++_hideGeneration;
        var fade = new DoubleAnimation(AnimRoot.Opacity, 0, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            if (generation != _hideGeneration)
            {
                return;
            }

            Hide();
            AnimRoot.Opacity = 0;
            SlideTransform.Y = 16;
        };
        AnimRoot.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateProgressTo(double value)
    {
        var animation = new DoubleAnimation(
            ProgressScale.ScaleX,
            value,
            TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    private void UpdatePosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Bottom - Height - 18;
    }

    private void StartSpinner()
    {
        StopSpinner();
        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.85))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void StopSpinner()
    {
        SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void SetState(ToastState state)
    {
        Spinner.Visibility = state == ToastState.Running ? Visibility.Visible : Visibility.Collapsed;
        SuccessIcon.Visibility = state == ToastState.Success ? Visibility.Visible : Visibility.Collapsed;
        ErrorIcon.Visibility = state == ToastState.Error ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush ResolveBrush(string key, Brush fallback)
    {
        return Application.Current?.TryFindResource(key) as Brush ?? fallback;
    }

    public void CloseImmediately()
    {
        _hideTimer.Stop();
        _hideGeneration++;
        Close();
    }

    private enum ToastState
    {
        Running,
        Success,
        Error
    }
}
