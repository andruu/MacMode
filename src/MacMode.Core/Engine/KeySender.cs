using System.Runtime.InteropServices;
using MacMode.Core.Hook;
using MacMode.Core.Logging;

namespace MacMode.Core.Engine;

/// <summary>
/// Wraps SendInput to inject synthetic key events with balanced keydown/keyup.
/// </summary>
public static class KeySender
{
    /// <summary>Creates a keydown INPUT struct for the given virtual key.</summary>
    public static NativeMethods.INPUT MakeKeyDown(int vk)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    /// <summary>Creates a keyup INPUT struct for the given virtual key.</summary>
    public static NativeMethods.INPUT MakeKeyUp(int vk)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    /// <summary>Sends a batch of INPUT events atomically via a single SendInput call.</summary>
    public static void SendBatch(NativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0) return;

        int cbSize = Marshal.SizeOf<NativeMethods.INPUT>();
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, cbSize);

        if (sent != inputs.Length)
        {
            int err = Marshal.GetLastWin32Error();
            Logger.Error($"SendInput expected {inputs.Length} but sent {sent} (error={err}, cbSize={cbSize})");
        }
        else
        {
            Logger.Debug($"SendInput sent {sent} events OK (cbSize={cbSize})");
        }
    }

    /// <summary>Sends a keydown event for the given virtual key.</summary>
    public static void SendKeyDown(int vk)
    {
        SendBatch(new[] { MakeKeyDown(vk) });
    }

    /// <summary>Sends a keyup event for the given virtual key.</summary>
    public static void SendKeyUp(int vk)
    {
        SendBatch(new[] { MakeKeyUp(vk) });
    }

    /// <summary>Sends a complete keydown then keyup for the given virtual key.</summary>
    public static void SendKeyPress(int vk)
    {
        SendBatch(new[] { MakeKeyDown(vk), MakeKeyUp(vk) });
    }

    public static NativeMethods.INPUT MakeMouseDown()
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN }
            }
        };
    }

    public static NativeMethods.INPUT MakeMouseUp()
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP }
            }
        };
    }

    /// <summary>
    /// Sends a chord: presses all modifiers, presses the key, releases the key, releases modifiers.
    /// All in a single atomic SendInput call.
    /// </summary>
    public static void SendChord(int[] modifierVks, int keyVk)
    {
        var inputs = new List<NativeMethods.INPUT>();

        foreach (int mod in modifierVks)
            inputs.Add(MakeKeyDown(mod));

        inputs.Add(MakeKeyDown(keyVk));
        inputs.Add(MakeKeyUp(keyVk));

        for (int i = modifierVks.Length - 1; i >= 0; i--)
            inputs.Add(MakeKeyUp(modifierVks[i]));

        SendBatch(inputs.ToArray());
    }
}
