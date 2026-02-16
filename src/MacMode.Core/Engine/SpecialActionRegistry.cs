using MacMode.Core.Logging;

namespace MacMode.Core.Engine;

/// <summary>
/// Registry of named special actions that can be invoked from profile mappings
/// using the "special:" prefix (e.g., "special:close-window").
///
/// New special actions can be added here without modifying the mapping engine.
/// </summary>
public static class SpecialActionRegistry
{
    private static readonly Dictionary<string, Action> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["close-window"] = WindowCycler.CloseActiveWindow,
        ["cycle-windows"] = WindowCycler.CycleNextWindow,
    };

    /// <summary>
    /// Attempts to execute the named special action.
    /// Returns true if the action was found and executed, false otherwise.
    /// </summary>
    public static bool TryExecute(string actionName)
    {
        if (Actions.TryGetValue(actionName, out var action))
        {
            Logger.Debug($"SpecialAction: executing '{actionName}'");
            action();
            return true;
        }

        Logger.Error($"SpecialAction: unknown action '{actionName}'");
        return false;
    }
}
