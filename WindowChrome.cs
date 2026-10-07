using System.Runtime.InteropServices;

namespace FluentPrayerTimes;

/// <summary>Removes the thin light border Windows 11 draws around borderless WinUI windows.</summary>
static class WindowChrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    const int GWL_STYLE = -16;
    const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_BORDER = 0x00800000, WS_DLGFRAME = 0x00400000;
    const uint SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

    public static void Apply(IntPtr hwnd)
    {
        try
        {
            long st = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
            st &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER | WS_DLGFRAME);
            SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)st);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
        catch { }
        try
        {
            int none = unchecked((int)0xFFFFFFFE), round = 2;
            DwmSetWindowAttribute(hwnd, 34, ref none, 4);   // DWMWA_BORDER_COLOR = none
            DwmSetWindowAttribute(hwnd, 33, ref round, 4);  // DWMWA_WINDOW_CORNER_PREFERENCE = round
        }
        catch { }
    }
}
