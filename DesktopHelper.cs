using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SnakeScreensaver;

internal static class DesktopHelper
{
    private const int UOI_NAME = 2;

    public static void Log(string msg)
    {
        try
        {
            var p = Path.Combine(Path.GetTempPath(), "SnakeScreensaver.log");
            File.AppendAllText(p, $"{DateTime.Now:O} {msg}\r\n");
        }
        catch { }
    }

    public static string CurrentName()
    {
        try
        {
            var h = GetThreadDesktop(GetCurrentThreadId());
            var nb = new char[256];
            if (GetUserObjectInformation(h, UOI_NAME, nb, (uint)nb.Length, out _))
            {
                int n = 0;
                while (n < nb.Length && nb[n] != '\0') n++;
                return new string(nb, 0, n);
            }
        }
        catch { }
        return "<err>";
    }

    public static void TrySwitchToDefault()
    {
        try
        {
            var cur = CurrentName();
            Log($"pre-switch desktop='{cur}'");
            if (cur == "Default") { Log("already Default, no switch needed"); return; }

            const int DESKTOP_CREATEWINDOW = 0x0002;
            const int DESKTOP_CREATEMENU = 0x0004;
            const int DESKTOP_ENUMERATE = 0x0040;
            const int DESKTOP_WRITEOBJECTS = 0x0080;
            const int DESKTOP_READOBJECTS = 0x0001;
            const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
            var access = DESKTOP_CREATEWINDOW | DESKTOP_CREATEMENU | DESKTOP_ENUMERATE
                       | DESKTOP_WRITEOBJECTS | DESKTOP_READOBJECTS
                       | STANDARD_RIGHTS_REQUIRED;

            var hDef = OpenDesktopW("Default", 0, false, access);
            if (hDef == IntPtr.Zero)
            {
                Log($"OpenDesktop(Default) fail WinError={Marshal.GetLastWin32Error()}");
                return;
            }
            try
            {
                if (SetThreadDesktop(hDef)) Log("SetThreadDesktop(Default) OK");
                else Log($"SetThreadDesktop(Default) fail WinError={Marshal.GetLastWin32Error()}");
            }
            finally { CloseDesktop(hDef); }
        }
        catch (Exception ex) { Log($"switch ex: {ex.Message}"); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint dwThreadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [Out] char[] pvInfo, uint cchInfo, out uint pcchWritten);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenDesktopW(string lpszDesktop, int dwFlags, bool fInherit, int dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr hDesktop);
}
