using System.Diagnostics;
using System.Runtime.InteropServices;
using MacMode.Core.Logging;

namespace MacMode.Core.Hook;

public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _proc;
    private bool _disposed;

    /// <summary>
    /// Fired for every low-level keyboard event. Set e.Handled = true to suppress.
    /// </summary>
    public event EventHandler<KeyboardHookEventArgs>? KeyEvent;

    /// <summary>
    /// Fired when the health monitor detects the hook was silently removed and reinstalls it.
    /// </summary>
    public event Action? HookReinstalled;

    /// <summary>UTC timestamp of the last key event received by the hook callback.</summary>
    public DateTime LastEventTime { get; private set; } = DateTime.UtcNow;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Logger.Error($"SetWindowsHookEx failed with error {error}");
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {error}");
        }

        LastEventTime = DateTime.UtcNow;
        Logger.Info("Keyboard hook installed.");
    }

    /// <summary>
    /// Reinstalls the hook. Call from the UI thread. Returns true if reinstall was needed.
    /// </summary>
    public bool Reinstall()
    {
        Logger.Info("Reinstalling keyboard hook...");
        Uninstall();
        Install();
        HookReinstalled?.Invoke();
        return true;
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _proc = null;
            Logger.Info("Keyboard hook uninstalled.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        LastEventTime = DateTime.UtcNow;

        if (nCode >= 0)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();

                bool isKeyDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
                bool isKeyUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    var args = new KeyboardHookEventArgs(
                        (int)hookStruct.vkCode,
                        hookStruct.scanCode,
                        hookStruct.flags,
                        isKeyDown);

                    KeyEvent?.Invoke(this, args);

                    if (args.Handled)
                        return (IntPtr)1; // Suppress
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception in hook callback: {ex.Message}");
                // Pass through on error to avoid stuck keys
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Uninstall();
            _disposed = true;
        }
    }
}
