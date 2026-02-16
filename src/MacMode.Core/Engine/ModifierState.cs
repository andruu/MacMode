namespace MacMode.Core.Engine;

/// <summary>
/// Tracks the physical state of modifier keys as reported by the hook.
/// </summary>
public sealed class ModifierState
{
    public bool LeftAltDown { get; set; }
    public bool LeftShiftDown { get; set; }
    public bool RightShiftDown { get; set; }
    public bool LeftCtrlDown { get; set; }
    public bool RightCtrlDown { get; set; }

    public bool ShiftDown => LeftShiftDown || RightShiftDown;
    public bool CtrlDown => LeftCtrlDown || RightCtrlDown;

    /// <summary>
    /// Returns the active modifiers as flags (excluding Left Alt which is handled specially).
    /// </summary>
    public ModifierFlags ActiveModifiers
    {
        get
        {
            var flags = ModifierFlags.None;
            if (ShiftDown) flags |= ModifierFlags.Shift;
            if (CtrlDown) flags |= ModifierFlags.Ctrl;
            return flags;
        }
    }

    public void Reset()
    {
        LeftAltDown = false;
        LeftShiftDown = false;
        RightShiftDown = false;
        LeftCtrlDown = false;
        RightCtrlDown = false;
    }
}

[Flags]
public enum ModifierFlags
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,
}
