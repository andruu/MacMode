namespace MacMode.Core.Engine;

/// <summary>
/// Coalesces incoming software input into one-shot display/system idle resets.
/// Does not retain key contents, generate input, or hold a continuous power request.
/// </summary>
public sealed class RemoteInputActivity(Func<uint, uint> resetExecutionState)
{
    private int _pending;
    public long SuccessfulResetCount { get; private set; }

    public void Observe(bool injected, bool ownInput)
    {
        if (injected && !ownInput)
            Interlocked.Exchange(ref _pending, 1);
    }

    /// <returns>Null for no work, false for an API failure, true for a reset.</returns>
    public bool? Flush(bool enabled)
    {
        bool pending = Interlocked.Exchange(ref _pending, 0) != 0;
        if (!enabled || !pending) return null;

        // ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED, without ES_CONTINUOUS.
        if (resetExecutionState(0x00000003) == 0) return false;
        SuccessfulResetCount++;
        return true;
    }
}
