using System.Text.Json.Serialization;
using System.Windows.Media;
using Scenario22.Services;

namespace Scenario22.Models;

public sealed class ScenarioAction : ObservableObject
{
    private ActionKind _kind = ActionKind.Launch;
    private string _name = string.Empty;
    private string _target = string.Empty;
    private string _arguments = string.Empty;
    private string _processName = string.Empty;
    private MusicSource _musicSource = MusicSource.Spotify;
    private WindowsTheme _desktopTheme = WindowsTheme.Dark;
    private int _delaySeconds;
    private bool _isEnabled = true;
    private ImageSource? _iconSource;

    public Guid Id { get; set; } = Guid.NewGuid();

    public ActionKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                RefreshIcon();
                OnManyPropertiesChanged(nameof(Subtitle), nameof(DelayLabel), nameof(TypeLabel));
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Executable path, document path or protocol URL for launch actions.
    /// For close actions this may optionally hold the executable path used to resolve an icon.
    /// </summary>
    public string Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string Arguments
    {
        get => _arguments;
        set
        {
            if (SetProperty(ref _arguments, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string ProcessName
    {
        get => _processName;
        set
        {
            if (SetProperty(ref _processName, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    /// <summary>
    /// Music service used by <see cref="ActionKind.Music"/> actions.
    /// </summary>
    public MusicSource MusicSource
    {
        get => _musicSource;
        set
        {
            if (SetProperty(ref _musicSource, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    /// <summary>
    /// Light or dark Windows mode used by <see cref="ActionKind.Theme"/> actions.
    /// </summary>
    public WindowsTheme DesktopTheme
    {
        get => _desktopTheme;
        set
        {
            if (SetProperty(ref _desktopTheme, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public int DelaySeconds
    {
        get => _delaySeconds;
        set
        {
            var clean = Math.Clamp(value, 0, 600);
            if (SetProperty(ref _delaySeconds, clean))
            {
                OnPropertyChanged(nameof(DelayLabel));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name;
            }

            if (!string.IsNullOrWhiteSpace(ProcessName))
            {
                return ProcessName;
            }

            if (!string.IsNullOrWhiteSpace(Target))
            {
                try
                {
                    var candidate = Uri.TryCreate(Target, UriKind.Absolute, out var uri) && !uri.IsFile
                        ? uri.Host
                        : Path.GetFileNameWithoutExtension(Target);
                    return string.IsNullOrWhiteSpace(candidate) ? Target : candidate;
                }
                catch
                {
                    return Target;
                }
            }

            return Strings.T("L_Unnamed");
        }
    }

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            switch (Kind)
            {
                case ActionKind.Close:
                    return string.IsNullOrWhiteSpace(ProcessName)
                        ? Strings.T("L_NoProcessNamed")
                        : Strings.T("L_ProcessFmt", NormalizeProcessName(ProcessName));

                case ActionKind.Music:
                    return string.IsNullOrWhiteSpace(Target)
                        ? Strings.T("L_NoTrack")
                        : $"{Strings.T(MusicSource switch
                        {
                            MusicSource.Spotify => "L_MusicSpotify",
                            MusicSource.YandexMusic => "L_MusicYandex",
                            MusicSource.SoundCloud => "L_MusicSoundCloud",
                            _ => "L_MusicYouTube"
                        })} · {Target.Trim()}";

                case ActionKind.YouTube:
                    return string.IsNullOrWhiteSpace(Target)
                        ? Strings.T("L_NoQuery")
                        : "YouTube · " + Target.Trim();

                case ActionKind.Wallpaper:
                    return string.IsNullOrWhiteSpace(Target)
                        ? Strings.T("L_NoWallpaper")
                        : Target.Trim();

                case ActionKind.Theme:
                    return Strings.T(DesktopTheme == WindowsTheme.Light ? "L_ThemeLight" : "L_ThemeDark");

                default:
                    if (string.IsNullOrWhiteSpace(Target))
                    {
                        return Strings.T("L_NoTarget");
                    }

                    var target = Target.Trim();
                    if (!string.IsNullOrWhiteSpace(Arguments))
                    {
                        return target + "  " + Arguments.Trim();
                    }

                    return target;
            }
        }
    }

    [JsonIgnore]
    public string DelayLabel => DelaySeconds <= 0 ? Strings.T("L_DelayNow") : Strings.T("L_DelayInFmt", DelaySeconds);

    [JsonIgnore]
    public string TypeLabel => Strings.T(Kind switch
    {
        ActionKind.Launch => "L_TypeLaunch",
        ActionKind.Close => "L_TypeClose",
        ActionKind.Music => "L_TypeMusic",
        ActionKind.YouTube => "L_TypeYoutube",
        ActionKind.Wallpaper => "L_TypeWallpaper",
        _ => "L_TypeTheme"
    });

    [JsonIgnore]
    public ImageSource? IconSource
    {
        get => _iconSource;
        private set
        {
            if (SetProperty(ref _iconSource, value))
            {
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    [JsonIgnore]
    public bool HasIcon => IconSource is not null;

    /// <summary>
    /// Re-notifies localized computed labels after the interface language changes.
    /// </summary>
    public void RefreshLanguageDependent()
    {
        OnManyPropertiesChanged(nameof(DisplayName), nameof(Subtitle), nameof(DelayLabel), nameof(TypeLabel));
    }

    public void RefreshIcon()
    {
        ImageSource? icon = null;
        switch (Kind)
        {
            case ActionKind.Wallpaper:
                icon = IconService.GetCustomIcon(Target);
                break;

            case ActionKind.Close:
            {
                var path = !string.IsNullOrWhiteSpace(Target) && File.Exists(Target)
                    ? Target
                    : IconService.TryFindProcessPath(ProcessName);
                icon = IconService.GetIcon(path);
                break;
            }

            case ActionKind.Launch:
                icon = !string.IsNullOrWhiteSpace(Target) && File.Exists(Target)
                    ? IconService.GetIcon(Target)
                    : null;
                break;
        }

        IconSource = icon;
    }

    public ScenarioAction Clone()
    {
        return new ScenarioAction
        {
            Id = Id,
            Kind = Kind,
            Name = Name,
            Target = Target,
            Arguments = Arguments,
            ProcessName = ProcessName,
            MusicSource = MusicSource,
            DesktopTheme = DesktopTheme,
            DelaySeconds = DelaySeconds,
            IsEnabled = IsEnabled
        };
    }

    public static string NormalizeProcessName(string input)
    {
        var value = input.Trim().Trim('"');
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        if (value.Contains('\\') || value.Contains('/'))
        {
            try
            {
                value = Path.GetFileNameWithoutExtension(value);
            }
            catch
            {
                // Keep the original value.
            }
        }

        return value.Trim();
    }
}
