using System.Globalization;
using System.Windows;

namespace Mode22.Services;

/// <summary>
/// Looks up localized UI strings in the shared string dictionaries.
/// Falls back to the key itself when a resource is missing.
/// </summary>
public static class Strings
{
    public static string T(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public static string T(string key, params object[] args)
    {
        return string.Format(LanguageService.Culture, T(key), args);
    }
}

/// <summary>
/// Applies the interface language by swapping the merged string dictionaries.
/// English is the default language; Russian is available from settings.
/// </summary>
public static class LanguageService
{
    private const string EnSource = "Themes/Strings.En.xaml";
    private const string RuSource = "Themes/Strings.Ru.xaml";

    private static readonly CultureInfo RuCulture = new("ru-RU");
    private static readonly CultureInfo EnCulture = new("en-US");

    public static bool IsRu { get; private set; }

    public static CultureInfo Culture => IsRu ? RuCulture : EnCulture;

    public static void Apply(string? language)
    {
        IsRu = string.Equals(language?.Trim(), "ru", StringComparison.OrdinalIgnoreCase);

        if (Application.Current is not { } application)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        var source = IsRu ? RuSource : EnSource;
        var newDictionary = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
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
            if (original.EndsWith("Themes/Strings.En.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Strings.Ru.xaml", StringComparison.OrdinalIgnoreCase))
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
            dictionaries.Add(newDictionary);
        }
    }
}
