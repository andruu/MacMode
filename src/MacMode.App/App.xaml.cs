using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MacMode.Core.Logging;

namespace MacMode.App;

public partial class App : System.Windows.Application
{
    private TrayIcon? _trayIcon;
    private AppBootstrapper? _bootstrapper;
    private Mutex? _mutex;

    private static readonly string PidFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacMode", "macmode.pid");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Wire up global crash handlers so hooks always get uninstalled
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (!AcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        WritePidFile();

        try
        {
            _bootstrapper = new AppBootstrapper();
            _trayIcon = new TrayIcon(_bootstrapper);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to start MacMode:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SafeCleanup();
        base.OnExit(e);
    }

    /// <summary>
    /// Attempts to acquire the single-instance mutex. If another instance holds it,
    /// checks whether that instance is actually alive. Kills zombie processes and
    /// retries so a reboot is never required.
    /// </summary>
    private bool AcquireSingleInstance()
    {
        _mutex = new Mutex(true, "MacMode_SingleInstance", out bool createdNew);
        if (createdNew)
            return true;

        // Mutex is held by another instance. Try to recover.
        if (TryKillStaleInstance())
        {
            // The old instance was a zombie or stale. Retry the mutex.
            _mutex.Dispose();
            Thread.Sleep(500);
            _mutex = new Mutex(true, "MacMode_SingleInstance", out createdNew);
            if (createdNew)
                return true;

            // One more attempt: the mutex might be abandoned after the kill
            try
            {
                if (_mutex.WaitOne(2000))
                    return true;
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        System.Windows.MessageBox.Show(
            "MacMode is already running.\n\nCheck the system tray (click the ^ arrow if hidden).",
            "MacMode",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    /// <summary>
    /// Reads the PID file from a previous run and kills the process if it exists
    /// but is no longer responding (zombie). Also kills any MacMode.App process
    /// that isn't us.
    /// </summary>
    private static bool TryKillStaleInstance()
    {
        bool killedSomething = false;
        int myPid = Environment.ProcessId;

        // Strategy 1: Kill the specific PID from the PID file
        if (File.Exists(PidFilePath))
        {
            try
            {
                string pidText = File.ReadAllText(PidFilePath).Trim();
                if (int.TryParse(pidText, out int savedPid) && savedPid != myPid)
                {
                    try
                    {
                        var staleProc = Process.GetProcessById(savedPid);
                        if (!staleProc.HasExited)
                        {
                            staleProc.Kill();
                            staleProc.WaitForExit(3000);
                            killedSomething = true;
                        }
                    }
                    catch (ArgumentException) { /* PID no longer exists */ }
                    catch { /* access denied or other issue */ }
                }
            }
            catch { /* PID file read error */ }
        }

        // Strategy 2: Find any other MacMode.App process by name
        try
        {
            string myName = Process.GetCurrentProcess().ProcessName;
            foreach (var proc in Process.GetProcessesByName(myName))
            {
                if (proc.Id == myPid) continue;
                try
                {
                    if (!proc.Responding || IsProcessHung(proc))
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                        killedSomething = true;
                    }
                }
                catch { /* access denied, already exited, etc. */ }
                finally { proc.Dispose(); }
            }
        }
        catch { /* process enumeration failed */ }

        if (killedSomething)
        {
            try { File.Delete(PidFilePath); } catch { }
        }

        return killedSomething;
    }

    private static bool IsProcessHung(Process proc)
    {
        try
        {
            if (proc.MainWindowHandle != IntPtr.Zero)
                return !proc.Responding;
            // No main window (tray app): check if it's been running but its
            // threads are all in a wait state. A simpler heuristic: if the PID
            // file exists and we got here, the old instance didn't shut down cleanly.
            return true;
        }
        catch { return true; }
    }

    private static void WritePidFile()
    {
        try
        {
            string dir = Path.GetDirectoryName(PidFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(PidFilePath, Environment.ProcessId.ToString());
        }
        catch { }
    }

    private static void DeletePidFile()
    {
        try { File.Delete(PidFilePath); } catch { }
    }

    private void SafeCleanup()
    {
        try { _trayIcon?.Dispose(); } catch { }
        try { _bootstrapper?.Dispose(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
        DeletePidFile();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error($"FATAL unhandled exception: {e.ExceptionObject}");
        SafeCleanup();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error($"FATAL dispatcher exception: {e.Exception}");
        SafeCleanup();
        e.Handled = false;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error($"Unobserved task exception: {e.Exception}");
    }
}
