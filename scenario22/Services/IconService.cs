using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Scenario22.Services;

public static class IconService
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        return Cache.GetOrAdd(filePath, path =>
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon is null)
                {
                    return null;
                }

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(64, 64));
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
        });
    }

    private static readonly ConcurrentDictionary<string, ImageSource?> CustomCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ExtractionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".ico", ".lnk", ".cur", ".ani"
    };

    /// <summary>
    /// Loads a user-chosen icon: extracts associated icons for exe/dll/ico/lnk,
    /// decodes image files (png, jpg, bmp, gif) as-is. Results are cached.
    /// </summary>
    public static ImageSource? GetCustomIcon(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        return CustomCache.GetOrAdd(filePath, path =>
        {
            try
            {
                if (!ExtractionExtensions.Contains(Path.GetExtension(path)))
                {
                    var image = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    image.Freeze();
                    return image;
                }

                return GetIcon(path);
            }
            catch
            {
                return null;
            }
        });
    }

    public static string? TryFindProcessPath(string processName)
    {
        var name = Models.ScenarioAction.NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessesByName(name).FirstOrDefault();
            return process?.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
