using System.Diagnostics;
using MacMode.Core.Logging;
using Microsoft.Win32;

namespace MacMode.Core.Settings;

public enum StartupMode
{
    None,
    Registry,
    TaskScheduler
}

/// <summary>
/// Manages "Start on Login" via either Registry Run key (normal) or
/// Task Scheduler with "Run with highest privileges" (admin).
/// </summary>
public static class StartupManager
{
    private const string TaskName = "MacMode";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static StartupMode GetCurrentMode()
    {
        if (TaskExists()) return StartupMode.TaskScheduler;
        if (RegistryKeyExists()) return StartupMode.Registry;
        return StartupMode.None;
    }

    public static void EnableRegistry()
    {
        string exePath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrEmpty(exePath)) return;

        DisableAll();
        SetRegistryKey(exePath);
        Logger.Info("Start on login enabled (Registry, normal privileges).");
    }

    public static void EnableTaskScheduler()
    {
        string exePath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrEmpty(exePath)) return;

        DisableAll();
        if (CreateScheduledTask(exePath))
            Logger.Info("Start on login enabled (Task Scheduler, elevated).");
        else
            Logger.Error("Failed to create scheduled task for elevated startup.");
    }

    public static void DisableAll()
    {
        DeleteScheduledTask();
        RemoveRegistryKey();
        Logger.Info("Start on login disabled.");
    }

    // Backward compat for settings loading
    public static bool IsStartOnLoginEnabled() => GetCurrentMode() != StartupMode.None;

    private static bool CreateScheduledTask(string exePath)
    {
        try
        {
            string xml = BuildTaskXml(exePath);
            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, xml);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /TN \"{TaskName}\" /XML \"{tempFile}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(5000);

            try { File.Delete(tempFile); } catch { }

            if (proc.ExitCode != 0)
            {
                string err = proc.StandardError.ReadToEnd();
                Logger.Error($"schtasks /Create failed (exit {proc.ExitCode}): {err}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create scheduled task: {ex.Message}");
            return false;
        }
    }

    private static string BuildTaskXml(string exePath)
    {
        string workingDir = Path.GetDirectoryName(exePath) ?? "";
        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Start MacMode at login with elevated privileges</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <Priority>7</Priority>
  </Settings>
  <Actions>
    <Exec>
      <Command>{exePath}</Command>
      <WorkingDirectory>{workingDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
    }

    private static void DeleteScheduledTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{TaskName}\" /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch { }
    }

    private static bool TaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool RegistryKeyExists()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(TaskName) != null;
        }
        catch { return false; }
    }

    private static void SetRegistryKey(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(TaskName, $"\"{exePath}\"");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set registry run key: {ex.Message}");
        }
    }

    private static void RemoveRegistryKey()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(TaskName, false);
        }
        catch { }
    }
}
