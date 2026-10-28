using System.Drawing;
using System.Windows;
using Scenario22.Models;

namespace Scenario22.Services;

public sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly Icon? _icon;
    private bool _disposed;

    public TrayService()
    {
        _menu = new System.Windows.Forms.ContextMenuStrip
        {
            Renderer = new TrayMenuRenderer(),
            ShowImageMargin = false,
            DropShadowEnabled = false,
            BackColor = Color.FromArgb(32, 32, 37),
            ForeColor = Color.White,
            Font = new Font("Consolas", 9.5f),
            Padding = new System.Windows.Forms.Padding(6)
        };
        _menu.Opening += (_, _) => RebuildMenu();

        _icon = LoadIcon();
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _icon ?? SystemIcons.Application,
            Text = Strings.T("L_TrCaption"),
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    public event Action? ShowRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<Scenario>? RunRequested;

    private IReadOnlyList<Scenario> _scenarios = Array.Empty<Scenario>();
    private bool _hotkeysEnabled = true;

    public void Update(IReadOnlyList<Scenario> scenarios, bool hotkeysEnabled)
    {
        _scenarios = scenarios;
        _hotkeysEnabled = hotkeysEnabled;
        _notifyIcon.Text = hotkeysEnabled
            ? Strings.T("L_TrCaption")
            : Strings.T("L_TrCaptionPaused");
    }

    public void ShowTrayTip(string title, string message)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(2500);
        }
        catch
        {
            // Balloon tips are optional.
        }
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        var open = CreateItem(Strings.T("L_TrOpen"), bold: true);
        open.Click += (_, _) => ShowRequested?.Invoke();
        _menu.Items.Add(open);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        foreach (var scenario in _scenarios)
        {
            var item = CreateItem("  " + scenario.Name);
            item.ShortcutKeyDisplayString = scenario.Hotkey?.Display ?? string.Empty;
            item.Enabled = _hotkeysEnabled && scenario.IsEnabled && scenario.HasActions && !scenario.IsRunning;
            item.Click += (_, _) => RunRequested?.Invoke(scenario);
            _menu.Items.Add(item);
        }

        if (_scenarios.Count == 0)
        {
            var empty = CreateItem("  " + Strings.T("L_TrNoScenarios"));
            empty.Enabled = false;
            _menu.Items.Add(empty);
        }

        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var settings = CreateItem(Strings.T("L_TrSettings"));
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        _menu.Items.Add(settings);
        var exit = CreateItem(Strings.T("L_TrExit"));
        exit.Click += (_, _) => ExitRequested?.Invoke();
        _menu.Items.Add(exit);
    }

    private static System.Windows.Forms.ToolStripMenuItem CreateItem(string text, bool bold = false)
    {
        return new System.Windows.Forms.ToolStripMenuItem(text)
        {
            ForeColor = Color.FromArgb(242, 242, 247),
            Font = new Font(
                "Consolas",
                9.5f,
                bold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular),
            Padding = new System.Windows.Forms.Padding(8, 5, 8, 5)
        };
    }

    private static Icon? LoadIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            return new Icon(stream);
        }
        catch
        {
            try
            {
                return Environment.ProcessPath is { } path
                    ? Icon.ExtractAssociatedIcon(path)
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon?.Dispose();
    }

    private sealed class TrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new TrayColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(242, 242, 247) : Color.FromArgb(115, 115, 122);
            base.OnRenderItemText(e);
        }
    }

    private sealed class TrayColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 37);
        public override Color MenuBorder => Color.FromArgb(54, 54, 61);
        public override Color MenuItemBorder => Color.FromArgb(58, 58, 65);
        public override Color MenuItemSelected => Color.FromArgb(48, 48, 55);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(48, 48, 55);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(48, 48, 55);
        public override Color SeparatorDark => Color.FromArgb(54, 54, 61);
        public override Color SeparatorLight => Color.FromArgb(54, 54, 61);
        public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 37);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 37);
        public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 37);
    }
}
