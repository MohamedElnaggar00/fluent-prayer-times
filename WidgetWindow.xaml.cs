using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentPrayerTimes;

/// <summary>Small floating desktop widget: next prayer + countdown. Draggable and pinnable (always on top).</summary>
public sealed partial class WidgetWindow : Window
{
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CurPt p);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [StructLayout(LayoutKind.Sequential)] struct CurPt { public int X, Y; }

    // 8 snap positions: TL, T, TR, L, R, BL, B, BR (fractions of the free area of the work area)
    static readonly (double fx, double fy)[] Anchors = { (0, 0), (0.5, 0), (1, 0), (0, 0.5), (1, 0.5), (0, 1), (0.5, 1), (1, 1) };
    readonly IntPtr _hwnd;
    bool _wantVisible;
    Microsoft.UI.Dispatching.DispatcherQueueTimer? _keep;

    readonly Settings _s;
    bool _drag;
    CurPt _start;
    PointInt32 _winStart;

    public void RefreshTheme() { var w = Surface.RequestedTheme; Surface.RequestedTheme = w == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark; Surface.RequestedTheme = w; }

    public Action? HiddenByUser;

    public WidgetWindow(Settings s)
    {
        _s = s;
        InitializeComponent();
        L.Register(Content); ApplyLang();
        Title = "Fluent Prayer Times Widget";
        SystemBackdrop = new DesktopAcrylicBackdrop();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _hwnd = hwnd;
        double dpi = GetDpiForWindow(hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(180 * dpi), h = (int)(110 * dpi);

        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.IsAlwaysOnTop = _s.WidgetPinned;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        WindowChrome.Apply(hwnd);
        BtnPin.IsChecked = _s.WidgetPinned;
        AppWindow.Closing += (a, e) => { e.Cancel = true; a.Hide(); };

        // tool window: no taskbar button, never takes focus
        long ex = (long)GetWindowLongPtr(hwnd, -20);
        SetWindowLongPtr(hwnd, -20, (IntPtr)(ex | 0x80 | 0x08000000));
        AppWindow.Resize(new SizeInt32(w, h));
        ApplySnap(Math.Clamp(_s.WidgetSnap, 0, 7));

        // "Show desktop" minimises/hides every window: bring the widget straight back.
        _keep = DispatcherQueue.CreateTimer();
        _keep.Interval = TimeSpan.FromMilliseconds(400);
        _keep.Tick += (a, b) => KeepAlive();
        _keep.Start();
    }

    void KeepAlive()
    {
        if (!_wantVisible) return;
        try
        {
            if (IsIconic(_hwnd) || !IsWindowVisible(_hwnd))
            {
                ShowWindow(_hwnd, 4); // SW_SHOWNOACTIVATE
                WindowChrome.Apply(_hwnd);
            }
            // SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE; topmost when pinned
            if (_s.WidgetPinned) SetWindowPos(_hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x2 | 0x1 | 0x10);
        }
        catch { }
    }

    void ApplySnap(int idx)
    {
        try
        {
            var pos = AppWindow.Position; var sz = AppWindow.Size;
            var wa = DisplayArea.GetFromRect(new RectInt32(pos.X, pos.Y, sz.Width, sz.Height), DisplayAreaFallback.Nearest).WorkArea;
            double dpi = Math.Max(1, GetDpiForWindow(_hwnd) / 96.0);
            int m = (int)(12 * dpi);
            var (fx, fy) = Anchors[idx];
            int x = wa.X + m + (int)(fx * (wa.Width - sz.Width - 2 * m));
            int y = wa.Y + m + (int)(fy * (wa.Height - sz.Height - 2 * m));
            AppWindow.Move(new PointInt32(x, y));
        }
        catch { }
    }

    void SnapNearest()
    {
        var pos = AppWindow.Position; var sz = AppWindow.Size;
        var wa = DisplayArea.GetFromRect(new RectInt32(pos.X, pos.Y, sz.Width, sz.Height), DisplayAreaFallback.Nearest).WorkArea;
        double cx = (pos.X + sz.Width / 2.0 - wa.X) / wa.Width, cy = (pos.Y + sz.Height / 2.0 - wa.Y) / wa.Height;
        int best = 2; double bd = double.MaxValue;
        for (int i = 0; i < Anchors.Length; i++)
        {
            double d = Math.Pow(cx - (0.08 + 0.84 * Anchors[i].fx), 2) + Math.Pow(cy - (0.12 + 0.76 * Anchors[i].fy), 2);
            if (d < bd) { bd = d; best = i; }
        }
        _s.WidgetSnap = best; _s.Save();
        ApplySnap(best);
    }

    Brush? _countBrush;
    public void ApplyLang()
    {
        TxtHeader.FlowDirection = L.Flow; PanelInfo.FlowDirection = L.Flow;
        TxtHeader.HorizontalAlignment = L.Rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        TxtHeader.TextAlignment = L.Rtl ? TextAlignment.Right : TextAlignment.Left;
        PrayerRow.HorizontalAlignment = HorizontalAlignment.Center;
        ToolTipService.SetToolTip(BtnPin, L.T("تثبيت فوق كل النوافذ")); ToolTipService.SetToolTip(BtnHide, L.T("إخفاء"));
        L.Refresh();
    }

    public void SetInfo(string name, string time, string count, string header, Windows.UI.Color? color, bool message, string unit = "")
    {
        _countBrush ??= TxtCount.Foreground;
        TxtName.Text = name; TxtTime.Text = time; TxtCount.Text = count; TxtHeader.Text = header;
        TxtCount.Foreground = color.HasValue ? new SolidColorBrush(color.Value) : _countBrush;
        TxtCount.FontSize = message ? 15 : 32;
        TxtUnit.Text = unit; TxtUnit.Visibility = string.IsNullOrEmpty(unit) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowWidget() { _wantVisible = true; ApplySnap(Math.Clamp(_s.WidgetSnap, 0, 7)); AppWindow.Show(); WindowChrome.Apply(_hwnd); }
    public void HideWidget() { _wantVisible = false; AppWindow.Hide(); }

    void Surface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!GetCursorPos(out _start)) return;
        _winStart = AppWindow.Position;
        _drag = true;
        Surface.CapturePointer(e.Pointer);
    }

    void Surface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_drag || !GetCursorPos(out var c)) return;
        AppWindow.Move(new PointInt32(_winStart.X + (c.X - _start.X), _winStart.Y + (c.Y - _start.Y)));
    }

    void Surface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_drag) return;
        _drag = false;
        Surface.ReleasePointerCapture(e.Pointer);
        SnapNearest();
    }

    void Pin_Click(object sender, RoutedEventArgs e)
    {
        _s.WidgetPinned = BtnPin.IsChecked == true;
        if (AppWindow.Presenter is OverlappedPresenter p) p.IsAlwaysOnTop = _s.WidgetPinned;
        _s.Save();
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        HideWidget();
        HiddenByUser?.Invoke();
    }
}
