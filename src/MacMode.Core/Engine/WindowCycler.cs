using MacMode.Core.Hook;
using MacMode.Core.Logging;

namespace MacMode.Core.Engine;

/// <summary>
/// Cycles between open windows of the same application, replicating
/// the macOS Cmd+` (cycle windows) behavior on Windows.
/// </summary>
public static class WindowCycler
{
    /// <summary>
    /// Closes the active window by sending WM_SYSCOMMAND SC_CLOSE (the exact
    /// message Alt+F4 generates) followed by WM_CLOSE as a fallback.
    /// More reliable than synthesized Alt+F4 across different app types.
    /// </summary>
    public static void CloseActiveWindow()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        Logger.Info($"WindowCycler: closing window 0x{hwnd:X}");
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_CLOSE, IntPtr.Zero);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Finds all visible top-level windows belonging to the same process
    /// as the current foreground window, then activates the next one in
    /// z-order (the window directly behind the current one).
    /// </summary>
    public static void CycleNextWindow()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            Logger.Debug("WindowCycler: no foreground window");
            return;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundPid);

        var windows = GetAppWindows(foregroundPid);

        if (windows.Count <= 1)
        {
            Logger.Debug($"WindowCycler: only {windows.Count} window(s) for PID {foregroundPid}, nothing to cycle");
            return;
        }

        int currentIndex = windows.IndexOf(foreground);
        int nextIndex = (currentIndex + 1) % windows.Count;
        IntPtr nextWindow = windows[nextIndex];

        Logger.Info($"WindowCycler: cycling from window 0x{foreground:X} to 0x{nextWindow:X} ({windows.Count} windows for PID {foregroundPid})");

        if (NativeMethods.IsIconic(nextWindow))
            NativeMethods.ShowWindow(nextWindow, NativeMethods.SW_RESTORE);

        NativeMethods.SetForegroundWindow(nextWindow);
    }

    /// <summary>
    /// Enumerates all visible, taskbar-worthy windows belonging to the given process.
    /// Returns them in z-order (topmost first) since that's how EnumWindows iterates.
    /// </summary>
    private static List<IntPtr> GetAppWindows(uint processId)
    {
        var windows = new List<IntPtr>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);

            if (pid == processId && NativeMethods.IsWindowVisible(hWnd) && IsAppWindow(hWnd))
            {
                windows.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Determines if a window is an "app window" that would appear on the taskbar.
    /// Filters out tool windows, tooltips, and other non-primary windows.
    /// </summary>
    private static bool IsAppWindow(IntPtr hWnd)
    {
        int exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE);

        // Skip tool windows (floating palettes, etc.)
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            return false;

        // Skip non-activatable windows
        if ((exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0)
            return false;

        // Must have a title (filters out hidden helper windows)
        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;

        // Owned windows don't appear on taskbar unless they have WS_EX_APPWINDOW
        IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
        if (owner != IntPtr.Zero && (exStyle & NativeMethods.WS_EX_APPWINDOW) == 0)
            return false;

        return true;
    }
}
