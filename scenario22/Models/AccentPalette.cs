using System.Windows;
using System.Windows.Media;
using Scenario22.Services;

namespace Scenario22.Models;

public sealed class AccentOption
{
    public required string Key { get; init; }
    public required string NameKey { get; init; }
    public string Name => Strings.T(NameKey);
    public required Color Color { get; init; }
    public required Color Color2 { get; init; }
    public required Brush PreviewBrush { get; init; }
}

public static class AccentPalette
{
    public const string CustomKey = "custom";
    public static readonly Color PlaceholderFrom = Color.FromRgb(0x4C, 0x82, 0xFF);
    public static readonly Color PlaceholderTo = Color.FromRgb(0x7C, 0x68, 0xF5);

    public static IReadOnlyList<AccentOption> Options { get; } = new List<AccentOption>
    {
        Create("blue", "L_AccentBlue", "#4C82FF", "#7968FF"),
        Create("violet", "L_AccentViolet", "#8B5CF6", "#C06CFF"),
        Create("pink", "L_AccentPink", "#F65C9D", "#FF8560"),
        Create("orange", "L_AccentAmber", "#F59E0B", "#F97316"),
        Create("green", "L_AccentMint", "#10B981", "#14B8A6"),
        Create("graphite", "L_AccentGraphite", "#4B5563", "#767C8A")
    };

    public static AccentOption Default => Options[0];

    public static AccentOption Get(string? key)
    {
        return Options.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    public static bool IsCustom(string? key)
    {
        return string.Equals(key, CustomKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseColor(string? hex, out Color color)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            color = default;
            return false;
        }

        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    /// <summary>
    /// Builds a linear gradient brush between two colors and freezes it.
    /// </summary>
    public static LinearGradientBrush CreateGradient(Color start, Color end)
    {
        var brush = new LinearGradientBrush(start, end, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Builds the extra "custom" palette entry shown in the scenario editor.
    /// Unknown or missing colors fall back to placeholder values.
    /// </summary>
    public static AccentOption CreateCustom(string? fromHex, string? toHex)
    {
        var from = TryParseColor(fromHex, out var parsedFrom) ? parsedFrom : PlaceholderFrom;
        var to = TryParseColor(toHex, out var parsedTo) ? parsedTo : from;
        return Create(CustomKey, "L_AccentCustom", from, to);
    }

    private static AccentOption Create(string key, string name, string start, string end)
    {
        TryParseColor(start, out var first);
        TryParseColor(end, out var second);
        return Create(key, name, first, second);
    }

    private static AccentOption Create(string key, string name, Color start, Color end)
    {
        return new AccentOption
        {
            Key = key,
            NameKey = name,
            Color = start,
            Color2 = end,
            PreviewBrush = CreateGradient(start, end)
        };
    }
}
