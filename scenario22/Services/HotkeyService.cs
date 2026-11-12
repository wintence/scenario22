using System.Runtime.InteropServices;
using System.Windows.Input;
using Scenario22.Models;

namespace Scenario22.Services;

public sealed class HotkeyService : IDisposable
{
    private const int FirstId = 0xB600;
    private IntPtr _windowHandle;
    private readonly Dictionary<int, Scenario> _registered = new();
    private readonly List<ListenEntry> _listened = new();
    private IntPtr _hookHandle;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private readonly HashSet<int> _pressedKeys = new();
    private ModifierKeys _hookModifiers = ModifierKeys.None;
    private bool _disposed;

    public IReadOnlyList<Scenario> RegistrationFailures { get; private set; } = Array.Empty<Scenario>();

    /// <summary>
    /// Raised when a pass-through hotkey is pressed. The key is never blocked,
    /// so other applications (for example MyDockFinder) still receive it.
    /// </summary>
    public event EventHandler<Scenario>? HotkeyListened;

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

        if (hotkeysEnabled)
        {
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
                    scenario.HotkeyError = Strings.T("L_HkDup");
                    failures.Add(scenario);
                    continue;
                }

                if (scenario.HotkeyPassthrough)
                {
                    // Pass-through hotkeys are watched with a keyboard hook instead of
                    // RegisterHotKey, so the combination still reaches other apps.
                    _listened.Add(new ListenEntry(hotkey, scenario));
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
                    scenario.HotkeyError = Strings.T("L_HkBusy");
                    failures.Add(scenario);
                }
            }

            RegistrationFailures = failures;
        }

        UpdateHook();
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
            _listened.Clear();
            _hookHandle = IntPtr.Zero;
            return;
        }

        foreach (var id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        _registered.Clear();
        _listened.Clear();
        UninstallHook();
    }

    private void UpdateHook()
    {
        if (_listened.Count > 0)
        {
            InstallHook();
        }
        else
        {
            UninstallHook();
        }
    }

    private void InstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookProc ??= ListenCallback;
        _pressedKeys.Clear();
        _hookModifiers = ModifierKeys.None;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookProc,
            IntPtr.Zero,
            0);
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _pressedKeys.Clear();
        _hookModifiers = ModifierKeys.None;
    }

    private IntPtr ListenCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

            if (isDown || isUp)
            {
                var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);

                // Injected input (SendInput) is ignored so "Hotkey" actions
                // of running scenarios do not re-trigger pass-through hotkeys.
                if ((info.Flags & NativeMethods.LlkhfInjected) == 0)
                {
                    HandleKeyEvent(isDown, (int)info.VirtualKeyCode);
                }
            }
        }

        // The key is always passed on: other applications keep receiving it.
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleKeyEvent(bool isDown, int virtualKey)
    {
        if (TryGetModifier(virtualKey, out var modifier))
        {
            if (isDown)
            {
                _hookModifiers |= modifier;
            }
            else
            {
                _hookModifiers &= ~modifier;
            }

            return;
        }

        if (isDown)
        {
            // Ignore auto-repeat: the key must go up before it can trigger again.
            if (!_pressedKeys.Add(virtualKey))
            {
                return;
            }

            foreach (var entry in _listened)
            {
                if (Matches(entry.Hotkey, virtualKey))
                {
                    HotkeyListened?.Invoke(this, entry.Scenario);
                    break;
                }
            }
        }
        else
        {
            _pressedKeys.Remove(virtualKey);
        }
    }

    private bool Matches(Hotkey hotkey, int virtualKey)
    {
        return hotkey.VirtualKey == virtualKey
               && hotkey.Ctrl == _hookModifiers.HasFlag(ModifierKeys.Control)
               && hotkey.Alt == _hookModifiers.HasFlag(ModifierKeys.Alt)
               && hotkey.Shift == _hookModifiers.HasFlag(ModifierKeys.Shift)
               && hotkey.Win == _hookModifiers.HasFlag(ModifierKeys.Windows);
    }

    private static bool TryGetModifier(int virtualKey, out ModifierKeys modifier)
    {
        switch (virtualKey)
        {
            case 0x11 or 0xA2 or 0xA3:
                modifier = ModifierKeys.Control;
                return true;

            case 0x12 or 0xA4 or 0xA5:
                modifier = ModifierKeys.Alt;
                return true;

            case 0x10 or 0xA0 or 0xA1:
                modifier = ModifierKeys.Shift;
                return true;

            case 0x5B or 0x5C:
                modifier = ModifierKeys.Windows;
                return true;

            default:
                modifier = ModifierKeys.None;
                return false;
        }
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

    private sealed record ListenEntry(Hotkey Hotkey, Scenario Scenario);
}
