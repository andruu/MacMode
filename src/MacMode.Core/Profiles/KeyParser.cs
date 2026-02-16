using System.Windows.Forms;
using MacMode.Core.Engine;
using MacMode.Core.Hook;

namespace MacMode.Core.Profiles;

/// <summary>
/// Parses shortcut strings like "Ctrl+Shift+T" into modifier flags + virtual key codes.
/// </summary>
public static class KeyParser
{
    private static readonly Dictionary<string, int> NameToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = NativeMethods.VK_TAB,
        ["Space"] = NativeMethods.VK_SPACE,
        ["Backspace"] = NativeMethods.VK_BACK,
        ["F4"] = NativeMethods.VK_F4,
        ["["] = (int)Keys.OemOpenBrackets,
        ["]"] = (int)Keys.OemCloseBrackets,
        ["/"] = (int)Keys.OemQuestion,
        ["-"] = (int)Keys.OemMinus,
        ["="] = (int)Keys.Oemplus,
        ["."] = (int)Keys.OemPeriod,
        [","] = (int)Keys.Oemcomma,
        [";"] = (int)Keys.OemSemicolon,
        ["'"] = (int)Keys.OemQuotes,
        ["\\"] = (int)Keys.OemBackslash,
        ["`"] = (int)Keys.Oemtilde,
        ["Left"] = 0x25,
        ["Right"] = 0x27,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["Delete"] = 0x2E,
        ["Enter"] = 0x0D,
        ["Escape"] = 0x1B,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["PrintScreen"] = 0x2C,
    };

    /// <summary>
    /// Parses a trigger string like "Alt+Shift+T".
    /// Returns the modifier flags (excluding Alt, which is implicit) and the VK code of the main key.
    /// </summary>
    public static (ModifierFlags modifiers, int vk) ParseTrigger(string shortcut)
    {
        var parts = shortcut.Split('+');
        var modifiers = ModifierFlags.None;
        string? keyName = null;

        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            switch (trimmed.ToLowerInvariant())
            {
                case "alt":
                    // Alt is always implicit in triggers (Left Alt = Cmd)
                    break;
                case "shift":
                    modifiers |= ModifierFlags.Shift;
                    break;
                case "ctrl":
                    modifiers |= ModifierFlags.Ctrl;
                    break;
                default:
                    keyName = trimmed;
                    break;
            }
        }

        if (keyName == null)
            throw new ArgumentException($"No key found in trigger: {shortcut}");

        int vk = ResolveVk(keyName);
        return (modifiers, vk);
    }

    /// <summary>
    /// Parses an action string like "Ctrl+Shift+Tab".
    /// Returns an array of modifier VK codes and the main key VK code.
    /// </summary>
    public static (int[] modifierVks, int vk) ParseAction(string shortcut)
    {
        var parts = shortcut.Split('+');
        var modVks = new List<int>();
        string? keyName = null;

        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            switch (trimmed.ToLowerInvariant())
            {
                case "ctrl":
                    modVks.Add(NativeMethods.VK_LCONTROL);
                    break;
                case "shift":
                    modVks.Add(NativeMethods.VK_LSHIFT);
                    break;
                case "alt":
                    modVks.Add(NativeMethods.VK_LMENU);
                    break;
                case "win":
                    modVks.Add(NativeMethods.VK_LWIN);
                    break;
                default:
                    keyName = trimmed;
                    break;
            }
        }

        if (keyName == null)
            throw new ArgumentException($"No key found in action: {shortcut}");

        int vk = ResolveVk(keyName);
        return (modVks.ToArray(), vk);
    }

    /// <summary>
    /// Reverse lookup: converts a virtual key code back to its display name.
    /// Used by the hotkey recorder to build shortcut strings from captured keys.
    /// </summary>
    public static string? VkToName(int vk)
    {
        // Check the named keys dictionary first
        foreach (var kvp in NameToVk)
        {
            if (kvp.Value == vk)
                return kvp.Key;
        }

        // A-Z
        if (vk is >= 'A' and <= 'Z')
            return ((char)vk).ToString();

        // 0-9
        if (vk is >= '0' and <= '9')
            return ((char)vk).ToString();

        // F1-F24
        if (vk >= 0x70 && vk <= 0x87)
            return $"F{vk - 0x70 + 1}";

        return null;
    }

    private static int ResolveVk(string keyName)
    {
        if (NameToVk.TryGetValue(keyName, out int vk))
            return vk;

        // Single character: A-Z, 0-9
        if (keyName.Length == 1)
        {
            char c = char.ToUpperInvariant(keyName[0]);
            if (c is >= 'A' and <= 'Z')
                return c; // VK codes for A-Z match ASCII
            if (c is >= '0' and <= '9')
                return c; // VK codes for 0-9 match ASCII
        }

        // Try F-keys: F1..F24
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(keyName.AsSpan(1), out int fNum) &&
            fNum >= 1 && fNum <= 24)
        {
            return 0x70 + (fNum - 1); // VK_F1 = 0x70
        }

        throw new ArgumentException($"Unknown key name: {keyName}");
    }
}
