using Mode22.Models;

namespace Mode22.Services;

public sealed class HotkeyService : IDisposable
{
    private const int FirstId = 0xB600;
    private IntPtr _windowHandle;
    private readonly Dictionary<int, Scenario> _registered = new();
    private bool _disposed;

    public IReadOnlyList<Scenario> RegistrationFailures { get; private set; } = Array.Empty<Scenario>();

    public void Attach(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public void RegisterAll(IEnumerable<Scenario> scenarios, bool hotkeysEnabled)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterAllInternal();
        RegistrationFailures = Array.Empty<Scenario>();

        foreach (var scenario in scenarios)
        {
            scenario.HotkeyError = null;
        }

        if (!hotkeysEnabled)
        {
            return;
        }

        var failures = new List<Scenario>();
        var used = new HashSet<Hotkey>();
        var nextId = FirstId;

        foreach (var scenario in scenarios)
        {
            if (!scenario.IsEnabled || scenario.Hotkey is null || scenario.Hotkey.IsEmpty)
            {
                continue;
            }

            var hotkey = scenario.Hotkey;
            if (!used.Add(hotkey))
            {
                scenario.HotkeyError = "Сочетание уже используется другим сценарием";
                failures.Add(scenario);
                continue;
            }

            var modifiers = 0;
            if (hotkey.Ctrl) modifiers |= NativeMethods.ModControl;
            if (hotkey.Alt) modifiers |= NativeMethods.ModAlt;
            if (hotkey.Shift) modifiers |= NativeMethods.ModShift;
            if (hotkey.Win) modifiers |= NativeMethods.ModWin;
            modifiers |= NativeMethods.ModNoRepeat;

            if (NativeMethods.RegisterHotKey(_windowHandle, nextId, modifiers, (uint)hotkey.VirtualKey))
            {
                _registered[nextId] = scenario;
                nextId++;
            }
            else
            {
                scenario.HotkeyError = "Сочетание занято другой программой";
                failures.Add(scenario);
            }
        }

        RegistrationFailures = failures;
    }

    public Scenario? Resolve(int id)
    {
        return _registered.TryGetValue(id, out var scenario) ? scenario : null;
    }

    public void UnregisterAll()
    {
        UnregisterAllInternal();
    }

    private void UnregisterAllInternal()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            _registered.Clear();
            return;
        }

        foreach (var id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        _registered.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAllInternal();
    }
}
