using System.Runtime.InteropServices;
using System.Text.Json;
using MacMode.Core.Hook;
using MacMode.Core.ProcessDetection;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Passive, time-limited diagnostics. No input is generated or suppressed.
        // Count origins and modifier transitions only; never record typed text.
        var counts = new Dictionary<string, int>();
        var transitions = new List<object>();
        var started = System.Diagnostics.Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacMode", "checks", "modifier-trace.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        int seconds = args.Length > 0 && int.TryParse(args[0], out var requested) ? Math.Clamp(requested, 10, 180) : 120;
        var window = new Form { Text = "Windows modifier check", Width = 510, Height = 210, StartPosition = FormStartPosition.CenterScreen };
        var label = new Label { Dock = DockStyle.Fill, Padding = new Padding(18), Text = "Passive trace running. Nothing to press here.\n\nSwitch to the app you want to check and use your MacMode shortcuts (Alt = Cmd). Only modifier keys and event origins are recorded, never typed text.", Font = new System.Drawing.Font("Segoe UI", 12) };
        window.Controls.Add(label);
        string latest = "Waiting for input";
        var detector = new ForegroundProcessDetector();
        void Count(string name) => counts[name] = counts.GetValueOrDefault(name) + 1;
        NativeMethods.LowLevelKeyboardProc keyboard = (n, w, l) => {
            if (n >= 0) {
                var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(l);
                string origin = (info.flags & NativeMethods.LLKHF_INJECTED) == 0 ? "physical" :
                    info.dwExtraInfo == InputOrigin.MacModeTag ? "MacMode-output" : "other-injected";
                string scope = detector.GetForegroundProcessName();
                Count(scope + "/" + origin + "/key-events");
                string? modifier = info.vkCode switch {
                    0xA4 or 0x12 => "LeftAlt", 0xA5 => "RightAlt", 0x5B => "LeftWin", 0x5C => "RightWin",
                    0xA2 or 0x11 => "LeftCtrl", 0xA3 => "RightCtrl", _ => null
                };
                if (modifier != null) {
                    Count(scope + "/" + origin + "/" + modifier + ((info.flags & 0x80) == 0 ? "-down" : "-up"));
                    latest = modifier + ((info.flags & 0x80) == 0 ? " down" : " up") + " (" + origin + ")";
                    if (transitions.Count < 150) transitions.Add(new { ms = started.ElapsedMilliseconds, modifier, down = (info.flags & 0x80) == 0, origin, flags = info.flags, scanCode = info.scanCode });
                }
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, n, w, l);
        };
        NativeMethods.LowLevelMouseProc mouse = (n, w, l) => {
            if (n >= 0) {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(l);
                Count((info.flags & NativeMethods.LLMHF_INJECTED) == 0 ? "physical-mouse-events" : "injected-mouse-events");
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, n, w, l);
        };
        IntPtr module = NativeMethods.GetModuleHandle(null);
        IntPtr kh = NativeMethods.SetWindowsHookEx(13, keyboard, module, 0);
        IntPtr mh = NativeMethods.SetWindowsHookEx(14, mouse, module, 0);
        if (kh == IntPtr.Zero || mh == IntPtr.Zero) throw new InvalidOperationException("Cannot install passive diagnostic hooks");
        using var timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (_, _) => {
            label.Text = "Passive trace running. Use your shortcuts in the app you want to check.\n\nLast modifier: " + latest + "\nCloses in " + Math.Max(0, seconds - started.ElapsedMilliseconds / 1000) + " seconds.";
            File.WriteAllText(output, JsonSerializer.Serialize(new { counts, transitions }, new JsonSerializerOptions { WriteIndented = true }));
            if (started.Elapsed.TotalSeconds >= seconds) window.Close();
        };
        timer.Start();
        Console.WriteLine("Passive modifier trace ready; no text is recorded.");
        try { Application.Run(window); }
        finally {
            NativeMethods.UnhookWindowsHookEx(kh);
            NativeMethods.UnhookWindowsHookEx(mh);
            GC.KeepAlive(keyboard); GC.KeepAlive(mouse);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { counts, transitions }, new JsonSerializerOptions { WriteIndented = true }));
        window.Dispose();
    }
}
