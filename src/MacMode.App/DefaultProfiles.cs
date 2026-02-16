using System.IO;
using System.Reflection;

namespace MacMode.App;

/// <summary>
/// Provides access to the default profile JSON files embedded as resources.
/// Used by the profile editor to restore profiles to their shipped state.
/// </summary>
public static class DefaultProfiles
{
    private static readonly Assembly ResourceAssembly = typeof(DefaultProfiles).Assembly;

    private static readonly string[] ShippedProfileNames =
    {
        "default", "chrome", "vscode", "warp", "terminal", "explorer"
    };

    /// <summary>
    /// Returns the set of profile names that ship with the app.
    /// </summary>
    public static IReadOnlyList<string> GetShippedNames() => ShippedProfileNames;

    /// <summary>
    /// Returns true if the given profile name (without extension) is a shipped default.
    /// </summary>
    public static bool IsShippedProfile(string profileName) =>
        ShippedProfileNames.Contains(profileName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the embedded resource for a shipped profile and returns its JSON content.
    /// Returns null if the resource is not found.
    /// </summary>
    public static string? GetDefaultJson(string profileName)
    {
        string resourceSuffix = $".{profileName}.json";
        string? resourceName = ResourceAssembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            return null;

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Restores a shipped profile to its default state by overwriting the file on disk.
    /// </summary>
    public static bool RestoreToDefault(string profilesDir, string profileName)
    {
        string? json = GetDefaultJson(profileName);
        if (json == null)
            return false;

        string filePath = Path.Combine(profilesDir, $"{profileName}.json");
        File.WriteAllText(filePath, json);
        return true;
    }
}
