using System.Runtime.InteropServices;

namespace FluentPrayerTimes;

/// <summary>Notification-area icon built on Shell_NotifyIcon, hooked to an existing window via a subclass.</summary>
sealed class TrayIcon : IDisposable
{
    const uint WM_TRAY = 0x8001;
    const uint WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public uint cbSize; public IntPtr hWnd; public uint uID; public uint uFlags; public uint uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState; public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint msg, ref NOTIFYICONDATA d);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(IntPtr h, SubclassProc p, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(IntPtr h, SubclassProc p, UIntPtr id);
    [DllImport("comctl32.dll")] static extern IntPtr DefSubclassProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadImageW(IntPtr inst, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr m, uint flags, UIntPtr id, string text);
    [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr m, uint flags, int x, int y, int r, IntPtr hwnd, IntPtr rc);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr m);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessageW(string s);

    readonly IntPtr _hwnd;
    readonly Action _onClick;
    readonly Action<int> _onMenu;
    readonly (int id, string text)[] _items;
    readonly SubclassProc _proc;
    readonly uint _taskbarCreated;
    IntPtr _icon;
    string _tip = "";
    bool _added;

    public TrayIcon(IntPtr hwnd, string icoPath, (int id, string text)[] items, Action onClick, Action<int> onMenu)
    {
        _hwnd = hwnd; _items = items; _onClick = onClick; _onMenu = onMenu;
        int sz = GetSystemMetrics(49);
        if (sz <= 0) sz = 16;
        _icon = LoadImageW(IntPtr.Zero, icoPath, 1, sz, sz, 0x10);
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        _proc = WndProc;
        SetWindowSubclass(_hwnd, _proc, new UIntPtr(1), UIntPtr.Zero);
        Add();
    }

    NOTIFYICONDATA Data(uint flags)
    {
        var d = new NOTIFYICONDATA { hWnd = _hwnd, uID = 1, uFlags = flags, uCallbackMessage = WM_TRAY, hIcon = _icon, szTip = _tip, szInfo = "", szInfoTitle = "" };
        d.cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>();
        return d;
    }

    void Add() { var d = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP); _added = Shell_NotifyIconW(NIM_ADD, ref d); }

    public void SetTip(string tip)
    {
        if (tip.Length > 120) tip = tip[..120];
        if (tip == _tip) return;
        _tip = tip;
        if (!_added) return;
        var d = Data(NIF_TIP);
        Shell_NotifyIconW(NIM_MODIFY, ref d);
    }

    IntPtr WndProc(IntPtr h, uint msg, IntPtr w, IntPtr l, UIntPtr id, UIntPtr data)
    {
        if (msg == WM_TRAY)
        {
            uint ev = (uint)((long)l & 0xFFFF);
            if (ev == WM_LBUTTONUP) _onClick();
            else if (ev == WM_RBUTTONUP) ShowMenu();
            return IntPtr.Zero;
        }
        if (_taskbarCreated != 0 && msg == _taskbarCreated) { Add(); }
        return DefSubclassProc(h, msg, w, l);
    }

    void ShowMenu()
    {
        GetCursorPos(out var pt);
        var m = CreatePopupMenu();
        foreach (var (id, text) in _items) AppendMenuW(m, 0, new UIntPtr((uint)id), text);
        SetForegroundWindow(_hwnd);
        int cmd = TrackPopupMenu(m, 0x100 | 0x20 | 0x2, pt.x, pt.y, 0, _hwnd, IntPtr.Zero);
        PostMessageW(_hwnd, 0, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(m);
        if (cmd > 0) _onMenu(cmd);
    }

    public void Dispose()
    {
        if (_added) { var d = Data(0); Shell_NotifyIconW(NIM_DELETE, ref d); _added = false; }
        RemoveWindowSubclass(_hwnd, _proc, new UIntPtr(1));
        if (_icon != IntPtr.Zero) { DestroyIcon(_icon); _icon = IntPtr.Zero; }
    }
}
