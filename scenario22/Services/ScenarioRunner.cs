using System.Diagnostics;
using Scenario22.Models;

namespace Scenario22.Services;

public sealed record ScenarioRunProgress(int Completed, int Total, string CurrentAction, int Percent);

public sealed record ScenarioRunError(string ActionName, string Message);

public sealed class ScenarioRunResult
{
    public required Scenario Scenario { get; init; }
    public int TotalActions { get; init; }
    public int CompletedActions { get; init; }
    public int ClosedProcesses { get; init; }
    public int LaunchedProcesses { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ScenarioRunError> Errors { get; init; } = Array.Empty<ScenarioRunError>();
    public bool Success => Errors.Count == 0;
}

public sealed class ScenarioRunner
{
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "mode22",
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
        "registry"
    };

    public async Task<ScenarioRunResult> RunAsync(
        Scenario scenario,
        IProgress<ScenarioRunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var actions = scenario.Actions
            .Where(x => x.IsEnabled)
            .Select(x => x.Clone())
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<ScenarioRunError>();
        var completed = 0;
        var launched = 0;
        var closedProcesses = 0;

        for (var index = 0; index < actions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var action = actions[index];

            progress?.Report(new ScenarioRunProgress(
                index,
                actions.Count,
                action.DisplayName,
                actions.Count == 0 ? 0 : (int)Math.Round(index * 100d / actions.Count)));

            try
            {
                if (action.DelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(action.DelaySeconds), cancellationToken).ConfigureAwait(false);
                }

                if (action.Kind == ActionKind.Launch)
                {
                    await Task.Run(() => Launch(action), cancellationToken).ConfigureAwait(false);
                    launched++;
                }
                else
                {
                    var result = await Task.Run(() => Close(action), cancellationToken).ConfigureAwait(false);
                    closedProcesses += result;
                }

                completed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new ScenarioRunError(action.DisplayName, FriendlyMessage(ex)));
            }

            progress?.Report(new ScenarioRunProgress(
                index + 1,
                actions.Count,
                action.DisplayName,
                actions.Count == 0 ? 0 : (int)Math.Round((index + 1) * 100d / actions.Count)));
        }

        stopwatch.Stop();
        return new ScenarioRunResult
        {
            Scenario = scenario,
            TotalActions = actions.Count,
            CompletedActions = completed,
            LaunchedProcesses = launched,
            ClosedProcesses = closedProcesses,
            Duration = stopwatch.Elapsed,
            Errors = errors
        };
    }

    private static void Launch(ScenarioAction action)
    {
        var target = Environment.ExpandEnvironmentVariables(action.Target.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(Strings.T("L_RNoPath"));
        }

        var arguments = Environment.ExpandEnvironmentVariables(action.Arguments ?? string.Empty);
        var info = new ProcessStartInfo
        {
            FileName = target,
            Arguments = arguments,
            UseShellExecute = true
        };

        if (File.Exists(target))
        {
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                info.WorkingDirectory = directory;
            }
        }
        else if (Path.IsPathRooted(target))
        {
            throw new FileNotFoundException(Strings.T("L_RFileNotFound"));
        }

        var process = Process.Start(info);
        if (process is null)
        {
            throw new InvalidOperationException(Strings.T("L_RNoStart"));
        }

        process.Dispose();
    }

    private static int Close(ScenarioAction action)
    {
        var name = ScenarioAction.NormalizeProcessName(action.ProcessName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(Strings.T("L_RNoProcess"));
        }

        if (ProtectedProcesses.Contains(name))
        {
            throw new InvalidOperationException(Strings.T("L_RProtected"));
        }

        var currentId = Environment.ProcessId;
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(name);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Strings.T("L_RProcListFail") + ex.Message, ex);
        }

        processes = processes.Where(p => p.Id != currentId).ToArray();
        if (processes.Length == 0)
        {
            return 0;
        }

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }
            }
            catch
            {
                // Force-close below.
            }
        }

        Thread.Sleep(550);

        var closed = 0;
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }

                closed++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(Strings.T("L_RCloseFail", name) + ": " + ex.Message, ex);
            }
            finally
            {
                process.Dispose();
            }
        }

        return closed;
    }

    private static string FriendlyMessage(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => Strings.T("L_RFileNotFound"),
            UnauthorizedAccessException => Strings.T("L_RNoAccess"),
            System.ComponentModel.Win32Exception win32 => win32.NativeErrorCode switch
            {
                2 => Strings.T("L_RFileNotFound"),
                5 => Strings.T("L_RNoAccess"),
                _ => win32.Message
            },
            _ => ex.Message
        };
    }
}
