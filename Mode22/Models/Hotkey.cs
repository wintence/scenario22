using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Mode22.Models;

public sealed class Hotkey : IEquatable<Hotkey>
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public int VirtualKey { get; set; }

    [JsonIgnore]
    public bool IsEmpty => VirtualKey == 0;

    [JsonIgnore]
    public string Display
    {
        get
        {
            if (IsEmpty)
            {
                return "Без клавиши";
            }

            var parts = new List<string>(5);
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(KeyLabel(VirtualKey));
            return string.Join(" + ", parts);
        }
    }

    [JsonIgnore]
    public IReadOnlyList<string> Tokens
    {
        get
        {
            if (IsEmpty)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(5);
            if (Ctrl) result.Add("Ctrl");
            if (Alt) result.Add("Alt");
            if (Shift) result.Add("Shift");
            if (Win) result.Add("Win");
            result.Add(KeyLabel(VirtualKey));
            return result;
        }
    }

    public static Hotkey Create(ModifierKeys modifiers, int virtualKey)
    {
        return new Hotkey
        {
            Ctrl = modifiers.HasFlag(ModifierKeys.Control),
            Alt = modifiers.HasFlag(ModifierKeys.Alt),
            Shift = modifiers.HasFlag(ModifierKeys.Shift),
            Win = modifiers.HasFlag(ModifierKeys.Windows),
            VirtualKey = virtualKey
        };
    }

    public Hotkey Clone()
    {
        return new Hotkey
        {
            Ctrl = Ctrl,
            Alt = Alt,
            Shift = Shift,
            Win = Win,
            VirtualKey = VirtualKey
        };
    }

    public bool Equals(Hotkey? other)
    {
        if (other is null)
        {
            return false;
        }

        return Ctrl == other.Ctrl
               && Alt == other.Alt
               && Shift == other.Shift
               && Win == other.Win
               && VirtualKey == other.VirtualKey;
    }

    public override bool Equals(object? obj) => Equals(obj as Hotkey);

    public override int GetHashCode() => HashCode.Combine(Ctrl, Alt, Shift, Win, VirtualKey);

    public override string ToString() => Display;

    public static string KeyLabel(int virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            return "Num " + (virtualKey - 0x60);
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return "F" + (virtualKey - 0x6F);
        }

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x14 => "Caps Lock",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6A => "Num *",
            0x6B => "Num +",
            0x6D => "Num −",
            0x6E => "Num .",
            0x6F => "Num /",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "−",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => "Клавиша"
        };
    }
}
