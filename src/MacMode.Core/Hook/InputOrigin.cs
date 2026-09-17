namespace MacMode.Core.Hook;

/// <summary>Distinguishes our output from input injected by Synergy or accessibility tools.</summary>
public static class InputOrigin
{
    // Fits in both 32-bit and 64-bit ULONG_PTR values. This is a recursion marker,
    // not authentication or proof of which other process supplied an input event.
    public static readonly IntPtr MacModeTag = new(0x4D4D4F44);
}
