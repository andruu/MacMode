namespace MacMode.Core.Hook;

public class KeyboardHookEventArgs : EventArgs
{
    public int VirtualKeyCode { get; }
    public uint ScanCode { get; }
    public uint Flags { get; }
    public bool IsKeyDown { get; }
    public bool IsKeyUp => !IsKeyDown;
    public bool IsExtended => (Flags & NativeMethods.LLKHF_EXTENDED) != 0;
    public bool IsInjected => (Flags & NativeMethods.LLKHF_INJECTED) != 0;

    /// <summary>True if the key is Left Alt (VK_LMENU or VK_MENU without extended flag).</summary>
    public bool IsLeftAlt =>
        VirtualKeyCode == NativeMethods.VK_LMENU ||
        (VirtualKeyCode == NativeMethods.VK_MENU && !IsExtended);

    /// <summary>True if the key is Right Alt / AltGr (VK_RMENU or VK_MENU with extended flag).</summary>
    public bool IsRightAlt =>
        VirtualKeyCode == NativeMethods.VK_RMENU ||
        (VirtualKeyCode == NativeMethods.VK_MENU && IsExtended);

    /// <summary>Set to true by the handler to suppress the key from reaching other apps.</summary>
    public bool Handled { get; set; }

    public KeyboardHookEventArgs(int vkCode, uint scanCode, uint flags, bool isKeyDown)
    {
        VirtualKeyCode = vkCode;
        ScanCode = scanCode;
        Flags = flags;
        IsKeyDown = isKeyDown;
    }
}
