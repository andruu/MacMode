using System.Text.Json.Serialization;

namespace MacMode.Core.Settings;

public sealed class AppSettings
{
    [JsonPropertyName("macModeEnabled")]
    public bool MacModeEnabled { get; set; } = true;

    [JsonPropertyName("startOnLogin")]
    public bool StartOnLogin { get; set; } = false;

    [JsonPropertyName("debugLogging")]
    public bool DebugLogging { get; set; } = false;
}
