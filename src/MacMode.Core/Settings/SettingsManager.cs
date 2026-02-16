using System.Text.Json;
using MacMode.Core.Logging;

namespace MacMode.Core.Settings;

public sealed class SettingsManager
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Current { get; private set; } = new();

    public SettingsManager(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                Logger.Info($"Settings loaded from {_settingsPath}");
            }
            else
            {
                Current = new AppSettings();
                Save(); // Create default file
                Logger.Info($"Created default settings at {_settingsPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load settings: {ex.Message}");
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save settings: {ex.Message}");
        }
    }
}
