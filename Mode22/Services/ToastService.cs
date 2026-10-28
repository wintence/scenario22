using System.Windows.Media;
using Mode22.Models;

namespace Mode22.Services;

public sealed class ToastService : IDisposable
{
    private ToastWindow? _window;
    private bool _disposed;

    public void ShowRunning(Scenario scenario, int totalActions)
    {
        EnsureWindow().ShowRunning(scenario, totalActions);
    }

    public void UpdateProgress(ScenarioRunProgress progress)
    {
        _window?.UpdateProgress(progress);
    }

    public void ShowCompleted(ScenarioRunResult result)
    {
        EnsureWindow().ShowCompleted(result);
    }

    public void ShowInfo(string title, string message, string emoji, Brush? accent = null, bool isError = false)
    {
        EnsureWindow().ShowInfo(title, message, emoji, accent, isError);
    }

    private ToastWindow EnsureWindow()
    {
        if (_window is null)
        {
            _window = new ToastWindow();
        }

        return _window;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window?.CloseImmediately();
        _window = null;
    }
}
