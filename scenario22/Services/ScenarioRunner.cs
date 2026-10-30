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

                switch (action.Kind)
                {
                    case ActionKind.Launch:
                        await Task.Run(() => Launch(action), cancellationToken).ConfigureAwait(false);
                        launched++;
                        break;

                    case ActionKind.Close:
                        closedProcesses += await Task.Run(() => Close(action), cancellationToken).ConfigureAwait(false);
                        break;

                    case ActionKind.Music:
                        await Task.Run(() => OpenMusic(action), cancellationToken).ConfigureAwait(false);
                        launched++;
                        break;

                    case ActionKind.YouTube:
                        await Task.Run(() => OpenYouTube(action), cancellationToken).ConfigureAwait(false);
                        launched++;
                        break;

                    case ActionKind.Wallpaper:
                        await Task.Run(() => DesktopService.SetWallpaper(action.Target), cancellationToken).ConfigureAwait(false);
                        break;

                    case ActionKind.Theme:
                        await Task.Run(() => DesktopService.SetWindowsTheme(action.DesktopTheme), cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        throw new InvalidOperationException(Strings.T("L_RUnknownAction"));
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

    private static void OpenMusic(ScenarioAction action)
    {
        var target = (action.Target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(Strings.T("L_RNoMusic"));
        }

        OpenUrl(BuildMusicUrl(action.MusicSource, target));
    }

    private static void OpenYouTube(ScenarioAction action)
    {
        var target = (action.Target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(Strings.T("L_RNoYouTube"));
        }

        if (IsWebLink(target))
        {
            OpenUrl(target);
            return;
        }

        if (IsYouTubeVideoId(target))
        {
            OpenUrl("https://www.youtube.com/watch?v=" + target);
            return;
        }

        OpenUrl("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(target));
    }

    private static string BuildMusicUrl(MusicSource source, string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (source == MusicSource.Spotify && TryConvertSpotifyLink(uri, out var deepLink))
            {
                return deepLink;
            }

            if (uri.Scheme is "http" or "https" or "spotify" or "yandexmusic" or "soundcloud" or "youtube")
            {
                return target;
            }
        }

        var query = Uri.EscapeDataString(target);
        return source switch
        {
            MusicSource.Spotify => "spotify:search:" + query,
            MusicSource.YandexMusic => "https://music.yandex.ru/search?text=" + query,
            MusicSource.SoundCloud => "https://soundcloud.com/search?q=" + query,
            MusicSource.YouTubeMusic => "https://music.youtube.com/search?q=" + query,
            _ => throw new InvalidOperationException(Strings.T("L_RUnknownAction"))
        };
    }

    /// <summary>
    /// Turns open.spotify.com links into spotify: URIs, which start playback in the desktop app.
    /// </summary>
    private static bool TryConvertSpotifyLink(Uri uri, out string deepLink)
    {
        deepLink = string.Empty;
        if (!uri.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("play.spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !x.StartsWith("intl-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (segments.Length < 2)
        {
            return false;
        }

        var type = segments[0].ToLowerInvariant();
        if (type is not ("track" or "album" or "playlist" or "artist" or "episode" or "show"))
        {
            return false;
        }

        deepLink = $"spotify:{type}:{segments[1]}";
        return true;
    }

    private static bool IsWebLink(string target)
    {
        return Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsYouTubeVideoId(string target)
    {
        if (target.Length != 11)
        {
            return false;
        }

        foreach (var ch in target)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static void OpenUrl(string url)
    {
        var info = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException(Strings.T("L_RNoStart"));
    }

    private static int Close(ScenarioAction action)
    {
        var name = ScenarioAction.NormalizeProcessName(action.ProcessName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(Strings.T("L_RNoProcess"));
        }

        if (ProcessSafety.IsProtected(name))
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
                1155 => Strings.T("L_RNoHandler"),
                _ => win32.Message
            },
            _ => ex.Message
        };
    }
}
