using Microsoft.UI.Composition;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentPrayerTimes;

/// <summary>Acrylic backdrop that stays "active" even though the window never takes focus.</summary>
sealed class ActiveAcrylic : SystemBackdrop
{
    DesktopAcrylicController? _c;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        _c = new DesktopAcrylicController();
        var cfg = GetDefaultSystemBackdropConfiguration(target, xamlRoot);
        cfg.IsInputActive = true;
        _c.SetSystemBackdropConfiguration(cfg);
        _c.AddSystemBackdropTarget(target);
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        _c?.RemoveSystemBackdropTarget(target);
        _c?.Dispose(); _c = null;
    }
}

/// <summary>Large, readable card shown when the mouse hovers the tray icon (replaces the tiny system tooltip).</summary>
sealed class HoverWindow : Window
{
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CurPt p);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] struct CurPt { public int X, Y; }

    readonly IntPtr _hwnd;
    readonly Grid _root = new();
    readonly TextBlock _title = new() { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _unit = HijriUtil.Sec(new TextBlock { FontSize = 18, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) });
    readonly TextBlock _count = HijriUtil.Themed(new TextBlock { FontSize = 42, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FlowDirection = FlowDirection.LeftToRight }, "AccentTextFillColorPrimaryBrush");
    readonly TextBlock _sub = HijriUtil.Sec(new TextBlock { FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis });
    readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    readonly Func<bool> _overTray;
    int _miss;
    public bool Shown { get; private set; }

    public HoverWindow(Func<bool> overTray)
    {
        _overTray = overTray;
        Title = "Fluent Prayer Times Hover";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false; p.IsMaximizable = false; p.IsMinimizable = false; p.IsAlwaysOnTop = true;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        try { SystemBackdrop = new ActiveAcrylic(); } catch { }
        AppWindow.Closing += (a, e) => { e.Cancel = true; Hide(); };
        long ex = (long)GetWindowLongPtr(_hwnd, -20);
        SetWindowLongPtr(_hwnd, -20, (IntPtr)(ex | 0x80 | 0x08000000 | 0x8)); // toolwindow, noactivate, topmost
        WindowChrome.Apply(_hwnd);

        // soft tint so the text stays clear on any wallpaper
        var tint = HijriUtil.Themed("SolidBackgroundFillColorBaseBrush", 0.35);
        var st = new StackPanel { Spacing = 2, Padding = new Thickness(20, 14, 20, 14), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(_unit); row.Children.Add(_count);
        st.Children.Add(_title); st.Children.Add(row); st.Children.Add(_sub);
        _root.Children.Add(tint); _root.Children.Add(st);
        _root.FlowDirection = FlowDirection.RightToLeft;
        Content = _root;

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += (a, b) => Watch();
    }

    public void Update(string title, string count, string unit, string sub)
    {
        _title.Text = title; _count.Text = count; _unit.Text = unit; _sub.Text = sub;
    }

    bool CursorInside()
    {
        if (!GetCursorPos(out var c)) return false;
        var pos = AppWindow.Position; var sz = AppWindow.Size;
        return c.X >= pos.X - 4 && c.X <= pos.X + sz.Width + 4 && c.Y >= pos.Y - 4 && c.Y <= pos.Y + sz.Height + 4;
    }

    void Watch()
    {
        if (!Shown) { _timer.Stop(); return; }
        if (_overTray() || CursorInside()) _miss = 0;
        else if (++_miss >= 3) Hide();
    }

    public void ShowAt(int ax, int ay, string theme)
    {
        _miss = 0;
        if (Shown) return;
        var wa = DisplayArea.GetFromPoint(new PointInt32(ax, ay), DisplayAreaFallback.Nearest).WorkArea;
        _root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(340 * dpi), h = (int)(160 * dpi);
        int x = Math.Clamp(ax - w / 2, wa.X + 8, wa.X + wa.Width - w - 8);
        int y = ay > wa.Y + wa.Height / 2 ? wa.Y + wa.Height - h - 8 : wa.Y + 8;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        ShowWindow(_hwnd, 4); // SW_SHOWNOACTIVATE
        WindowChrome.Apply(_hwnd);
        Shown = true;
        _timer.Start();
    }

    public void Hide()
    {
        Shown = false;
        _timer.Stop();
        try { AppWindow.Hide(); } catch { }
    }
}
