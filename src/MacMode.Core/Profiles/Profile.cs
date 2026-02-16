using System.Text.Json.Serialization;

namespace MacMode.Core.Profiles;

/// <summary>
/// Data model for a profile JSON file.
/// </summary>
public sealed class ProfileDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("processNames")]
    public List<string> ProcessNames { get; set; } = new();

    [JsonPropertyName("mappings")]
    public List<KeyMappingDto> Mappings { get; set; } = new();
}
