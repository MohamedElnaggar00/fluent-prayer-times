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
    [StructLayout(LayoutKind.Sequential)] struct CurPt { public int X, Y; }

    readonly Settings _s;
    bool _drag;
    CurPt _start;
    PointInt32 _winStart;

    public Action? HiddenByUser;

    public WidgetWindow(Settings s)
    {
        _s = s;
        InitializeComponent();
        Title = "Fluent Prayer Times Widget";
        SystemBackdrop = new DesktopAcrylicBackdrop();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double dpi = GetDpiForWindow(hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(290 * dpi), h = (int)(150 * dpi);

        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.IsAlwaysOnTop = _s.WidgetPinned;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        BtnPin.IsChecked = _s.WidgetPinned;
        AppWindow.Closing += (a, e) => { e.Cancel = true; a.Hide(); };

        int x, y;
        if (_s.WidgetX != int.MinValue && _s.WidgetY != int.MinValue) { x = _s.WidgetX; y = _s.WidgetY; }
        else
        {
            var wa = DisplayArea.Primary.WorkArea;
            x = wa.X + wa.Width - w - (int)(24 * dpi);
            y = wa.Y + (int)(24 * dpi);
        }
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }

    public void SetInfo(string name, string time, string count)
    {
        TxtName.Text = name; TxtTime.Text = time; TxtCount.Text = count;
    }

    public void ShowWidget() { AppWindow.Show(); Activate(); }
    public void HideWidget() { AppWindow.Hide(); }

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
        _s.WidgetX = AppWindow.Position.X;
        _s.WidgetY = AppWindow.Position.Y;
        _s.Save();
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
