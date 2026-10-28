using System.Windows;

namespace Mode22.Services;

public static class ThemeService
{
    private const string LightSource = "Themes/Light.xaml";
    private const string DarkSource = "Themes/Dark.xaml";

    public static bool IsDark { get; private set; }

    public static void Apply(bool dark)
    {
        if (Application.Current is null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var source = dark ? DarkSource : LightSource;
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
            if (original.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase)
                || original.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase))
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

        IsDark = dark;
    }
}
