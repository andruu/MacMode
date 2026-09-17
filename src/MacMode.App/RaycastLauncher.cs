using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using MacMode.Core.Hook;
using MacMode.Core.Logging;

namespace MacMode.App;

internal static class RaycastLauncher
{
    private static int _pending;
    private static IntPtr _mainWindow;
    private static IntPtr _previousWindow;
    private static uint _previousProcess;

    public static void Toggle()
    {
        if (Interlocked.Exchange(ref _pending, 1) != 0)
            return;

        // Keep pipe IO off the keyboard hook. Talk to the existing instance;
        // launching Explorer on every tap causes the Windows busy cursor.
        _ = Task.Run(async () =>
        {
            try
            {
                IntPtr foreground = NativeMethods.GetForegroundWindow();
                if (IsRaycast(foreground))
                {
                    // Only dismiss the launcher we observed after opening it.
                    // Settings, shortcut recorders and other Raycast windows
                    // retain their input. Never close/quit the Raycast process.
                    if (foreground == _mainWindow && IsPreviousWindowValid())
                    {
                        bool restored = RestorePreviousWindow(foreground);
                        Logger.Info($"Raycast toggle returned focus to previous app: {restored}.");
                    }
                    return;
                }

                _previousWindow = foreground;
                NativeMethods.GetWindowThreadProcessId(foreground, out _previousProcess);
                using var pipe = new NamedPipeClientStream(".", "InterRaycastProductionNamedPipe",
                    PipeDirection.Out, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(500);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false));
                // Raycast 2.2's own InterRaycastPipe.Server accepts this message.
                // It follows the same ShowRaycast path used by its hotkey.
                await writer.WriteLineAsync("{\"type\":\"bring-to-front\",\"payload\":\"{}\"}");
                await writer.FlushAsync();
                Logger.Info("Requested Raycast show through its existing instance.");

                _mainWindow = IntPtr.Zero;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    await Task.Delay(25);
                    IntPtr shown = NativeMethods.GetForegroundWindow();
                    if (IsRaycast(shown))
                    {
                        _mainWindow = shown;
                        Logger.Info("Raycast launcher is focused; next standalone tap will dismiss it.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Raycast toggle failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _pending, 0);
            }
        });
    }

    private static bool IsRaycast(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(window, out uint processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("Raycast", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }

    private static bool IsPreviousWindowValid()
    {
        if (_previousWindow == IntPtr.Zero || !IsWindow(_previousWindow) ||
            !NativeMethods.IsWindowVisible(_previousWindow)) return false;
        NativeMethods.GetWindowThreadProcessId(_previousWindow, out uint processId);
        return processId == _previousProcess && !IsRaycast(_previousWindow);
    }

    private static bool RestorePreviousWindow(IntPtr launcher)
    {
        if (NativeMethods.GetForegroundWindow() != launcher || !IsPreviousWindowValid())
            return false;
        NativeMethods.SetForegroundWindow(_previousWindow);
        if (NativeMethods.GetForegroundWindow() == _previousWindow)
            return true;

        // The worker is a background thread, so Windows can reject an ordinary
        // foreground request even though the user explicitly tapped the hotkey.
        // Give this worker a message queue and briefly join the current foreground
        // input queue. The keyboard hook stays on its separate UI thread.
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        uint currentThread = GetCurrentThreadId();
        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(launcher, out _);
        uint targetThread = NativeMethods.GetWindowThreadProcessId(_previousWindow, out _);
        if (foregroundThread == 0 || foregroundThread == currentThread ||
            targetThread == 0 || IsHungAppWindow(launcher) || IsHungAppWindow(_previousWindow) ||
            NativeMethods.GetForegroundWindow() != launcher || !IsPreviousWindowValid())
            return false;

        if (!AttachThreadInput(currentThread, foregroundThread, true))
        {
            Logger.Info($"Raycast focus handoff could not join foreground input queue: {Marshal.GetLastWin32Error()}.");
            return false;
        }
        bool targetAttached = false;
        try
        {
            // Joining only the worker and Raycast is insufficient when the
            // destination owns a separate input queue. Include that destination
            // in the short handoff, and detach both pairs on every exit path.
            if (foregroundThread != targetThread)
            {
                targetAttached = AttachThreadInput(foregroundThread, targetThread, true);
                if (!targetAttached)
                {
                    Logger.Info($"Raycast focus handoff could not join destination input queue: {Marshal.GetLastWin32Error()}.");
                    return false;
                }
            }
            // No await, input injection, or window closing while attached.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (NativeMethods.GetForegroundWindow() == _previousWindow) return true;
                if (NativeMethods.GetForegroundWindow() != launcher || !IsPreviousWindowValid())
                    return false;
                NativeMethods.SetForegroundWindow(_previousWindow);
                // The API can return before the destination processes activation.
                Thread.Sleep(15);
            }
            return NativeMethods.GetForegroundWindow() == _previousWindow;
        }
        finally
        {
            if (targetAttached && !AttachThreadInput(foregroundThread, targetThread, false))
                Logger.Error($"Raycast focus handoff could not detach destination input queue: {Marshal.GetLastWin32Error()}.");
            if (!AttachThreadInput(currentThread, foregroundThread, false))
                Logger.Error($"Raycast focus handoff could not detach foreground input queue: {Marshal.GetLastWin32Error()}.");
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsHungAppWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint fromThread, uint toThread,
        [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out WindowMessage message, IntPtr window,
        uint min, uint max, uint remove);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMessage
    {
        public IntPtr Window;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
}
