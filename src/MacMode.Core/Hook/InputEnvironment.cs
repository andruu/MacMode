using System.Runtime.InteropServices;
using System.Text;

namespace MacMode.Core.Hook;

internal static class InputEnvironment
{
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index,
        StringBuilder value, int length, out int needed);

    private static string Name(IntPtr handle)
    {
        var value = new StringBuilder(256);
        return GetUserObjectInformation(handle, 2, value, 512, out _) ? value.ToString() : "unknown";
    }

    public static string Describe() =>
        $"{Name(GetProcessWindowStation())}\\{Name(GetThreadDesktop(GetCurrentThreadId()))}, thread {GetCurrentThreadId()}";
}
