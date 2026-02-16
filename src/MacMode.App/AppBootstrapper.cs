using System.IO;
using MacMode.Core.Engine;
using MacMode.Core.Hook;
using MacMode.Core.Logging;
using MacMode.Core.ProcessDetection;
using MacMode.Core.Profiles;
using MacMode.Core.Settings;

namespace MacMode.App;

/// <summary>
/// Composition root: resolves paths, initializes logging, and creates all
/// core services. Separates dependency wiring from UI concerns (TrayIcon).
/// </summary>
public sealed class AppBootstrapper : IDisposable
{
    private const int MaxDirTraversalDepth = 8;
    private bool _disposed;

    public KeyboardHook Hook { get; }
    public MouseHook MouseHook { get; }
    public MappingEngine Engine { get; }
    public ProfileManager Profiles { get; }
    public SettingsManager Settings { get; }

    public AppBootstrapper()
    {
        // Resolve paths
        string baseDir = AppContext.BaseDirectory;
        string profilesDir = Path.Combine(baseDir, "profiles");
        string settingsPath = Path.Combine(baseDir, "settings.json");

        // If running from dev (bin/Debug/...), look for profiles at solution root
        if (!Directory.Exists(profilesDir))
        {
            string devProfilesDir = FindProfilesDir(baseDir);
            if (Directory.Exists(devProfilesDir))
                profilesDir = devProfilesDir;
        }

        // Initialize logging
        Logger.Initialize(enableDebug: true);

        // Initialize settings
        Settings = new SettingsManager(settingsPath);
        Settings.Load();
        Logger.SetDebugEnabled(Settings.Current.DebugLogging);

        // Initialize profiles
        Profiles = new ProfileManager(profilesDir);
        Profiles.Load();
        Profiles.StartWatching();

        // Initialize engine
        var processDetector = new ForegroundProcessDetector();
        Engine = new MappingEngine(Profiles, processDetector);
        Engine.Enabled = Settings.Current.MacModeEnabled;

        // Initialize hooks
        Hook = new KeyboardHook();
        MouseHook = new MouseHook(Engine.ModState, () => Engine.Enabled);
    }

    /// <summary>
    /// Walks up from the bin directory to find the profiles folder at solution root.
    /// </summary>
    private static string FindProfilesDir(string startDir)
    {
        string? dir = startDir;
        for (int i = 0; i < MaxDirTraversalDepth && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "profiles");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(startDir, "profiles");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Each resource is disposed independently so a failure in one
        // doesn't prevent the others from cleaning up (critical for hooks).
        try { MouseHook.Dispose(); } catch { }
        try { Hook.Dispose(); } catch { }
        try { Profiles.Dispose(); } catch { }
        try { Logger.Shutdown(); } catch { }
    }
}
