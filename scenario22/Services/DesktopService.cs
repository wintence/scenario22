using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Scenario22.Models;

namespace Scenario22.Services;

/// <summary>
/// Applies Windows desktop settings: wallpaper and light/dark mode.
/// </summary>
public static class DesktopService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly HashSet<string> WallpaperExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".jpg", ".jpeg", ".png"
    };

    public static void SetWallpaper(string path)
    {
        var target = Environment.ExpandEnvironmentVariables((path ?? string.Empty).Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(Strings.T("L_RWallpaperMissing"));
        }

        if (!File.Exists(target))
        {
            throw new FileNotFoundException(Strings.T("L_RWallpaperMissing"), target);
        }

        if (!WallpaperExtensions.Contains(Path.GetExtension(target)))
        {
            throw new InvalidOperationException(Strings.T("L_RWallpaperFormat"));
        }

        string appliedPath;
        try
        {
            appliedPath = PrepareWallpaper(target);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Strings.T("L_RWallpaperFail") + ": " + ex.Message, ex);
        }

        if (!NativeMethods.SystemParametersInfo(
                NativeMethods.SpiSetDeskWallpaper,
                0,
                appliedPath,
                NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendChange))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), Strings.T("L_RWallpaperFail"));
        }
    }

    /// <summary>
    /// SPI_SETDESKWALLPAPER is only reliable with BMP, so other formats are
    /// converted into a temporary bitmap in the app data folder.
    /// </summary>
    private static string PrepareWallpaper(string path)
    {
        if (Path.GetExtension(path).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "scenario22");
        Directory.CreateDirectory(dataDirectory);

        var bitmapPath = Path.Combine(dataDirectory, "wallpaper.bmp");
        using var source = Image.FromFile(path);
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        bitmap.Save(bitmapPath, ImageFormat.Bmp);
        return bitmapPath;
    }

    public static void SetWindowsTheme(WindowsTheme theme)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKey)
                ?? throw new InvalidOperationException(Strings.T("L_RThemeFail"));

            var value = theme == WindowsTheme.Light ? 1 : 0;
            key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Strings.T("L_RThemeFail") + ": " + ex.Message, ex);
        }

        // Let running apps pick up the new immersive color set.
        NativeMethods.SendMessageTimeout(
            NativeMethods.HwndBroadcast,
            NativeMethods.WmSettingChange,
            IntPtr.Zero,
            "ImmersiveColorSet",
            NativeMethods.SmtoAbortIfHung,
            1000,
            out _);
    }
}
