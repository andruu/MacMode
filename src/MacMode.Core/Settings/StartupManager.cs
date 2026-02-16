using MacMode.Core.Logging;
using Microsoft.Win32;

namespace MacMode.Core.Settings;

/// <summary>
/// Manages the "Start on Login" feature via the Registry Run key.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MacMode";

    public static bool IsStartOnLoginEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to read startup registry: {ex.Message}");
            return false;
        }
    }

    public static void SetStartOnLogin(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enabled)
            {
                string exePath = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    Logger.Info("Start on login enabled.");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                Logger.Info("Start on login disabled.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set startup registry: {ex.Message}");
        }
    }
}
