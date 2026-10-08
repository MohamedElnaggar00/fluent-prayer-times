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
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    /// <summary>Keeps the card above every other window without taking focus (HWND_TOPMOST, NOMOVE | NOSIZE | NOACTIVATE).</summary>
    void Raise() { try { SetWindowPos(_hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x2 | 0x1 | 0x10); } catch { } }
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
    readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _delay;
    long _entered;
    int _hoverDelay;
    Action? _pending;
    bool _below;
    RectInt32 _rect;
    int _gen;
    Microsoft.UI.Xaml.Media.Animation.Storyboard? _motion;
    readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _heal;
    int _healTries;
    readonly Brush _accent;
    public bool Shown { get; private set; }
    public bool Pinned;   // CI preview only: keep the card up without the mouse over the tray icon

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
        _root.RenderTransform = new TranslateTransform();
        _accent = _count.Foreground;
        Content = _root;

        // Self-heal: if the card was shown before WinUI finished sizing/painting it (the empty small box), re-apply geometry and force a layout pass.
        _heal = DispatcherQueue.CreateTimer();
        _heal.Interval = TimeSpan.FromMilliseconds(90);
        _heal.Tick += (a, b) => Heal();

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += (a, b) => Watch();
        _delay = DispatcherQueue.CreateTimer();
        _delay.Interval = TimeSpan.FromMilliseconds(50);
        _delay.Tick += (a, b) =>
        {
            if (!_overTray()) { CancelPending(); return; }
            if (Environment.TickCount64 - _entered < _hoverDelay) return;
            var show = _pending;
            CancelPending();
            show?.Invoke();
        };
    }

    public void RefreshTheme() { var w = _root.RequestedTheme; _root.RequestedTheme = w == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark; _root.RequestedTheme = w; }

    public void RequestShow(Action show)
    {
        if (Shown || _delay.IsRunning) return;
        _pending = show; _hoverDelay = NativeMotion.HoverDelay(); _entered = Environment.TickCount64; _delay.Start();
    }

    void CancelPending() { _delay.Stop(); _pending = null; }

    public void Update(string title, string count, string unit, string sub, Windows.UI.Color? color = null, bool message = false)
    {
        _title.Text = title; _count.Text = count; _unit.Text = unit; _sub.Text = sub;
        _count.Foreground = color.HasValue ? new SolidColorBrush(color.Value) : _accent;
        _count.FontSize = message ? 28 : 42;
    }

    public string Diag() => $"shown={Shown} size={AppWindow.Size.Width}x{AppWindow.Size.Height} want={_rect.Width}x{_rect.Height} actual={_root.ActualWidth:F0}x{_root.ActualHeight:F0} opacity={_root.Opacity:F2}";

    void Heal()
    {
        if (!Shown) { _heal.Stop(); return; }
        Raise();
        try
        {
            var sz = AppWindow.Size;
            bool bad = Math.Abs(sz.Width - _rect.Width) > 2 || Math.Abs(sz.Height - _rect.Height) > 2 || _root.ActualWidth < 50 || _root.ActualHeight < 50;
            if (bad)
            {
                AppWindow.MoveAndResize(_rect);
                _root.InvalidateMeasure(); _root.UpdateLayout();
                ShowWindow(_hwnd, 4);
            }
            if (!bad || ++_healTries > 8) { _heal.Stop(); }
        }
        catch { _heal.Stop(); }
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
        if (Pinned) return;
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
        _rect = new RectInt32(x, y, w, h);
        _below = y > ay;
        _gen++;
        _motion?.Stop();
        _root.Opacity = 0;
        AppWindow.MoveAndResize(_rect);
        ShowWindow(_hwnd, 4);                 // SW_SHOWNOACTIVATE: plain show, no AnimateWindow capture of a possibly unpainted window
        AppWindow.MoveAndResize(_rect);       // size again once the window is really visible
        WindowChrome.Apply(_hwnd);
        Raise();
        _root.InvalidateMeasure(); _root.UpdateLayout();
        Shown = true;
        _timer.Start();
        _healTries = 0; _heal.Start();
        Animate(true, _gen);
    }

    void Animate(bool show, int gen, Action? done = null)
    {
        _motion?.Stop();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled || !NativeMotion.TooltipAnimations())
        {
            _root.Opacity = show ? 1 : 0; ((TranslateTransform)_root.RenderTransform).Y = 0; done?.Invoke(); return;
        }
        var tr = new TranslateTransform(); _root.RenderTransform = tr;
        double time = show ? 160 : 120, dist = _below ? -10 : 10; // slides in from the taskbar side
        var ease = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = show ? Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut : Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn };
        var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = show ? 0 : 1, To = show ? 1 : 0, Duration = new Duration(TimeSpan.FromMilliseconds(time)), EasingFunction = ease };
        var slide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = show ? dist : 0, To = show ? 0 : dist, Duration = new Duration(TimeSpan.FromMilliseconds(time)), EasingFunction = ease, EnableDependentAnimation = true };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, _root); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slide, tr); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slide, "Y");
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); sb.Children.Add(fade); sb.Children.Add(slide);
        _motion = sb;
        sb.Completed += (a, b) => { if (gen == _gen) { _root.Opacity = show ? 1 : 0; done?.Invoke(); } };
        sb.Begin();
    }

    public void Hide()
    {
        CancelPending();
        _timer.Stop(); _heal.Stop();
        if (!Shown) return;
        Shown = false;
        int g = ++_gen;
        Animate(false, g, () => { if (g == _gen && !Shown) ShowWindow(_hwnd, 0); });
    }
}
