using Microsoft.Win32;

namespace Scenario22.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "scenario22";

    public static (bool Success, string? Error) SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                var command = BuildCommand();
                if (string.IsNullOrWhiteSpace(command))
                {
                    return (false, Strings.T("L_StartupErr"));
                }

                key.SetValue(ValueName, command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes the autostart entry left by the pre-rename "Mode22" build.
    /// </summary>
    public static void CleanupLegacy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue("Mode22", throwOnMissingValue: false);
        }
        catch
        {
            // Legacy cleanup is best-effort.
        }
    }

    private static string BuildCommand()
    {
        // Environment.ProcessPath works for single-file publishes and normal builds alike.
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return string.Empty;
        }

        if (Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            // Running under `dotnet run`: point the launcher at the built dll next to the host.
            var name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var dll = Path.Combine(AppContext.BaseDirectory, name + ".dll");
            return $"\"{processPath}\" \"{dll}\" --tray";
        }

        return $"\"{processPath}\" --tray";
    }
}
