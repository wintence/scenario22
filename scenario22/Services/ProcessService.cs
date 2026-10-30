using System.Diagnostics;
using Scenario22.Models;

namespace Scenario22.Services;

/// <summary>
/// Enumerates running processes for the picker dialog.
/// </summary>
public static class ProcessService
{
    public static IReadOnlyList<RunningProcessInfo> GetRunningProcesses()
    {
        var result = new List<RunningProcessInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    // Access to the module is denied for some system processes.
                }

                var key = string.IsNullOrWhiteSpace(path) ? name : path;
                if (!seen.Add(key))
                {
                    continue;
                }

                var title = string.Empty;
                try
                {
                    title = process.MainWindowTitle ?? string.Empty;
                }
                catch
                {
                    // The window title is unavailable for background processes.
                }

                result.Add(new RunningProcessInfo(
                    name,
                    path ?? string.Empty,
                    title,
                    ProcessSafety.IsProtected(name),
                    string.IsNullOrWhiteSpace(path) ? null : IconService.GetIcon(path)));
            }
        }

        return result
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
