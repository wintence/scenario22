using System.Windows;
using System.Windows.Media;
using Mode22.Services;

namespace Mode22.Models;

/// <summary>
/// A built-in scenario icon rendered as a white line glyph from Themes/Icons.xaml.
/// </summary>
public sealed record ScenarioIcon(string Key, string NameKey, string ResourceKey)
{
    private static readonly Geometry FallbackGeometry = new RectangleGeometry(new Rect(0, 0, 24, 24));

    public string Name => Strings.T(NameKey);

    public Geometry Geometry =>
        Application.Current?.TryFindResource(ResourceKey) as Geometry ?? FallbackGeometry;
}

public static class ScenarioIcons
{
    public static ScenarioIcon Default { get; } = new("sparkle", "L_IconStar", "IconSparkle");

    public static IReadOnlyList<ScenarioIcon> Options { get; } = new[]
    {
        Default,
        new ScenarioIcon("bolt", "L_IconBolt", "IconBolt"),
        new ScenarioIcon("play", "L_IconStart", "IconPlay"),
        new ScenarioIcon("gamepad", "L_IconGame", "IconGamepad"),
        new ScenarioIcon("briefcase", "L_IconWork", "IconBriefcase"),
        new ScenarioIcon("moon", "L_IconNight", "IconMoon"),
        new ScenarioIcon("window", "L_IconWindow", "IconWindow"),
        new ScenarioIcon("folder", "L_IconFiles", "IconFolder"),
        new ScenarioIcon("gear", "L_IconSystem", "IconGear"),
        new ScenarioIcon("keyboard", "L_IconKeys", "IconKeyboard"),
        new ScenarioIcon("bell", "L_IconBell", "IconBell"),
        new ScenarioIcon("clock", "L_IconTimer", "IconClock"),
        new ScenarioIcon("power", "L_IconPower", "IconPower"),
        new ScenarioIcon("external", "L_IconLinks", "IconExternal")
    };

    public static ScenarioIcon Get(string? key)
    {
        return Options.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    /// <summary>
    /// Maps a legacy emoji from old settings files to a matching built-in icon key.
    /// </summary>
    public static string ResolveFromEmoji(string? emoji)
    {
        return (emoji ?? string.Empty).Trim() switch
        {
            "🎮" => "gamepad",
            "💼" => "briefcase",
            "🌙" => "moon",
            "⚡" => "bolt",
            _ => Default.Key
        };
    }
}
