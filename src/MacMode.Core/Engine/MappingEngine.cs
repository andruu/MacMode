using MacMode.Core.Hook;
using MacMode.Core.Logging;
using MacMode.Core.ProcessDetection;
using MacMode.Core.Profiles;

namespace MacMode.Core.Engine;

/// <summary>
/// Core remapping engine. New architecture: Left Alt is NOT suppressed on keydown.
/// It passes through naturally so Alt+Tab, Alt+F4, Alt+Space all work natively.
/// When a recognized chord key arrives, we cancel the Alt and inject the mapped shortcut.
///
/// States:
///   Idle        - nothing tracked
///   AltPending  - Left Alt is physically held and was passed through
///   ChordActive - a mapping was fired; physical Alt is held but we sent synthetic Alt-up
/// </summary>
public sealed class MappingEngine
{
    private enum State
    {
        Idle,
        AltPending,
        ChordActive
    }

    private readonly ProfileManager _profiles;
    private readonly ForegroundProcessDetector _processDetector;
    private readonly ModifierState _modState = new();

    public ModifierState ModState => _modState;

    private State _state = State.Idle;
    private bool _enabled = true;

    /// <summary>
    /// When true, the engine passes all keys through without processing.
    /// Used by the profile editor's hotkey recorder to capture raw key input.
    /// </summary>
    public bool IsRecording { get; set; }

    /// <summary>Raised when panic key (Ctrl+Alt+Backspace) is pressed.</summary>
    public event Action? PanicKeyPressed;

    /// <summary>Raised when a mapping is activated (for UI feedback).</summary>
    public event Action<string>? MappingActivated;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            Logger.Info($"Mac Mode {(_enabled ? "enabled" : "disabled")}");

            if (!_enabled)
            {
                _state = State.Idle;
                _modState.Reset();
            }
        }
    }

    public MappingEngine(ProfileManager profiles, ForegroundProcessDetector processDetector)
    {
        _profiles = profiles;
        _processDetector = processDetector;
    }

    /// <summary>
    /// Main entry point called by the keyboard hook for every key event.
    /// Returns true if the event should be suppressed (swallowed).
    /// </summary>
    public bool ProcessKeyEvent(KeyboardHookEventArgs e)
    {
        // Always pass through injected events (our own SendInput) to avoid recursion
        if (e.IsInjected)
            return false;

        // Track physical modifier state regardless of Mac Mode
        UpdateModifierState(e);

        // When the hotkey recorder is active, pass everything through
        if (IsRecording)
            return false;

        // Check panic key: Ctrl+Alt+Backspace
        if (e.IsKeyDown && e.VirtualKeyCode == NativeMethods.VK_BACK &&
            _modState.CtrlDown && _modState.LeftAltDown)
        {
            Logger.Info("Panic key detected: Ctrl+Alt+Backspace");
            PanicKeyPressed?.Invoke();
            return false;
        }

        if (!_enabled)
            return false;

        // Right Alt (AltGr) is always passed through
        if (e.IsRightAlt)
            return false;

        return _state switch
        {
            State.Idle => HandleIdle(e),
            State.AltPending => HandleAltPending(e),
            State.ChordActive => HandleChordActive(e),
            _ => false
        };
    }

    /// <summary>
    /// Idle: nothing is tracked. Watch for Left Alt keydown.
    /// </summary>
    private bool HandleIdle(KeyboardHookEventArgs e)
    {
        if (e.IsLeftAlt && e.IsKeyDown)
        {
            _state = State.AltPending;
            Logger.Debug("State: Idle -> AltPending (Left Alt down, passed through)");
            // DO NOT suppress. Let the physical Alt pass through.
            return false;
        }

        return false;
    }

    /// <summary>
    /// AltPending: Left Alt is physically held and was passed through to the system.
    /// Waiting to see if the next key forms a recognized chord.
    /// Alt+Tab, Alt+F4, Alt+Space all work natively because we never touched Alt.
    /// </summary>
    private bool HandleAltPending(KeyboardHookEventArgs e)
    {
        // Left Alt released without a chord -> natural Alt tap, go to Idle
        if (e.IsLeftAlt && e.IsKeyUp)
        {
            Logger.Debug("State: AltPending -> Idle (Left Alt released, natural behavior)");
            _state = State.Idle;
            return false; // Let the physical Alt up through
        }

        // Left Alt autorepeat
        if (e.IsLeftAlt && e.IsKeyDown)
            return false; // Let autorepeat through

        // Global passthrough keys: let them through naturally (Alt is already held)
        if (e.IsKeyDown && IsGlobalPassthrough(e.VirtualKeyCode))
        {
            Logger.Debug($"Global passthrough VK 0x{e.VirtualKeyCode:X2}, going Idle");
            _state = State.Idle;
            return false;
        }

        // Non-modifier keydown: check for a chord mapping (regular or special action)
        if (e.IsKeyDown && !IsModifierKey(e.VirtualKeyCode))
        {
            string processName = _processDetector.GetForegroundProcessName();
            var triggerMods = _modState.ActiveModifiers;
            var mapping = _profiles.GetMapping(processName, triggerMods, e.VirtualKeyCode);

            if (mapping != null)
            {
                Logger.Debug($"Chord matched: process={processName}, mapping={mapping}");

                if (mapping.IsSpecialAction)
                {
                    // Special actions (e.g., close-window, cycle-windows) are dispatched
                    // via the registry. Cancel Alt first to prevent menu activation.
                    CancelAltAndTransition();
                    SpecialActionRegistry.TryExecute(mapping.SpecialActionName!);
                }
                else if (mapping.ActionModifierVks.Contains(NativeMethods.VK_LMENU))
                {
                    // The action wants Alt held (e.g., Alt+F4).
                    // Physical Alt is already held and passed through, so just
                    // send the key (+ any non-Alt modifiers). Don't touch Alt.
                    BuildAndSendMapping(mapping, keepAlt: true);
                    // Stay in AltPending: Alt is still physically held and the
                    // system still sees it. Normal behavior resumes.
                }
                else
                {
                    BuildAndSendMapping(mapping, cancelAlt: true);
                    _state = State.ChordActive;
                }
                MappingActivated?.Invoke($"{mapping}");
                return true; // Suppress the physical chord key
            }
            else
            {
                // No mapping: let everything through naturally (app gets Alt+key)
                Logger.Debug($"No mapping for VK 0x{e.VirtualKeyCode:X2} in process '{processName}', passthrough");
                _state = State.Idle;
                return false;
            }
        }

        // Modifier key changes (Shift, Ctrl down/up): pass through, stay in AltPending
        return false;
    }

    /// <summary>
    /// ChordActive: a mapping was fired. Physical Alt is still held by the user,
    /// but we already sent a synthetic Alt-up to cancel it. We suppress the
    /// physical Alt-up when it finally comes, and handle additional chord keys.
    /// </summary>
    private bool HandleChordActive(KeyboardHookEventArgs e)
    {
        // Physical Left Alt released: suppress it (we already sent Alt-up), go Idle
        if (e.IsLeftAlt && e.IsKeyUp)
        {
            Logger.Debug("State: ChordActive -> Idle (Left Alt released, suppressed)");
            _state = State.Idle;
            return true; // Suppress: system already thinks Alt is up
        }

        // Left Alt autorepeat: suppress
        if (e.IsLeftAlt && e.IsKeyDown)
            return true;

        // Modifier keys (Shift, Ctrl): let physical changes through
        if (IsModifierKey(e.VirtualKeyCode))
            return false;

        // Global passthrough keys (Tab, F4, Space): re-engage Alt so native
        // behavior works (e.g., user does Alt+` then switches to Alt+Tab)
        if (e.IsKeyDown && IsGlobalPassthrough(e.VirtualKeyCode))
        {
            Logger.Debug($"ChordActive -> Idle: re-engaging Alt for global passthrough VK 0x{e.VirtualKeyCode:X2}");
            KeySender.SendBatch(new[] { KeySender.MakeKeyDown(NativeMethods.VK_LMENU) });
            _state = State.Idle;
            return false; // Let the key through; system now sees Alt+key
        }

        // Another keydown: check for a new chord mapping (regular or special action)
        if (e.IsKeyDown)
        {
            string processName = _processDetector.GetForegroundProcessName();
            var triggerMods = _modState.ActiveModifiers;
            var mapping = _profiles.GetMapping(processName, triggerMods, e.VirtualKeyCode);

            if (mapping != null)
            {
                Logger.Debug($"Sequential chord: process={processName}, mapping={mapping}");

                if (mapping.IsSpecialAction)
                    SpecialActionRegistry.TryExecute(mapping.SpecialActionName!);
                else
                    BuildAndSendMapping(mapping);

                MappingActivated?.Invoke($"{mapping}");
                return true;
            }

            // Unmapped key in chord context: suppress
            return true;
        }

        // Keyup of previously suppressed chord keys: suppress
        if (e.IsKeyUp)
            return true;

        return false;
    }

    /// <summary>
    /// Unified mapping execution. Builds a balanced INPUT[] sequence and sends it atomically.
    ///
    /// Three modes controlled by parameters:
    ///   keepAlt=true:  Action needs Alt held (e.g., Alt+F4). Skip synthetic Alt in modifiers.
    ///   cancelAlt=true: Alt was passed through and must be cancelled (AltPending → ChordActive).
    ///   Both false:     Alt already cancelled (ChordActive sequential chord).
    ///
    /// Handles physical-Shift deconfliction in all modes: if Shift is physically held and the
    /// action needs Shift, we skip the synthetic Shift press/release. If Shift is physically held
    /// but the action does NOT need Shift, we temporarily release it.
    /// </summary>
    private void BuildAndSendMapping(CompiledMapping mapping, bool cancelAlt = false, bool keepAlt = false)
    {
        var inputs = new List<NativeMethods.INPUT>();
        bool physicalShiftHeld = _modState.ShiftDown;
        bool physicalCtrlHeld = _modState.CtrlDown;
        bool actionNeedsShift = mapping.ActionModifierVks.Contains(NativeMethods.VK_LSHIFT);
        bool actionNeedsCtrl = mapping.ActionModifierVks.Contains(NativeMethods.VK_LCONTROL);
        bool shouldReleaseShift = physicalShiftHeld && !actionNeedsShift && !keepAlt;
        bool shouldReleaseCtrl = physicalCtrlHeld && !actionNeedsCtrl && !keepAlt;

        // Step 1: Press action modifiers (Ctrl, Shift, Win, etc.)
        // In cancelAlt mode, pressing Ctrl BEFORE releasing Alt prevents menu-bar activation.
        foreach (int modVk in mapping.ActionModifierVks)
        {
            if (modVk == NativeMethods.VK_LMENU && keepAlt)
                continue; // Alt is already physically held
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue; // Shift is already physically held
            if (modVk == NativeMethods.VK_LCONTROL && physicalCtrlHeld)
                continue; // Ctrl is already physically held
            inputs.Add(KeySender.MakeKeyDown(modVk));
        }

        // Step 2: Cancel Alt if transitioning from AltPending
        if (cancelAlt)
            inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LMENU));

        // Step 3: Temporarily release modifiers that are held but not needed by the action
        if (shouldReleaseCtrl)
            inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LCONTROL));
        if (shouldReleaseShift)
            inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LSHIFT));

        // Step 4: Press and release the action key
        inputs.Add(KeySender.MakeKeyDown(mapping.ActionVk));
        inputs.Add(KeySender.MakeKeyUp(mapping.ActionVk));

        // Step 5: Restore modifiers if we released them
        if (shouldReleaseShift)
            inputs.Add(KeySender.MakeKeyDown(NativeMethods.VK_LSHIFT));
        if (shouldReleaseCtrl)
            inputs.Add(KeySender.MakeKeyDown(NativeMethods.VK_LCONTROL));

        // Step 6: Release action modifiers (reverse order)
        foreach (int modVk in mapping.ActionModifierVks.Reverse())
        {
            if (modVk == NativeMethods.VK_LMENU && keepAlt)
                continue;
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue;
            if (modVk == NativeMethods.VK_LCONTROL && physicalCtrlHeld)
                continue;
            inputs.Add(KeySender.MakeKeyUp(modVk));
        }

        KeySender.SendBatch(inputs.ToArray());
    }

    /// <summary>
    /// Cancels the physical Alt that was passed through (sends Ctrl down, Alt up, Ctrl up
    /// to suppress menu activation) and transitions to ChordActive state.
    /// Used by special actions like window cycling that aren't regular key mappings.
    /// </summary>
    private void CancelAltAndTransition()
    {
        var inputs = new[]
        {
            KeySender.MakeKeyDown(NativeMethods.VK_LCONTROL),
            KeySender.MakeKeyUp(NativeMethods.VK_LMENU),
            KeySender.MakeKeyUp(NativeMethods.VK_LCONTROL),
        };
        KeySender.SendBatch(inputs);
        _state = State.ChordActive;
    }

    private void UpdateModifierState(KeyboardHookEventArgs e)
    {
        switch (e.VirtualKeyCode)
        {
            case NativeMethods.VK_LMENU:
                _modState.LeftAltDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_MENU when !e.IsExtended:
                _modState.LeftAltDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_LSHIFT:
                _modState.LeftShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_RSHIFT:
                _modState.RightShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_SHIFT:
                if (e.IsExtended)
                    _modState.RightShiftDown = e.IsKeyDown;
                else
                    _modState.LeftShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_LCONTROL:
                _modState.LeftCtrlDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_RCONTROL:
                _modState.RightCtrlDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_CONTROL:
                if (e.IsExtended)
                    _modState.RightCtrlDown = e.IsKeyDown;
                else
                    _modState.LeftCtrlDown = e.IsKeyDown;
                break;
        }
    }

    /// <summary>
    /// Keys that should always pass through with Alt for native behavior.
    /// Tab and F4 always pass through (Alt+Tab, Alt+F4).
    /// Space passes through only without additional modifiers (Alt+Space = window menu),
    /// allowing Alt+Ctrl+Space to be mapped (e.g., emoji picker).
    /// </summary>
    private bool IsGlobalPassthrough(int vk)
    {
        if (vk == NativeMethods.VK_TAB || vk == NativeMethods.VK_F4)
            return true;

        if (vk == NativeMethods.VK_SPACE && !_modState.ShiftDown && !_modState.CtrlDown)
            return true;

        return false;
    }

    private static bool IsModifierKey(int vk)
    {
        return vk is NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or NativeMethods.VK_SHIFT
            or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or NativeMethods.VK_CONTROL
            or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or NativeMethods.VK_MENU;
    }
}
