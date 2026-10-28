using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Mode22.Services;

namespace Mode22.Models;

public sealed class Scenario : ObservableObject
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _emoji = "✨";
    private string _accentKey = "blue";
    private string? _customAccentFrom;
    private string? _customAccentTo;
    private string _iconKey = ScenarioIcons.Default.Key;
    private string? _customIconPath;
    private Hotkey? _hotkey;
    private bool _isEnabled = true;
    private bool _isRunning;
    private DateTimeOffset? _lastRun;
    private string? _hotkeyError;
    private ObservableCollection<ScenarioAction> _actions = new();

    public Scenario()
    {
        // Subscribe the field-initialized collection so collection changes
        // are tracked even when the Actions property is never reassigned
        // (scenarios created in the UI, not loaded from a file).
        AttachActions(_actions);
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Emoji
    {
        get => _emoji;
        set => SetProperty(ref _emoji, value);
    }

    public string AccentKey
    {
        get => _accentKey;
        set
        {
            if (SetProperty(ref _accentKey, value))
            {
                OnAccentChanged();
            }
        }
    }

    /// <summary>
    /// Start color of the user-defined gradient as "#RRGGBB". Used only when AccentKey is "custom".
    /// </summary>
    public string? CustomAccentFrom
    {
        get => _customAccentFrom;
        set
        {
            if (SetProperty(ref _customAccentFrom, value))
            {
                OnAccentChanged();
            }
        }
    }

    /// <summary>
    /// End color of the user-defined gradient as "#RRGGBB". Used only when AccentKey is "custom".
    /// </summary>
    public string? CustomAccentTo
    {
        get => _customAccentTo;
        set
        {
            if (SetProperty(ref _customAccentTo, value))
            {
                OnAccentChanged();
            }
        }
    }

    [JsonIgnore]
    public bool IsCustomAccent =>
        AccentPalette.IsCustom(AccentKey) && AccentPalette.TryParseColor(CustomAccentFrom, out _);

    private Color FromColor
    {
        get
        {
            if (IsCustomAccent && AccentPalette.TryParseColor(CustomAccentFrom, out var from))
            {
                return from;
            }

            return AccentPalette.Get(AccentKey).Color;
        }
    }

    private Color ToColor
    {
        get
        {
            if (IsCustomAccent)
            {
                return AccentPalette.TryParseColor(CustomAccentTo, out var to) ? to : FromColor;
            }

            return AccentPalette.Get(AccentKey).Color2;
        }
    }

    /// <summary>
    /// Built-in icon key from <see cref="ScenarioIcons"/>. Ignored when a custom icon is set.
    /// </summary>
    public string IconKey
    {
        get => _iconKey;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value) ? ScenarioIcons.Default.Key : value.Trim();
            if (SetProperty(ref _iconKey, clean))
            {
                OnTileChanged();
            }
        }
    }

    /// <summary>
    /// Optional path to a custom icon image (png, jpg, bmp, ico, exe, lnk).
    /// When set to null the built-in icon is shown again.
    /// </summary>
    public string? CustomIconPath
    {
        get => _customIconPath;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (SetProperty(ref _customIconPath, clean))
            {
                OnTileChanged();
            }
        }
    }

    public Hotkey? Hotkey
    {
        get => _hotkey;
        set
        {
            if (SetProperty(ref _hotkey, value))
            {
                HotkeyError = null;
                OnManyPropertiesChanged(nameof(HotkeyDisplay), nameof(HotkeyTokens), nameof(HasHotkey));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public ObservableCollection<ScenarioAction> Actions
    {
        get => _actions;
        set
        {
            if (ReferenceEquals(_actions, value))
            {
                return;
            }

            DetachActions(_actions);
            _actions = value ?? new ObservableCollection<ScenarioAction>();
            AttachActions(_actions);
            OnManyPropertiesChanged(nameof(Actions), nameof(ActionCount), nameof(ActionsSummary), nameof(HasActions), nameof(HasEnabledActions));
        }
    }

    public DateTimeOffset? LastRun
    {
        get => _lastRun;
        set
        {
            if (SetProperty(ref _lastRun, value))
            {
                OnPropertyChanged(nameof(LastRunLabel));
            }
        }
    }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    [JsonIgnore]
    public string? HotkeyError
    {
        get => _hotkeyError;
        set
        {
            if (SetProperty(ref _hotkeyError, value))
            {
                OnPropertyChanged(nameof(HasHotkeyError));
            }
        }
    }

    [JsonIgnore]
    public string HotkeyDisplay => Hotkey?.Display ?? "Без клавиши";

    [JsonIgnore]
    public IReadOnlyList<string> HotkeyTokens => Hotkey?.Tokens ?? Array.Empty<string>();

    [JsonIgnore]
    public bool HasHotkey => Hotkey is { IsEmpty: false };

    [JsonIgnore]
    public bool HasHotkeyError => !string.IsNullOrWhiteSpace(HotkeyError);

    [JsonIgnore]
    public int ActionCount => Actions.Count;

    [JsonIgnore]
    public string ActionsSummary
    {
        get
        {
            var count = Actions.Count;
            if (count == 0)
            {
                return "Нет действий";
            }

            var mod10 = count % 10;
            var mod100 = count % 100;
            var word = mod10 == 1 && mod100 != 11 ? "действие"
                : mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14 ? "действия"
                : "действий";
            return $"{count} {word}";
        }
    }

    [JsonIgnore]
    public bool HasActions => Actions.Count > 0;

    [JsonIgnore]
    public bool HasEnabledActions => Actions.Any(x => x.IsEnabled);

    [JsonIgnore]
    public ImageSource? TileImage => HasTileImage ? IconService.GetCustomIcon(CustomIconPath) : null;

    [JsonIgnore]
    public bool HasTileImage => !string.IsNullOrWhiteSpace(CustomIconPath)
        && IconService.GetCustomIcon(CustomIconPath) is not null;

    [JsonIgnore]
    public bool TileIsImage => HasTileImage;

    [JsonIgnore]
    public Geometry? TileGeometry => TileIsImage ? null : ScenarioIcons.Get(IconKey).Geometry;

    private void OnTileChanged()
    {
        OnManyPropertiesChanged(
            nameof(TileGeometry),
            nameof(TileImage),
            nameof(TileIsImage),
            nameof(HasTileImage));
    }

    private void OnAccentChanged()
    {
        OnManyPropertiesChanged(
            nameof(AccentBrush),
            nameof(AccentGradient),
            nameof(AccentSoftBrush),
            nameof(AccentSofterBrush),
            nameof(AmbientBrush),
            nameof(AccentColor),
            nameof(IsCustomAccent));
    }

    [JsonIgnore]
    public Brush AccentBrush
    {
        get
        {
            var brush = new SolidColorBrush(FromColor);
            brush.Freeze();
            return brush;
        }
    }

    [JsonIgnore]
    public Color AccentColor => FromColor;

    [JsonIgnore]
    public Brush AccentGradient => AccentPalette.CreateGradient(FromColor, ToColor);

    [JsonIgnore]
    public Brush AccentSoftBrush
    {
        get
        {
            var color = FromColor;
            var brush = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }
    }

    [JsonIgnore]
    public Brush AccentSofterBrush
    {
        get
        {
            var color = FromColor;
            var brush = new SolidColorBrush(Color.FromArgb(16, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }
    }

    [JsonIgnore]
    public Brush AmbientBrush
    {
        get
        {
            var color = FromColor;
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(36, color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(16, color.R, color.G, color.B), 0.55));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            brush.Freeze();
            return brush;
        }
    }

    [JsonIgnore]
    public string LastRunLabel
    {
        get
        {
            if (LastRun is null)
            {
                return "Ещё не запускался";
            }

            var local = LastRun.Value.ToLocalTime();
            var today = DateTimeOffset.Now.Date;
            if (local.Date == today)
            {
                return $"Сегодня в {local:HH:mm}";
            }

            if (local.Date == today.AddDays(-1))
            {
                return $"Вчера в {local:HH:mm}";
            }

            return local.ToString("d MMMM, HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
        }
    }

    public event EventHandler<ScenarioAction>? ActionChanged;

    public void EnsureIcons()
    {
        foreach (var action in Actions)
        {
            action.RefreshIcon();
        }
    }

    public void RefreshCounters()
    {
        OnManyPropertiesChanged(nameof(ActionCount), nameof(ActionsSummary), nameof(HasActions), nameof(HasEnabledActions));
    }

    private void AttachActions(ObservableCollection<ScenarioAction> collection)
    {
        collection.CollectionChanged += Actions_CollectionChanged;
        foreach (var action in collection)
        {
            action.PropertyChanged += Action_PropertyChanged;
        }
    }

    private void DetachActions(ObservableCollection<ScenarioAction> collection)
    {
        collection.CollectionChanged -= Actions_CollectionChanged;
        foreach (var action in collection)
        {
            action.PropertyChanged -= Action_PropertyChanged;
        }
    }

    private void Actions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ScenarioAction action in e.OldItems)
            {
                action.PropertyChanged -= Action_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ScenarioAction action in e.NewItems)
            {
                action.PropertyChanged += Action_PropertyChanged;
            }
        }

        RefreshCounters();
        OnPropertyChanged(nameof(Actions));
    }

    private void Action_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScenarioAction.Name) || e.PropertyName == nameof(ScenarioAction.Kind))
        {
            OnPropertyChanged(nameof(Actions));
        }

        if (e.PropertyName != nameof(ScenarioAction.IconSource))
        {
            RefreshCounters();
        }

        if (sender is ScenarioAction action)
        {
            ActionChanged?.Invoke(this, action);
        }
    }
}
