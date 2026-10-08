using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentPrayerTimes;

/// <summary>Fluent toast-style notification shown above the taskbar when a newer release exists. The button opens the installer download directly.</summary>
sealed class UpdateToast : Window
{
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

    readonly IntPtr _hwnd;
    readonly Grid _root = new();

    public UpdateToast(string version, string url, string theme)
    {
        Title = "Fluent Prayer Times Update";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false; p.IsMaximizable = false; p.IsMinimizable = false; p.IsAlwaysOnTop = true;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        try { SystemBackdrop = new ActiveAcrylic(); } catch { }
        long ex = (long)GetWindowLongPtr(_hwnd, -20);
        SetWindowLongPtr(_hwnd, -20, (IntPtr)(ex | 0x80 | 0x08000000 | 0x8));
        WindowChrome.Apply(_hwnd);

        var tint = HijriUtil.Themed("SolidBackgroundFillColorBaseBrush", 0.35);
        var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), VerticalAlignment = VerticalAlignment.Top };
        icon.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        icon.Child = new FontIcon { Glyph = "\uE896", FontSize = 18, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = L.T("تحديث جديد متاح"), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var sub = HijriUtil.Sec(new TextBlock { Text = L.T("الإصدار ") + version + L.T(" جاهز للتنزيل"), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(title); texts.Children.Add(sub);
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        head.Children.Add(icon); head.Children.Add(texts);

        var dl = new Button { Content = L.T("تحميل"), MinWidth = 120, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        dl.Click += (a, b) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            Close();
        };
        var later = new Button { Content = L.T("لاحقًا"), MinWidth = 100 };
        later.Click += (a, b) => Close();
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btns.Children.Add(dl); btns.Children.Add(later);

        var st = new StackPanel { Spacing = 14, Padding = new Thickness(18, 16, 18, 16), VerticalAlignment = VerticalAlignment.Center };
        st.Children.Add(head); st.Children.Add(btns);
        _root.Children.Add(tint); _root.Children.Add(st);
        _root.FlowDirection = L.Flow;
        _root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        Content = _root;
    }

    public void ShowToast()
    {
        var wa = DisplayArea.Primary.WorkArea;
        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(360 * dpi), h = (int)(150 * dpi);
        var rect = new RectInt32(wa.X + wa.Width - w - (int)(12 * dpi), wa.Y + wa.Height - h - (int)(12 * dpi), w, h);
        AppWindow.MoveAndResize(rect);
        ShowWindow(_hwnd, 4);
        AppWindow.MoveAndResize(rect);
        WindowChrome.Apply(_hwnd);
        SetWindowPos(_hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x2 | 0x1 | 0x10);
        _root.InvalidateMeasure(); _root.UpdateLayout();
    }
}
