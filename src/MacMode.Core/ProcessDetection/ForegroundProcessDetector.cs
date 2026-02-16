using System.Diagnostics;
using MacMode.Core.Hook;
using MacMode.Core.Logging;

namespace MacMode.Core.ProcessDetection;

/// <summary>
/// Detects the foreground process name. Caches the result to avoid
/// expensive Process.GetProcessById calls on every keystroke.
/// </summary>
public sealed class ForegroundProcessDetector
{
    private IntPtr _lastHwnd;
    private string _lastProcessName = string.Empty;
    private DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Returns the process name (without extension) of the current foreground window.
    /// </summary>
    public string GetForegroundProcessName()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        DateTime now = DateTime.UtcNow;

        // Return cached value if same window and recent
        if (hwnd == _lastHwnd && now - _lastCheck < CacheTimeout)
            return _lastProcessName;

        _lastHwnd = hwnd;
        _lastCheck = now;

        if (hwnd == IntPtr.Zero)
        {
            _lastProcessName = string.Empty;
            return _lastProcessName;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            _lastProcessName = proc.ProcessName.ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Logger.Debug($"Could not get process name: {ex.Message}");
            _lastProcessName = string.Empty;
        }

        return _lastProcessName;
    }
}
