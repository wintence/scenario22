using System.Text.Json;
using System.Text.Json.Serialization;
using Scenario22.Models;

namespace Scenario22.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        DataDirectory = Path.Combine(appData, "scenario22");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        TryMigrateLegacyFolder(appData);
        Directory.CreateDirectory(DataDirectory);
    }

    /// <summary>
    /// Moves settings from the pre-rename "Mode22" folder so existing users keep their data.
    /// </summary>
    private void TryMigrateLegacyFolder(string appData)
    {
        var legacy = Path.Combine(appData, "Mode22");
        if (!Directory.Exists(legacy) || Directory.Exists(DataDirectory))
        {
            return;
        }

        try
        {
            Directory.Move(legacy, DataDirectory);
        }
        catch
        {
            // Migration is best-effort; a fresh data folder is created instead.
        }
    }

    public string DataDirectory { get; }

    public string SettingsPath { get; }

    public string? LastLoadWarning { get; private set; }

    public AppState Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppState();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();
            Normalize(state);
            NormalizeLanguage(state);
            return state;
        }
        catch (Exception ex)
        {
            LastLoadWarning = ex.Message;
            TryBackupCorruptFile();
            return CreateDefaultState();
        }
    }

    public void Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsPath, true);
        }
        catch
        {
            // A failed autosave should never interrupt the scenario or the UI.
        }
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(DataDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DataDirectory,
            UseShellExecute = true
        });
    }

    public static AppState CreateDefaultState()
    {
        return new AppState();
    }

    /// <summary>
    /// Restricts the saved interface language to the supported values, then applies
    /// it eagerly so localized strings resolve correctly during normalization.
    /// </summary>
    private static void NormalizeLanguage(AppState state)
    {
        var language = (state.Preferences.Language ?? "en").Trim().ToLowerInvariant();
        if (language is not ("ru" or "en"))
        {
            language = "en";
        }

        state.Preferences.Language = language;
        LanguageService.Apply(language);
    }

    private static void Normalize(AppState state)
    {
        state.SchemaVersion = Math.Max(1, state.SchemaVersion);
        state.Scenarios ??= new();
        state.Preferences ??= new AppPreferences();

        var seenIds = new HashSet<Guid>();
        foreach (var scenario in state.Scenarios)
        {
            if (scenario.Id == Guid.Empty || !seenIds.Add(scenario.Id))
            {
                scenario.Id = Guid.NewGuid();
                seenIds.Add(scenario.Id);
            }

            scenario.Name = string.IsNullOrWhiteSpace(scenario.Name) ? Strings.T("L_FallbackScenarioName") : scenario.Name.Trim();
            scenario.Description ??= string.Empty;
            scenario.Emoji = string.IsNullOrWhiteSpace(scenario.Emoji) ? "✨" : scenario.Emoji;
            scenario.AccentKey = AccentPalette.Get(scenario.AccentKey).Key;
            scenario.Actions ??= new();

            var knownIcon = ScenarioIcons.Options.Any(x =>
                string.Equals(x.Key, scenario.IconKey, StringComparison.OrdinalIgnoreCase));
            if (!knownIcon)
            {
                scenario.IconKey = ScenarioIcons.ResolveFromEmoji(scenario.Emoji);
            }

            if (scenario.CustomIconPath is { } iconPath && !File.Exists(iconPath))
            {
                scenario.CustomIconPath = null;
            }

            if (AccentPalette.IsCustom(scenario.AccentKey)
                && AccentPalette.TryParseColor(scenario.CustomAccentFrom, out _))
            {
                if (!AccentPalette.TryParseColor(scenario.CustomAccentTo, out _))
                {
                    scenario.CustomAccentTo = scenario.CustomAccentFrom;
                }
            }
            else
            {
                scenario.AccentKey = AccentPalette.Get(scenario.AccentKey).Key;
                scenario.CustomAccentFrom = null;
                scenario.CustomAccentTo = null;
            }

            if (scenario.Hotkey is { VirtualKey: <= 0 })
            {
                scenario.Hotkey = null;
            }

            foreach (var action in scenario.Actions)
            {
                if (action.Id == Guid.Empty)
                {
                    action.Id = Guid.NewGuid();
                }

                action.Name ??= string.Empty;
                action.Target ??= string.Empty;
                action.Arguments ??= string.Empty;
                action.ProcessName ??= string.Empty;
                action.DelaySeconds = Math.Clamp(action.DelaySeconds, 0, 600);

                if (!Enum.IsDefined(action.Kind))
                {
                    action.Kind = ActionKind.Launch;
                }

                if (!Enum.IsDefined(action.MusicSource))
                {
                    action.MusicSource = MusicSource.Spotify;
                }

                if (!Enum.IsDefined(action.DesktopTheme))
                {
                    action.DesktopTheme = WindowsTheme.Dark;
                }
            }
        }

        if (state.SelectedScenarioId is null || state.Scenarios.All(x => x.Id != state.SelectedScenarioId))
        {
            state.SelectedScenarioId = state.Scenarios.FirstOrDefault()?.Id;
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backup = SettingsPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(SettingsPath, backup, true);
        }
        catch
        {
            // Ignore backup failures.
        }
    }
}
