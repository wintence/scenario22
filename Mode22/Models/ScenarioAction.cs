using System.Text.Json.Serialization;
using System.Windows.Media;
using Mode22.Services;

namespace Mode22.Models;

public sealed class ScenarioAction : ObservableObject
{
    private ActionKind _kind = ActionKind.Launch;
    private string _name = string.Empty;
    private string _target = string.Empty;
    private string _arguments = string.Empty;
    private string _processName = string.Empty;
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

            return "Без названия";
        }
    }

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            if (Kind == ActionKind.Close)
            {
                return string.IsNullOrWhiteSpace(ProcessName)
                    ? "Имя процесса не указано"
                    : $"Процесс: {NormalizeProcessName(ProcessName)}";
            }

            if (string.IsNullOrWhiteSpace(Target))
            {
                return "Путь не указан";
            }

            var target = Target.Trim();
            if (!string.IsNullOrWhiteSpace(Arguments))
            {
                return target + "  " + Arguments.Trim();
            }

            return target;
        }
    }

    [JsonIgnore]
    public string DelayLabel => DelaySeconds <= 0 ? "Сразу" : $"Через {DelaySeconds} с";

    [JsonIgnore]
    public string TypeLabel => Kind == ActionKind.Launch ? "Открыть" : "Закрыть";

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

    public void RefreshIcon()
    {
        string? path = null;
        if (!string.IsNullOrWhiteSpace(Target) && File.Exists(Target))
        {
            path = Target;
        }
        else if (Kind == ActionKind.Close && !string.IsNullOrWhiteSpace(ProcessName))
        {
            path = IconService.TryFindProcessPath(ProcessName);
        }

        IconSource = IconService.GetIcon(path);
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
