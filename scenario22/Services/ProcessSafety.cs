using Scenario22.Models;

namespace Scenario22.Services;

/// <summary>
/// Central list of processes that scenarios must never close.
/// </summary>
public static class ProcessSafety
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "mode22",
        "scenario22",
        "explorer",
        "winlogon",
        "csrss",
        "wininit",
        "services",
        "lsass",
        "smss",
        "dwm",
        "svchost",
        "system",
        "idle",
        "registry",
        "memory compression",
        "secure system",
        "fontdrvhost",
        "audiodg"
    };

    public static bool IsProtected(string processName)
    {
        var name = ScenarioAction.NormalizeProcessName(processName);
        return !string.IsNullOrWhiteSpace(name) && Protected.Contains(name);
    }
}
