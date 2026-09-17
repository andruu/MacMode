using MacMode.Core.Hook;

namespace MacMode.Core.Engine;

/// <summary>
/// Observes standalone injected Windows-key taps. Never suppresses or generates
/// input. Physical Windows keys and MacMode's own output retain their behavior.
/// </summary>
public sealed class InjectedWindowsTap
{
    private readonly HashSet<int> _held = new();
    private int? _candidate;

    public void Reset()
    {
        _held.Clear();
        _candidate = null;
    }

    public void CancelTap() => _candidate = null;

    public bool Observe(KeyboardHookEventArgs e, bool enabled)
    {
        if (e.IsMacModeInjected)
            return false;
        if (!enabled)
        {
            Reset();
            return false;
        }

        int key = e.VirtualKeyCode;
        if (e.IsKeyDown)
        {
            if (_held.Contains(key))
                return false; // Auto-repeat is still one held key.
            bool alone = _held.Count == 0;
            _held.Add(key);
            _candidate = alone && e.IsInjected &&
                key is NativeMethods.VK_LWIN or NativeMethods.VK_RWIN ? key : null;
            return false;
        }

        bool activate = e.IsInjected && _candidate == key && _held.Count == 1;
        _held.Remove(key);
        _candidate = null;
        return activate;
    }
}
