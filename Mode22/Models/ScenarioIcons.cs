using System.Windows;
using System.Windows.Media;

namespace Mode22.Models;

/// <summary>
/// A built-in scenario icon rendered as a white line glyph from Themes/Icons.xaml.
/// </summary>
public sealed record ScenarioIcon(string Key, string Name, string ResourceKey)
{
    private static readonly Geometry FallbackGeometry = new RectangleGeometry(new Rect(0, 0, 24, 24));

    public Geometry Geometry =>
        Application.Current?.TryFindResource(ResourceKey) as Geometry ?? FallbackGeometry;
}

public static class ScenarioIcons
{
    public static ScenarioIcon Default { get; } = new("sparkle", "Звезда", "IconSparkle");

    public static IReadOnlyList<ScenarioIcon> Options { get; } = new[]
    {
        Default,
        new ScenarioIcon("bolt", "Молния", "IconBolt"),
        new ScenarioIcon("play", "Старт", "IconPlay"),
        new ScenarioIcon("gamepad", "Игра", "IconGamepad"),
        new ScenarioIcon("briefcase", "Работа", "IconBriefcase"),
        new ScenarioIcon("moon", "Ночь", "IconMoon"),
        new ScenarioIcon("window", "Окно", "IconWindow"),
        new ScenarioIcon("folder", "Файлы", "IconFolder"),
        new ScenarioIcon("gear", "Система", "IconGear"),
        new ScenarioIcon("keyboard", "Клавиатура", "IconKeyboard"),
        new ScenarioIcon("bell", "Звонок", "IconBell"),
        new ScenarioIcon("clock", "Таймер", "IconClock"),
        new ScenarioIcon("power", "Питание", "IconPower"),
        new ScenarioIcon("external", "Ссылки", "IconExternal")
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
