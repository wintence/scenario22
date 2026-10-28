using Microsoft.Win32;

namespace Mode22.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mode22";

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
                    return (false, "Не удалось определить путь к приложению для автозапуска.");
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

    private static string BuildCommand()
    {
        var processPath = Environment.ProcessPath
                          ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
                          ?? string.Empty;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return string.Empty;
        }

        if (Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var dll = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            return $"\"{processPath}\" \"{dll}\" --tray";
        }

        return $"\"{processPath}\" --tray";
    }
}
