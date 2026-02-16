using System.Diagnostics;
using System.Runtime.InteropServices;
using MacMode.Core.Engine;
using MacMode.Core.Logging;

namespace MacMode.Core.Hook;

/// <summary>
/// Low-level mouse hook that translates Alt+Click into Ctrl+Click.
/// 
/// Safety design: the hook callback never calls SendInput. It only suppresses
/// the event and signals a dedicated worker thread. The worker thread performs
/// the actual input injection off the hook callback, avoiding the Windows
/// 300ms hook timeout that caused crashes in the previous implementation.
/// 
/// A stuck-state watchdog automatically resets if the button-up is never
/// received within a timeout window.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private const int StuckTimeoutMs = 2000;

    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _proc;
    private bool _disposed;

    private readonly ModifierState _modState;
    private readonly Func<bool> _isEnabled;

    // Shared state between hook callback and worker thread.
    // volatile ensures visibility across threads.
    private volatile bool _swapped;
    private volatile bool _pendingDown;
    private volatile bool _pendingUp;
    private long _swapStartTick;

    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _worker;

    public MouseHook(ModifierState modState, Func<bool> isEnabled)
    {
        _modState = modState;
        _isEnabled = isEnabled;
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "MouseHook-Injector",
            Priority = ThreadPriority.AboveNormal
        };
        _worker.Start();
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            Logger.Error($"Mouse hook SetWindowsHookEx failed: {Marshal.GetLastWin32Error()}");
            return;
        }
        Logger.Info("Mouse hook installed.");
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _proc = null;
            _swapped = false;
            _pendingDown = false;
            _pendingUp = false;
            Logger.Info("Mouse hook uninstalled.");
        }
    }

    /// <summary>
    /// Hook callback. MUST return as fast as possible. Never calls SendInput.
    /// Sets a flag and signals the worker thread to do the actual injection.
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isEnabled())
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                bool injected = (info.flags & NativeMethods.LLMHF_INJECTED) != 0;
                int msg = wParam.ToInt32();

                if (!injected && msg == NativeMethods.WM_LBUTTONDOWN && _modState.LeftAltDown && !_swapped)
                {
                    _swapped = true;
                    _swapStartTick = Environment.TickCount64;
                    _pendingDown = true;
                    _signal.Set();
                    return (IntPtr)1;
                }

                if (!injected && msg == NativeMethods.WM_LBUTTONUP && _swapped)
                {
                    _swapped = false;
                    _pendingUp = true;
                    _signal.Set();
                    return (IntPtr)1;
                }
            }
            catch
            {
                // On any error, pass through to avoid stuck mouse
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// Dedicated thread that waits for signals from the hook callback and
    /// performs input injection safely outside the hook context.
    /// </summary>
    private void WorkerLoop()
    {
        while (!_disposed)
        {
            // Wait for a signal, but also wake periodically to check for stuck state
            _signal.WaitOne(500);

            if (_disposed) break;

            try
            {
                if (_pendingDown)
                {
                    _pendingDown = false;
                    Logger.Debug("Mouse hook: injecting Ctrl+Click down");
                    KeySender.SendBatch(new[]
                    {
                        KeySender.MakeKeyUp(NativeMethods.VK_LMENU),
                        KeySender.MakeKeyDown(NativeMethods.VK_LCONTROL),
                        KeySender.MakeMouseDown(),
                    });
                }

                if (_pendingUp)
                {
                    _pendingUp = false;
                    Logger.Debug("Mouse hook: injecting Ctrl+Click up, restoring Alt");
                    KeySender.SendBatch(new[]
                    {
                        KeySender.MakeMouseUp(),
                        KeySender.MakeKeyUp(NativeMethods.VK_LCONTROL),
                        KeySender.MakeKeyDown(NativeMethods.VK_LMENU),
                    });
                }

                // Watchdog: if we suppressed button-down but never got button-up,
                // force a cleanup so the mouse doesn't get stuck
                if (_swapped && Environment.TickCount64 - _swapStartTick > StuckTimeoutMs)
                {
                    Logger.Error("Mouse hook: stuck state detected, forcing cleanup");
                    _swapped = false;
                    _pendingDown = false;
                    _pendingUp = false;
                    KeySender.SendBatch(new[]
                    {
                        KeySender.MakeMouseUp(),
                        KeySender.MakeKeyUp(NativeMethods.VK_LCONTROL),
                        KeySender.MakeKeyDown(NativeMethods.VK_LMENU),
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Mouse hook worker error: {ex.Message}");
                _swapped = false;
                _pendingDown = false;
                _pendingUp = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _signal.Set(); // Wake the worker so it exits
        Uninstall();
        _signal.Dispose();
    }
}
