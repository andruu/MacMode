using MacMode.Core.Engine;
using MacMode.Core.Hook;
using MacMode.Core.Profiles;
using MacMode.Core.ProcessDetection;

// No desktop input is generated. Exercise complete event sequences using the real
// profiles and capture output at the SendInput boundary, including loop prevention.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var profiles = new ProfileManager(Path.Combine(root, "profiles"));
profiles.Load();
int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Test(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

Harness New(string process = "chrome") => new(profiles, process);
const int Alt = NativeMethods.VK_LMENU;
const int Ctrl = NativeMethods.VK_LCONTROL;
const int Shift = NativeMethods.VK_LSHIFT;

foreach (bool injected in new[] { false, true })
{
    string kind = injected ? "Synergy-style injected" : "physical";
    Test($"{kind} Alt+C translates and releases cleanly", () =>
    {
        var h = New();
        Check(!h.Key(Alt, true, injected), "Alt down must pass through");
        Check(h.Key('C', true, injected), "Copy trigger must be consumed");
        Check(h.Down(Ctrl) && h.Down('C') && h.Up('C') && h.Up(Ctrl) && h.Up(Alt), "Expected balanced Ctrl+C with Alt cancelled");
        h.Key('C', false, injected);
        h.Key(Alt, false, injected);
        Check(!h.Engine.ModState.LeftAltDown, "Alt must be released");
        Check(!h.Key('V', true, injected), "Plain V must not remain in a chord");
        Check(h.Events.Count == 5, "One copy chord must generate exactly five events");
    });
    Test($"{kind} Alt+Shift+Z keeps Shift out of Ctrl+Y", () =>
    {
        var h = New("unmatched-app");
        h.Key(Alt, true, injected);
        h.Key(Shift, true, injected);
        Check(h.Key('Z', true, injected), "Redo should map");
        Check(h.Up(Shift) && h.Down('Y') && h.Down(Shift), "Redo must release and restore held Shift");
    });
    Test($"{kind} per-app Warp copy remains Ctrl+Shift+C", () =>
    {
        var h = New("warp");
        h.Key(Alt, true, injected);
        h.Key('C', true, injected);
        Check(h.Down(Ctrl) && h.Down(Shift) && h.Down('C'), "Warp profile was not applied");
    });
    Test($"{kind} Alt+Q dismisses Raycast instead of closing its launcher window", () =>
    {
        // The default close-window action posts WM_CLOSE to the foreground window.
        // Raycast destroys its launcher on WM_CLOSE (rather than hiding it), after
        // which its hotkey still fires but there is no window left to show.
        var h = New("raycast");
        h.Key(Alt, true, injected);
        Check(h.Key('Q', true, injected), "Raycast Alt+Q must be consumed");
        Check(h.Down(NativeMethods.VK_ESCAPE) && h.Up(NativeMethods.VK_ESCAPE) && h.Up(Alt),
            "Raycast Alt+Q must send Escape with Alt cancelled");
        Check(h.Events.Count == 3, "Raycast Alt+Q must generate exactly Alt-up, Escape down, Escape up");
        var fallback = profiles.GetMapping("chrome", ModifierFlags.None, 'Q');
        Check(fallback != null && fallback.IsSpecialAction && fallback.SpecialActionName == "close-window",
            "Other apps must keep the close-window action");
    });
}

Test("Own generated input cannot change modifier state or recurse", () =>
{
    var h = New();
    Check(!h.Key(Alt, true, true, InputOrigin.MacModeTag), "Own Alt must pass through");
    Check(!h.Engine.ModState.LeftAltDown, "Own Alt affected source state");
    h.Key(Alt, true);
    h.Key('C', true);
    Check(h.Events.All(i => i.u.ki.dwExtraInfo == InputOrigin.MacModeTag), "Missing output marker");
    Check(h.Engine.ModState.LeftAltDown, "Own synthetic Alt-up cleared real held Alt");
});
Test("External injection with another extra-info tag is accepted", () =>
{
    var h = New();
    h.Key(Alt, true, true, new IntPtr(123));
    Check(h.Key('C', true, true, new IntPtr(123)), "Foreign input was mistaken for own output");
    Check(h.Down('C'), "No copy was emitted");
});
Test("Both generated mouse edges carry the same recursion marker", () =>
{
    Check(KeySender.MakeMouseDown().u.mi.dwExtraInfo == InputOrigin.MacModeTag, "Mouse down is unmarked");
    Check(KeySender.MakeMouseUp().u.mi.dwExtraInfo == InputOrigin.MacModeTag, "Mouse up is unmarked");
});
foreach (string process in new[] { "synergy-core", "Synergy", "synergys", "synergy-server" })
{
    Test($"{process} receives untouched physical and injected shortcuts", () =>
    {
        var h = New(process);
        foreach (bool injected in new[] { false, true })
        {
            Check(!h.Key(Alt, true, injected), "Remote Alt was consumed");
            Check(!h.Key('C', true, injected), "Remote C was consumed");
            h.Key('C', false, injected);
            h.Key(Alt, false, injected);
        }
        Check(h.Events.Count == 0, "MacMode injected into Synergy capture");
        Check(!h.Engine.CanRemapInput && h.Engine.Enabled, "Pause must be temporary");
    });
}
Test("A handoff clears the previous chord and resumes local remapping", () =>
{
    var h = New();
    h.Key(Alt, true);
    h.Key('C', true);
    h.Process = "synergy-core";
    Check(!h.Engine.CanRemapInput && !h.Engine.ModState.LeftAltDown, "Mouse context did not clear held Alt");
    Check(!h.Key('V', true), "Old chord swallowed remote key");
    h.Process = "chrome";
    Check(!h.Key('X', true), "Old chord swallowed local key after return");
    h.Key(Alt, true);
    Check(h.Key('V', true) && h.Down('V'), "Remapping did not resume");
});
Test("Manual disable survives a Synergy round trip", () =>
{
    var h = New();
    h.Engine.Enabled = false;
    h.Process = "synergy-core";
    h.Key('C', true);
    h.Process = "chrome";
    h.Key(Alt, true);
    Check(!h.Key('C', true) && h.Events.Count == 0 && !h.Engine.Enabled, "Manual disable was lost");
});
Test("Compatibility pause can be disabled in settings", () =>
{
    var h = New("synergy-core");
    h.Engine.SuspendForSynergy = false;
    h.Key(Alt, true);
    Check(h.Key('C', true) && h.Down('C'), "Explicit opt-out was ignored");
});
foreach (int digit in new[] { (int)'1', (int)'2' })
{
    Test($"Ctrl+Alt+{(char)digit} passes in either modifier order", () =>
    {
        foreach (bool ctrlFirst in new[] { false, true })
        {
            var h = New();
            h.Key(ctrlFirst ? Ctrl : Alt, true);
            h.Key(ctrlFirst ? Alt : Ctrl, true);
            Check(!h.Key(digit, true) && !h.Key(digit, false), "KVM shortcut consumed");
            Check(h.Events.Count == 0, "KVM shortcut translated");
        }
    });
    Test($"Ctrl+Alt+{(char)digit} works after copy while Alt stays down", () =>
    {
        var h = New();
        h.Key(Alt, true);
        h.Key('C', true);
        h.Key('C', false);
        h.Events.Clear();
        h.Key(Ctrl, true);
        Check(!h.Key(digit, true) && !h.Key(digit, false), "Sequential KVM shortcut swallowed");
        Check(h.Events.Count == 1 && h.Down(Alt), "Must restore Alt exactly once");
    });
}
Test("Native Alt+Tab and Right Alt still pass through", () =>
{
    var h = New();
    h.Key(Alt, true);
    Check(!h.Key(NativeMethods.VK_TAB, true), "Alt+Tab remapped");
    h.Key(Alt, false);
    h.Key(NativeMethods.VK_RMENU, true);
    Check(!h.Key('C', true) && h.Events.Count == 0, "Right Alt remapped");
});
Test("Panic shortcut is honored for incoming keys", () =>
{
    var h = New();
    bool panic = false;
    h.Engine.PanicKeyPressed += () => panic = true;
    h.Key(Ctrl, true, true);
    h.Key(Alt, true, true);
    h.Key(NativeMethods.VK_BACK, true, true);
    Check(panic, "Injected panic shortcut ignored");
});
Test("Profile recording remains unmodified", () =>
{
    var h = New();
    h.Engine.IsRecording = true;
    h.Key(Alt, true, true);
    Check(!h.Key('C', true, true) && h.Events.Count == 0 && !h.Engine.CanRemapInput, "Recorder remapped input");
});
KeyboardHookEventArgs TapKey(int vk, bool down, bool injected = true, IntPtr extra = default) =>
    new(vk, 0, injected ? NativeMethods.LLKHF_INJECTED : 0, down, extra);
foreach (int win in new[] { NativeMethods.VK_LWIN, NativeMethods.VK_RWIN })
{
    Test($"Injected Windows tap {win} activates once, on release", () =>
    {
        var tap = new InjectedWindowsTap();
        Check(!tap.Observe(TapKey(win, true), true), "Launched before release");
        Check(!tap.Observe(TapKey(win, true), true), "Auto-repeat launched");
        Check(tap.Observe(TapKey(win, false), true), "Standalone tap did not activate");
        Check(!tap.Observe(TapKey(win, false), true), "Duplicate release activated again");
        tap.Observe(TapKey(win, true), true);
        Check(tap.Observe(TapKey(win, false), true), "A later single tap did not activate");
    });
    Test($"Physical Windows tap {win} is left to Raycast", () =>
    {
        var tap = new InjectedWindowsTap();
        Check(!tap.Observe(TapKey(win, true, false), true), "Physical down activated");
        Check(!tap.Observe(TapKey(win, false, false), true), "Physical up activated");
    });
    Test($"Windows+E {win} never activates the launcher", () =>
    {
        foreach (bool winReleasedFirst in new[] { true, false })
        {
            var tap = new InjectedWindowsTap();
            tap.Observe(TapKey(win, true), true);
            Check(!tap.Observe(TapKey('E', true), true), "Chord down activated");
            Check(!tap.Observe(TapKey(winReleasedFirst ? win : 'E', false), true), "Chord release activated");
            Check(!tap.Observe(TapKey(winReleasedFirst ? 'E' : win, false), true), "Last release activated");
            tap.Observe(TapKey(win, true), true);
            Check(tap.Observe(TapKey(win, false), true), "Chord left stale state");
        }
    });
    Test($"Windows modifier {win} is not swallowed after an Alt shortcut", () =>
    {
        foreach (bool injected in new[] { false, true })
        {
            var h = New();
            h.Key(Alt, true, injected);
            h.Key('C', true, injected);
            h.Key('C', false, injected);
            int sent = h.Events.Count;
            Check(!h.Key(win, true, injected) && !h.Key(win, false, injected), "Windows key swallowed in chord state");
            Check(h.Events.Count == sent, "Windows key unexpectedly translated");
        }
    });
}
Test("Keys held before Windows prevent launcher activation", () =>
{
    foreach (int held in new[] { Alt, Ctrl, Shift, (int)'A', NativeMethods.VK_RWIN })
    {
        var tap = new InjectedWindowsTap();
        tap.Observe(TapKey(held, true), true);
        tap.Observe(TapKey(NativeMethods.VK_LWIN, true), true);
        Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false), true), "Held-key chord activated");
        Check(!tap.Observe(TapKey(held, false), true), "Held-key release activated");
    }
});
Test("Mouse chord and hook reset cancel a pending tap", () =>
{
    var tap = new InjectedWindowsTap();
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true), true);
    tap.CancelTap();
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false), true), "Mouse chord activated");
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true), true);
    tap.Reset();
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false), true), "Reset retained pending tap");
});
Test("Disabled, remote, or recording contexts cannot activate", () =>
{
    var tap = new InjectedWindowsTap();
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true), true);
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false), false), "Disabled context activated");
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true), false);
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false), true), "Release without eligible press activated");
});
Test("MacMode output and unmatched physical releases cannot activate", () =>
{
    var tap = new InjectedWindowsTap();
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true, true, InputOrigin.MacModeTag), true);
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false, true, InputOrigin.MacModeTag), true), "Own output activated");
    tap.Observe(TapKey(NativeMethods.VK_LWIN, true), true);
    Check(!tap.Observe(TapKey(NativeMethods.VK_LWIN, false, false), true), "Mismatched-origin release activated");
});
Test("Incoming input resets both idle timers once per batch, then becomes idle", () =>
{
    var requests = new List<uint>();
    var activity = new RemoteInputActivity(flags => { requests.Add(flags); return 0x80000000; });
    Check(activity.Flush(true) == null, "Idle startup issued a power request");
    for (int i = 0; i < 1000; i++) activity.Observe(true, false);
    Check(requests.Count == 0, "Hook observation called the power API");
    Check(activity.Flush(true) == true && requests.SequenceEqual(new uint[] { 3 }),
        "Burst must issue one display/system reset without continuous or away-mode flags");
    Check(activity.Flush(true) == null && requests.Count == 1, "Idle timer tick kept the computer awake");
    activity.Observe(true, false);
    Check(activity.Flush(true) == true && activity.SuccessfulResetCount == 2, "Returning input did not refresh again");
});
Test("Physical input and MacMode output do not produce extra power requests", () =>
{
    int calls = 0;
    var activity = new RemoteInputActivity(_ => { calls++; return 1; });
    activity.Observe(false, false);
    activity.Observe(true, true);
    Check(activity.Flush(true) == null && calls == 0, "Own output or physical input generated a request");
});
Test("Disabling idle refresh clears pending activity without creating a power request", () =>
{
    int calls = 0;
    var activity = new RemoteInputActivity(_ => { calls++; return 1; });
    activity.Observe(true, false);
    Check(activity.Flush(false) == null && calls == 0, "Disabled feature created a request");
    Check(activity.Flush(true) == null, "Reenable replayed stale input");
    activity.Observe(true, false);
    Check(activity.Flush(true) == true && calls == 1, "Fresh input after reenable failed");
});
Test("Power API failure is reported and does not create an idle retry loop", () =>
{
    bool fail = true;
    var activity = new RemoteInputActivity(_ => fail ? 0u : 1u);
    activity.Observe(true, false);
    Check(activity.Flush(true) == false && activity.SuccessfulResetCount == 0, "API failure reported as success");
    fail = false;
    Check(activity.Flush(true) == null, "Failure replayed a stale power request without user input");
    activity.Observe(true, false);
    Check(activity.Flush(true) == true && activity.SuccessfulResetCount == 1, "Fresh input did not recover");
});
Test("Input arriving during an idle reset is preserved for the next timer tick", () =>
{
    RemoteInputActivity? activity = null;
    int calls = 0;
    activity = new RemoteInputActivity(_ => { if (++calls == 1) activity!.Observe(true, false); return 1; });
    activity.Observe(true, false);
    Check(activity.Flush(true) == true, "First reset failed");
    Check(activity.Flush(true) == true && calls == 2, "Concurrent activity was dropped");
    Check(activity.Flush(true) == null, "No-input tick issued another reset");
});
Test("Slow power calls cannot block input observation or input-thread shutdown", () =>
{
    using var entered = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    int callerThread = Environment.CurrentManagedThreadId;
    int powerThread = 0;
    uint requestedFlags = 0;
    using var power = new MacMode.App.RemoteInputPower(() => true, flags =>
    {
        requestedFlags = flags;
        powerThread = Environment.CurrentManagedThreadId;
        entered.Set();
        release.Wait(TimeSpan.FromSeconds(5));
        return 1;
    }, TimeSpan.FromMilliseconds(20));
    try
    {
        power.Observe(true, false);
        Check(entered.Wait(TimeSpan.FromSeconds(2)), "Background power worker did not start");
        Check(powerThread != callerThread && requestedFlags == 3, "Power call ran on input thread or held continuous power");
        // Keep the simulated native API blocked while more pointer activity and
        // shutdown arrive. Neither operation may wait for the power worker.
        var input = Task.Run(() => { for (int i = 0; i < 1000; i++) power.Observe(true, false); });
        Check(input.Wait(TimeSpan.FromSeconds(1)), "Slow power API stalled pointer observation");
        Check(Task.Run(power.Dispose).Wait(TimeSpan.FromSeconds(1)), "Shutdown waited on the blocked power API");
    }
    finally
    {
        power.Dispose();
        release.Set();
        Check(power.WaitForStopped(TimeSpan.FromSeconds(2)), "Power worker did not cleanly exit");
    }
});
Console.WriteLine($"{passed} regression scenarios passed. No desktop input was generated.");

sealed class Harness
{
    public string Process;
    public MappingEngine Engine { get; }
    public List<NativeMethods.INPUT> Events { get; } = new();
    public Harness(ProfileManager profiles, string process)
    {
        Process = process;
        MappingEngine? engine = null;
        engine = new MappingEngine(profiles, new ForegroundProcessDetector(), () => Process, batch =>
        {
            Events.AddRange(batch);
            foreach (var input in batch)
            {
                bool down = (input.u.ki.dwFlags & NativeMethods.KEYEVENTF_KEYUP) == 0;
                if (engine!.ProcessKeyEvent(new KeyboardHookEventArgs(input.u.ki.wVk, 0,
                    NativeMethods.LLKHF_INJECTED, down, input.u.ki.dwExtraInfo)))
                    throw new Exception("Generated output was recursively consumed");
            }
        });
        Engine = engine;
    }
    public bool Key(int vk, bool down, bool injected = false, IntPtr extra = default) =>
        Engine.ProcessKeyEvent(new KeyboardHookEventArgs(vk, 0,
            injected ? NativeMethods.LLKHF_INJECTED : 0, down, extra));
    public bool Down(int vk) => Events.Any(i => i.u.ki.wVk == vk && (i.u.ki.dwFlags & NativeMethods.KEYEVENTF_KEYUP) == 0);
    public bool Up(int vk) => Events.Any(i => i.u.ki.wVk == vk && (i.u.ki.dwFlags & NativeMethods.KEYEVENTF_KEYUP) != 0);
}
