using System.Windows;
using System.Windows.Media;
using Scenario22.Services;

namespace Scenario22.Services;

/// <summary>
/// Descriptor of a built-in interface theme with data for the settings preview cards.
/// </summary>
public sealed class ThemeInfo
{
    public required string Key { get; init; }
    public required string NameKey { get; init; }
    public required bool IsDark { get; init; }
    public required string Source { get; init; }
    public required Brush PreviewBackground { get; init; }
    public required Brush PreviewSidebar { get; init; }
    public required Brush PreviewAccent { get; init; }
    public required Brush PreviewLine { get; init; }

    public string Name => Strings.T(NameKey);
}

public static class ThemeService
{
    public static IReadOnlyList<ThemeInfo> Themes { get; } = new List<ThemeInfo>
    {
        Create(
            "light",
            "L_ThemeUiLight",
            isDark: false,
            "Themes/Light.xaml",
            background: "#F5F4F0",
            sidebar: "#EDEBE5",
            accent: "#4C82FF"),
        Create(
            "dark",
            "L_ThemeUiDark",
            isDark: true,
            "Themes/Dark.xaml",
            background: "#17171B",
            sidebar: "#131316",
            accent: "#6B97FF"),
        Create(
            "midnight",
            "L_ThemeUiMidnight",
            isDark: true,
            "Themes/Midnight.xaml",
            background: "#0E1220",
            sidebar: "#0A0E19",
            accent: "#7CB8FF"),
        Create(
            "nord",
            "L_ThemeUiNord",
            isDark: true,
            "Themes/Nord.xaml",
            background: "#2E3440",
            sidebar: "#272C36",
            accent: "#88C0D0"),
        Create(
            "cream",
            "L_ThemeUiCream",
            isDark: false,
            "Themes/Cream.xaml",
            background: "#F6F1E7",
            sidebar: "#EDE5D5",
            accent: "#C68A3F"),
    };

    public static bool IsDark { get; private set; }

    public static string CurrentKey { get; private set; } = "light";

    public static void Apply(string? key)
    {
        var theme = Resolve(key);

        if (Application.Current is null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var newDictionary = new ResourceDictionary
        {
            Source = new Uri(theme.Source, UriKind.Relative)
        };

        var existingIndex = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var item = dictionaries[i];
            if (item.Source is null)
            {
                continue;
            }

            var original = item.Source.OriginalString.Replace('\\', '/');
            if (original.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Midnight.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Nord.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Cream.xaml", StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            dictionaries[existingIndex] = newDictionary;
        }
        else
        {
            dictionaries.Insert(0, newDictionary);
        }

        IsDark = theme.IsDark;
        CurrentKey = theme.Key;
    }

    /// <summary>
    /// Legacy boolean entry point kept for compatibility with pre-1.7.1 callers.
    /// </summary>
    public static void Apply(bool dark)
    {
        Apply(dark ? "dark" : "light");
    }

    public static ThemeInfo Resolve(string? key)
    {
        var clean = key?.Trim().ToLowerInvariant() ?? string.Empty;
        return Themes.FirstOrDefault(x => x.Key == clean) ?? Themes[0];
    }

    private static ThemeInfo Create(
        string key,
        string nameKey,
        bool isDark,
        string source,
        string background,
        string sidebar,
        string accent)
    {
        var line = new SolidColorBrush(
            isDark ? Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x2E, 0x00, 0x00, 0x00));
        line.Freeze();

        return new ThemeInfo
        {
            Key = key,
            NameKey = nameKey,
            IsDark = isDark,
            Source = source,
            PreviewBackground = Brush(background),
            PreviewSidebar = Brush(sidebar),
            PreviewAccent = Brush(accent),
            PreviewLine = line
        };
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
