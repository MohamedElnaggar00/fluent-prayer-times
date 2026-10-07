using System.Runtime.InteropServices;
namespace FluentPrayerTimes;

// Follow the user's Windows effect preferences, including reduced motion.
static class NativeMotion
{
    [DllImport("user32.dll")] static extern bool SystemParametersInfoW(uint action, uint param, out int value, uint flags);
    [DllImport("user32.dll")] static extern bool AnimateWindow(IntPtr hwnd, uint duration, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint ex, string cls, string? title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
    static bool Enabled(uint action) => SystemParametersInfoW(action, 0, out int enabled, 0) && enabled != 0;
    public static int HoverDelay()
    {
        // Ask a native tooltip control for TTDT_INITIAL. Windows chooses the default.
        var tip = CreateWindowExW(0, "tooltips_class32", null, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        try
        {
            int delay = tip == IntPtr.Zero ? 0 : (int)SendMessageW(tip, 0x0400 + 21, new IntPtr(3), IntPtr.Zero);
            return delay > 0 ? delay : (int)GetDoubleClickTime();
        }
        finally { if (tip != IntPtr.Zero) DestroyWindow(tip); }
    }
    public static void Tooltip(IntPtr hwnd, bool show, bool below)
    {
        bool animate = Enabled(0x1042) && Enabled(0x1016); // client area + tooltip animation
        uint effect = Enabled(0x1018) ? 0x80000u : (0x40000u | (below == show ? 4u : 8u));
        if (!animate || !AnimateWindow(hwnd, 160, effect | (show ? 0u : 0x10000u)))
            ShowWindow(hwnd, show ? 4 : 0);
    }
    public static void Window(IntPtr hwnd, bool show)
    {
        // Native top-level expand/collapse; DWM renders the actual window.
        if (!Enabled(0x1042) || !AnimateWindow(hwnd, 180, 0x10u | (show ? 0x20000u : 0x10000u)))
            ShowWindow(hwnd, show ? 5 : 0);
    }
}
