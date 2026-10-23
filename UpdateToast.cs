using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentPrayerTimes;

/// <summary>Fluent toast-style notification shown above the taskbar when a newer release exists. The button downloads the installer inside the app, checks it, and runs it only after the user presses Install now.</summary>
sealed class UpdateToast : Window
{
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

    readonly IntPtr _hwnd;
    readonly Grid _root = new();
    readonly string _version, _fallback;
    readonly UpdateAsset _asset;
    readonly TextBlock _title = new() { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _sub = HijriUtil.Sec(new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap });
    readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
    readonly StackPanel _btns = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    CancellationTokenSource? _cts;
    string? _file;

    public UpdateToast(string version, UpdateAsset asset, string fallbackUrl, string theme)
    {
        _version = version; _asset = asset; _fallback = fallbackUrl;
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
        var texts = new StackPanel { Spacing = 2, MaxWidth = 260 };
        texts.Children.Add(_title); texts.Children.Add(_sub);
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        head.Children.Add(icon); head.Children.Add(texts);

        var st = new StackPanel { Spacing = 12, Padding = new Thickness(18, 16, 18, 16), VerticalAlignment = VerticalAlignment.Center };
        st.Children.Add(head); st.Children.Add(_bar); st.Children.Add(_btns);
        _root.Children.Add(tint); _root.Children.Add(st);
        _root.FlowDirection = L.Flow;
        _root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        Content = _root;
        Closed += (a, b) => { try { _cts?.Cancel(); } catch { } };
        SetState("offer");
    }

    Button AddButton(string text, Action click, bool accent = false)
    {
        var button = new Button { Content = text, MinWidth = 96 };
        if (accent) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        button.Click += (a, b) => click();
        _btns.Children.Add(button);
        return button;
    }

    void SetState(string state, string? detail = null)
    {
        _btns.Children.Clear();
        _bar.Visibility = state == "downloading" ? Visibility.Visible : Visibility.Collapsed;
        switch (state)
        {
            case "downloading":
                _title.Text = L.T("جارٍ تنزيل التحديث");
                _sub.Text = "v" + _version + "  0%";
                _bar.Value = 0;
                AddButton(L.T("إلغاء"), CancelDownload);
                break;
            case "ready":
                _title.Text = L.T("تم التنزيل والتحقق من الملف");
                _sub.Text = L.T("سيُغلق التطبيق ويكمل المثبّت التحديث.");
                AddButton(L.T("تثبيت الآن"), InstallNow, true);
                AddButton(L.T("لاحقًا"), () => Close());
                break;
            case "error":
                _title.Text = L.T("تعذر تنزيل التحديث");
                _sub.Text = detail ?? "";
                AddButton(L.T("إعادة المحاولة"), Begin, true);
                AddButton(L.T("فتح في المتصفح"), OpenInBrowser);
                break;
            default:
                _title.Text = L.T("تحديث جديد متاح");
                _sub.Text = L.T("الإصدار ") + _version + L.T(" جاهز للتنزيل");
                AddButton(L.T("تنزيل وتثبيت"), Begin, true);
                AddButton(L.T("لاحقًا"), () => Close());
                break;
        }
    }

    /// <summary>Screenshot previews only: shows a state without downloading anything.</summary>
    public void PreviewState(string state)
    {
        SetState(state, state == "error" ? L.T("لم يكتمل التنزيل. تحقق من اتصالك بالإنترنت وحاول مرة أخرى.") : null);
        if (state == "downloading") { _bar.Value = 46; _sub.Text = "v" + _version + "  46%"; }
    }

    void OpenInBrowser()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_fallback) { UseShellExecute = true }); } catch { }
        Close();
    }

    void CancelDownload() { try { _cts?.Cancel(); } catch { } }

    /// <summary>Starts the download after the user pressed the download button. The installer is not started until "Install now" is pressed.</summary>
    public async void Begin()
    {
        if (_cts != null) return;
        var cts = _cts = new CancellationTokenSource();
        SetState("downloading");
        try
        {
            _file = await UpdateInstaller.DownloadAsync(_asset, pct => { _bar.Value = pct; _sub.Text = "v" + _version + "  " + (int)pct + "%"; }, cts.Token);
            _cts = null;
            SetState("ready");
        }
        catch (OperationCanceledException)
        {
            _cts = null;
            SetState("offer");
        }
        catch (Exception error)
        {
            _cts = null;
            try { Directory.CreateDirectory(App.AppData); File.AppendAllText(Path.Combine(App.AppData, "update.log"), DateTime.Now + " download: " + error.Message + "\n"); } catch { }
            SetState("error", error is InvalidDataException ? L.T("فشل التحقق من الملف المنزّل. لم يتم تشغيل أي شيء.") : L.T("لم يكتمل التنزيل. تحقق من اتصالك بالإنترنت وحاول مرة أخرى."));
        }
    }

    async void InstallNow()
    {
        try
        {
            var file = _file;
            if (file == null || !File.Exists(file)) throw new FileNotFoundException();
            await Task.Run(() => UpdateInstaller.Verify(file, _asset));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            Close();
        }
        catch (Exception error)
        {
            try { File.AppendAllText(Path.Combine(App.AppData, "update.log"), DateTime.Now + " install: " + error.Message + "\n"); } catch { }
            _cts = null;
            SetState("error", L.T("فشل التحقق من الملف المنزّل. لم يتم تشغيل أي شيء."));
        }
    }

    public void ShowToast()
    {
        var wa = DisplayArea.Primary.WorkArea;
        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(380 * dpi), h = (int)(190 * dpi);
        var rect = new RectInt32(wa.X + wa.Width - w - (int)(12 * dpi), wa.Y + wa.Height - h - (int)(12 * dpi), w, h);
        AppWindow.MoveAndResize(rect);
        ShowWindow(_hwnd, 4);
        AppWindow.MoveAndResize(rect);
        WindowChrome.Apply(_hwnd);
        SetWindowPos(_hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x2 | 0x1 | 0x10);
        _root.InvalidateMeasure(); _root.UpdateLayout();
    }
}
