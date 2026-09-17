using System.Runtime.InteropServices;
using System.Diagnostics;
using MacMode.Core.Engine;
using MacMode.Core.Logging;

namespace MacMode.App;

internal sealed class RemoteInputPower : IDisposable
{
    private readonly Func<bool> _enabled;
    private readonly RemoteInputActivity _activity;
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _worker;
    private readonly TimeSpan _interval;
    private int _disposed;
    private long _lastLogTick = long.MinValue;

    public RemoteInputPower(Func<bool> enabled)
        : this(enabled, SetThreadExecutionState, TimeSpan.FromSeconds(2)) { }

    internal RemoteInputPower(Func<bool> enabled, Func<uint, uint> reset, TimeSpan interval)
    {
        _enabled = enabled;
        _activity = new RemoteInputActivity(reset);
        _interval = interval;
        _worker = new Thread(WorkerLoop)
        {
            Name = "MacMode-RemoteInputPower",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
        _worker.Start();
        if (_enabled()) Logger.Info("Remote-input idle refresh is enabled on a separate background worker.");
    }

    public void Observe(bool injected, bool ownInput)
    {
        // Hook path: only read flags and set one atomic pending bit. No power
        // APIs, logging, thread waits, or callbacks onto the input dispatcher.
        if (Volatile.Read(ref _disposed) == 0 && _enabled())
            _activity.Observe(injected, ownInput);
    }

    private void WorkerLoop()
    {
        try
        {
            while (!_stop.WaitOne(_interval))
            {
                try { Refresh(); }
                catch (Exception ex) { Logger.Error($"Remote-input idle refresh failed: {ex.Message}"); }
            }
        }
        finally
        {
            _activity.Flush(false);
            _stop.Dispose();
        }
    }

    private void Refresh()
    {
        long started = Stopwatch.GetTimestamp();
        bool? result = _activity.Flush(_enabled());
        if (result == null) return;
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long now = Environment.TickCount64;
        if (_lastLogTick == long.MinValue || now - _lastLogTick >= 300_000 || result == false)
        {
            _lastLogTick = now;
            if (result == true)
                Logger.Info($"Background remote-input idle refresh accepted in {elapsed:F1} ms; successful resets: {_activity.SuccessfulResetCount}.");
            else
                Logger.Error("Windows rejected the remote-input idle-timer refresh.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Set();
        // Never wait for a slow power call on the input thread during shutdown.
        // No persistent power request was created, so none needs clearing.
    }

    internal bool WaitForStopped(TimeSpan timeout) => _worker.Join(timeout);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
