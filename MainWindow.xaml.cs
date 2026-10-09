using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Net.Http;
using Microsoft.Win32;
using Windows.Graphics;

namespace FluentPrayerTimes;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CurPt p);
    [StructLayout(LayoutKind.Sequential)] struct CurPt { public int X, Y; }

    const int CmdOpen = 1, CmdSettings = 2, CmdExit = 3, CmdWidget = 4, CmdUpdate = 5;
    WidgetWindow? _widget;
    string _wName = "--", _wTime = "", _wCount = "--:--", _wHeader = L.T("الصلاة القادمة"), _wUnit = "";
    Windows.UI.Color? _wColor; bool _wMsg;

    readonly Settings _s = Settings.Load();
    readonly IntPtr _hwnd;
    readonly TrayIcon _tray;
    readonly TrayMenuWindow _menu;
    readonly HoverWindow _hover;
    string _hTitle = "", _hCount = "--:--", _hUnit = "", _hSub = "";
    Windows.UI.Color? _hColor; bool _hMsg;
    Brush? _countBrush;
    string _lastIqama = "";
    bool _checkingUpdates;
    int _flyGen;
    bool _flyBelow = true;
    Microsoft.UI.Xaml.Media.Animation.Storyboard? _flyMotion;
    static readonly Windows.UI.Color Green = Windows.UI.Color.FromArgb(255, 0x9F, 0xD8, 0x9F), Amber = Windows.UI.Color.FromArgb(255, 0xEA, 0xA3, 0x00);
    readonly DispatcherQueueTimer _timer;
    readonly TextBlock[] _rowName = new TextBlock[6];
    readonly TextBlock[] _rowTime = new TextBlock[6];
    readonly Border[] _rowBox = new Border[6];
    List<Times.Place> _places = new();
    Times.Result? _res;
    DateTime _loadedDay = DateTime.MinValue;
    DateTime _lastTry = DateTime.MinValue;
    bool _loading = true;
    bool _quit;
    int _hl = -2;
    string _lastAdhan = "";
    DateTime _azkarNext = DateTime.MaxValue;

    public MainWindow(bool startHidden)
    {
        ApplyAccentResources(ParseHex(_s.Accent));
        InitializeComponent();
        L.Register(RootGrid); ApplyFlow(); L.Refresh();
        if (App.Preview != null) _s.WidgetVisible = false;   // CI preview: never inherit a saved widget state
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = L.T("مواقيت الصلاة - Fluent Prayer Times");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        if (AppWindow.Presenter is OverlappedPresenter mainPresenter) { mainPresenter.IsMaximizable = false; mainPresenter.IsMinimizable = false; }   // only close remains

        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        AppWindow.Resize(new SizeInt32((int)(470 * dpi), (int)(748 * dpi)));
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }
        AppWindow.Closing += (s, a) => { if (!_quit) { a.Cancel = true; HideFlyout(); } };
        HideFromTaskbar();
        RootGrid.RenderTransform = new TranslateTransform();
        _countBrush = TxtCountdown.Foreground;

        try
        {
            ImgTitle.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "tray.png")));
            ImgLogo.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "logo.png")));
            var v = typeof(App).Assembly.GetName().Version;
            if (v != null) TxtVersion.Text = L.T("الإصدار ") + v.Major + "." + v.Minor + "." + v.Build;
        }
        catch { }
        RootGrid.SizeChanged += (s2, e2) => FitPages();
        BuildRows();
        PanelCalendar.Children.Add(new CalendarPage());
        PanelConvert.Children.Add(new ConverterPage());
        PanelAzkar.Children.Add(new AzkarPage(_s, OnAzkarChanged));
        _azkarNext = _s.AzkarOn ? DateTime.Now.AddMinutes(Math.Max(1, _s.AzkarMinutes)) : DateTime.MaxValue;
        LoadSettingsUi();
        ApplyAppearance();
        Nav.SelectedItem = Nav.MenuItems[0];
        _loading = false;

        _menu = new TrayMenuWindow(OnMenu);
        _tray = new TrayIcon(_hwnd, Path.Combine(AppContext.BaseDirectory, TaskbarIsLight() ? "tray-dark.ico" : "tray-white.ico"),
            new[] { (CmdOpen, L.T("فتح")), (CmdWidget, L.T("ويدجت سطح المكتب")), (CmdSettings, L.T("الإعدادات")), (CmdUpdate, L.T("التحقق من التحديثات")), (CmdExit, L.T("خروج")) },
            ToggleFlyout, OnMenu, OnContext);

        _hover = new HoverWindow(() =>
        {
            if (!_tray.TryGetAnchor(out int hx, out int hy) || !GetCursorPos(out var cp)) return false;
            double d = Math.Max(1, GetDpiForWindow(_hwnd) / 96.0);
            return Math.Abs(cp.X - hx) <= 18 * d && Math.Abs(cp.Y - hy) <= 18 * d;
        });
        _tray.OnHover = () => _hover.RequestShow(() =>
        {
            if (_menu.AppWindow.IsVisible || AppWindow.IsVisible) return;
            if (!_tray.TryGetAnchor(out int hx, out int hy)) return;
            _hover.Update(_hTitle, _hCount, _hUnit, _hSub, _hColor, _hMsg);
            _hover.ShowAt(hx, hy, _s.Theme);
        });

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) => Tick();
        _timer.Start();
        _ = ReloadAsync();

        if (_s.WidgetVisible) SetWidget(true);
        if (!startHidden) ShowFlyout(false);
        StartUpdateWatch();
    }

    // ---------- window / tray ----------
    bool OnContext(int x, int y)
    {
        try
        {
            _hover.Hide();
            if (_tray.TryGetAnchor(out var ax, out var ay)) { x = ax; y = ay; }
            _menu.ShowAt(x, y, _s.Theme, _s.WidgetVisible);
            return true;
        }
        catch { return false; }
    }

    void OnMenu(int cmd)
    {
        if (cmd == CmdOpen) { Nav.SelectedItem = Nav.MenuItems[0]; ShowFlyout(false); }
        else if (cmd == CmdSettings) { Nav.SelectedItem = Nav.FooterMenuItems[0]; ShowFlyout(false); }
        else if (cmd == CmdWidget) SetWidget(!_s.WidgetVisible);
        else if (cmd == CmdUpdate) { Nav.SelectedItem = Nav.FooterMenuItems[0]; ShowFlyout(false); _ = CheckForUpdatesAsync(); }
        else if (cmd == CmdExit) Quit();
    }

    void SetWidget(bool on)
    {
        _s.WidgetVisible = on;
        _s.Save();
        if (on)
        {
            if (_widget == null)
            {
                _widget = new WidgetWindow(_s);
                _widget.HiddenByUser = () => { _s.WidgetVisible = false; _s.Save(); SyncWidgetToggle(); };
            }
            _widget.SetInfo(_wName, _wTime, _wCount, _wHeader, _wColor, _wMsg, _wUnit);
            _widget.ShowWidget();
        }
        else _widget?.HideWidget();
        SyncWidgetToggle();
    }

    void SyncWidgetToggle()
    {
        bool was = _loading; _loading = true;
        TglWidget.IsOn = _s.WidgetVisible;
        _loading = was;
    }

    void Widget_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SetWidget(TglWidget.IsOn);
    }

    void ApplyChanges_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _s.Save();
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & start \"\" \"" + exe + "\"") { CreateNoWindow = true, UseShellExecute = false });
        }
        catch { }
        Quit();
    }

    void Quit()
    {
        _quit = true;
        _hover.Hide();
        _timer.Stop();
        _tray.Dispose();
        Application.Current.Exit();
        Environment.Exit(0);
    }

    void HideFromTaskbar()
    {
        // same behaviour as Fluent Vantage Toolbar: never a taskbar button, also when opened from the tray icon
        try
        {
            AppWindow.IsShownInSwitchers = false;
            long style = GetWindowLongPtr(_hwnd, -20).ToInt64();
            style = (style | 0x80L) & ~0x40000L; // WS_EX_TOOLWINDOW on, WS_EX_APPWINDOW off
            SetWindowLongPtr(_hwnd, -20, new IntPtr(style));
        }
        catch { }
    }

    void ToggleFlyout()
    {
        _hover.Hide();
        bool minimized = (AppWindow.Presenter as OverlappedPresenter)?.State == OverlappedPresenterState.Minimized;
        if (AppWindow.IsVisible && !minimized) HideFlyout();
        else { Nav.SelectedItem = Nav.MenuItems[0]; ShowFlyout(true); }
    }

    async void HideFlyout()
    {
        if (!AppWindow.IsVisible) return;
        int g = ++_flyGen;
        await AnimateFlyout(false);
        if (g == _flyGen) { AppWindow.Hide(); RootGrid.Opacity = 1; RootGrid.RenderTransform = new TranslateTransform(); }
    }

    Task AnimateFlyout(bool show)
    {
        _flyMotion?.Stop();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) { RootGrid.Opacity = 1; RootGrid.RenderTransform = new TranslateTransform(); return Task.CompletedTask; }
        var tr = new TranslateTransform(); RootGrid.RenderTransform = tr;
        double time = show ? 220 : 170, dist = _flyBelow ? 42 : -42;
        var ease = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = show ? Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut : Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn };
        var slide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = show ? dist : 0, To = show ? 0 : dist, Duration = new Duration(TimeSpan.FromMilliseconds(time)), EasingFunction = ease, EnableDependentAnimation = true };
        var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = show ? 0 : 1, To = show ? 1 : 0, Duration = new Duration(TimeSpan.FromMilliseconds(time)), EasingFunction = ease };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slide, tr); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slide, "Y");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, RootGrid); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); _flyMotion = sb;
        sb.Children.Add(slide); sb.Children.Add(fade); sb.Begin();
        return Task.Delay((int)time);
    }

    void ShowFlyout(bool nearCursor)
    {
        ++_flyGen;
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        bool wasVisible = AppWindow.IsVisible;
        Place(nearCursor);
        HideFromTaskbar();
        if (!wasVisible) RootGrid.Opacity = 0;
        if (!AppWindow.IsVisible) AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
        if (!wasVisible) _ = AnimateFlyout(true); else { RootGrid.Opacity = 1; RootGrid.RenderTransform = new TranslateTransform(); }
    }

    void Place(bool nearCursor)
    {
        try
        {
            bool have = _tray.TryGetAnchor(out int cx, out int cy);
            if (!have && nearCursor && GetCursorPos(out var c)) { cx = c.X; cy = c.Y; have = true; }
            var area = have
                ? DisplayArea.GetFromPoint(new PointInt32(cx, cy), DisplayAreaFallback.Nearest)
                : DisplayArea.Primary;
            var wa = area.WorkArea;
            int w = AppWindow.Size.Width, h = AppWindow.Size.Height;
            if (h > wa.Height - 16) h = wa.Height - 16;
            if (w > wa.Width - 16) w = wa.Width - 16;
            int x, y;
            if (have)
            {
                x = Math.Clamp(cx - w / 2, wa.X + 8, wa.X + wa.Width - w - 8);
                y = cy > wa.Y + wa.Height / 2 ? wa.Y + wa.Height - h - 8 : wa.Y + 8;
                _flyBelow = cy > wa.Y + wa.Height / 2;
            }
            else
            {
                x = wa.X + wa.Width - w - 12;
                y = wa.Y + wa.Height - h - 12;
            }
            AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        }
        catch { }
    }

    void ApplyAppearance()
    {
        SystemBackdrop = _s.Backdrop switch
        {
            "mica" => new MicaBackdrop { Kind = MicaKind.Base },
            "acrylic" => new DesktopAcrylicBackdrop(),
            _ => new MicaBackdrop { Kind = MicaKind.BaseAlt },
        };
        RootGrid.RequestedTheme = _s.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    // ---------- accent colour ----------
    static readonly string[] AccentSwatches = { "#00C4CC", "#0078D4", "#5B5FC7", "#8764B8", "#C239B3", "#E81123", "#F7630C", "#FFB900", "#10893E", "#69797E" };
    static Windows.UI.Color DefaultAccent => Windows.UI.Color.FromArgb(255, 0, 196, 204);
    static Windows.UI.Color ParseHex(string? hex)
    {
        try
        {
            var h = (hex ?? "").TrimStart('#');
            if (h.Length == 6) return Windows.UI.Color.FromArgb(255, Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..], 16));
        }
        catch { }
        return DefaultAccent;
    }
    static Windows.UI.Color Mix(Windows.UI.Color c, byte tr, byte tg, byte tb, double t) =>
        Windows.UI.Color.FromArgb(255, (byte)(c.R + (tr - c.R) * t), (byte)(c.G + (tg - c.G) * t), (byte)(c.B + (tb - c.B) * t));
    static void ApplyAccentResources(Windows.UI.Color c)
    {
        var r = Application.Current.Resources;
        r["SystemAccentColor"] = c;
        r["SystemAccentColorDark1"] = Mix(c, 0, 0, 0, 0.18);
        r["SystemAccentColorDark2"] = Mix(c, 0, 0, 0, 0.36);
        r["SystemAccentColorDark3"] = Mix(c, 0, 0, 0, 0.54);
        r["SystemAccentColorLight1"] = Mix(c, 255, 255, 255, 0.18);
        r["SystemAccentColorLight2"] = Mix(c, 255, 255, 255, 0.36);
        r["SystemAccentColorLight3"] = Mix(c, 255, 255, 255, 0.54);
    }
    void RefreshAccent()
    {
        // theme resources re-resolve on a theme change: flip the element theme and back
        void Flip(FrameworkElement fe)
        {
            var was = fe.RequestedTheme;
            fe.RequestedTheme = was == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            fe.RequestedTheme = was;
        }
        try { Flip(RootGrid); } catch { }
        try { _widget?.RefreshTheme(); } catch { }
        try { _hover.RefreshTheme(); } catch { }
    }
    void SetAccent(Windows.UI.Color c, bool save)
    {
        ApplyAccentResources(c);
        AccentSwatch.Background = new SolidColorBrush(c);
        if (save) { _s.Accent = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; _s.Save(); }
        RefreshAccent();
    }
    void InitAccentUi()
    {
        AccentSwatch.Background = new SolidColorBrush(ParseHex(_s.Accent));
        int n = 0;
        foreach (var hex in AccentSwatches)
        {
            var col = ParseHex(hex);
            var b = new Button { Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(col), Tag = col };
            b.Click += (a, e) => SetAccent((Windows.UI.Color)((Button)a).Tag, true);
            (n++ < 5 ? AccentPresets : AccentPresets2).Children.Add(b);
        }
    }
    async void AccentCustom_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPicker { Color = ParseHex(_s.Accent), IsAlphaEnabled = false, IsMoreButtonVisible = true, IsColorChannelTextInputVisible = true, IsHexInputVisible = true, FlowDirection = FlowDirection.LeftToRight };
        picker.ColorChanged += (s2, a2) => SetAccent(a2.NewColor, true);
        var dlg = new ContentDialog { XamlRoot = RootGrid.XamlRoot, Title = L.T("لون مخصص"), Content = new ScrollViewer { Content = picker, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, CloseButtonText = L.T("تم"), DefaultButton = ContentDialogButton.Close, FlowDirection = L.Flow };
        try { await dlg.ShowAsync(); } catch { }
    }
    void AccentReset_Click(object sender, RoutedEventArgs e)
    {
        SetAccent(DefaultAccent, true);
        _s.Accent = ""; _s.Save();
    }

    // ---------- automatic update check ----------
    Microsoft.UI.Dispatching.DispatcherQueueTimer? _updFirst, _updTimer;
    UpdateToast? _toast;
    void StartUpdateWatch()
    {
        if (App.Preview != null) return;
        _updFirst = DispatcherQueue.CreateTimer();
        _updFirst.Interval = TimeSpan.FromSeconds(45); _updFirst.IsRepeating = false;
        _updFirst.Tick += async (a, b) => await AutoUpdateAsync();
        _updFirst.Start();
        _updTimer = DispatcherQueue.CreateTimer();
        _updTimer.Interval = TimeSpan.FromHours(6);
        _updTimer.Tick += async (a, b) => await AutoUpdateAsync();
        _updTimer.Start();
    }
    async Task AutoUpdateAsync()
    {
        if (!_s.AutoUpdate || _quit || _checkingUpdates) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FluentPrayerTimes/" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"));
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync("https://api.github.com/repos/" + UpdateRepo + "/releases/latest");
            response.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var installed = ReleaseVersion(typeof(MainWindow).Assembly.GetName().Version?.ToString(3)) ?? new Version(0, 0, 0);
            var url = UpdateDownload(json.RootElement, installed);
            if (url == null || _quit) return;
            var tag = (json.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
            if (_s.UpdateSeen == tag) return;   // each new version is announced once
            _s.UpdateSeen = tag; _s.Save();
            _toast = new UpdateToast(tag, url, _s.Theme);
            _toast.ShowToast();
        }
        catch (Exception error)
        {
            try { Directory.CreateDirectory(App.AppData); File.AppendAllText(Path.Combine(App.AppData, "update.log"), DateTime.Now + " auto: " + error.Message + "\n"); } catch { }
        }
    }
    void AutoUpdate_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.AutoUpdate = TglAutoUpdate.IsOn;
        _s.Save();
    }

    // ---------- navigation ----------
    void FitPages()
    {
        double avail = RootGrid.ActualWidth - 48;
        if (avail < 200) avail = 200;
        double w = Math.Max(200, Math.Min(560, avail - 32));
        foreach (var sv in new[] { PageTimes, PageSettings, PageAbout, PageCalendar, PageConvert, PageAzkar }) sv.MaxWidth = avail;
        PanelTimes.Width = w; PanelSettings.Width = w; PanelAbout.Width = w;
        PanelCalendar.Width = w; PanelConvert.Width = w; PanelAzkar.Width = w;
    }

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string tag = (args.SelectedItemContainer?.Tag as string) ?? "times";
        PageTimes.Visibility = tag == "times" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        PageCalendar.Visibility = tag == "calendar" ? Visibility.Visible : Visibility.Collapsed;
        PageConvert.Visibility = tag == "convert" ? Visibility.Visible : Visibility.Collapsed;
        PageAzkar.Visibility = tag == "azkar" ? Visibility.Visible : Visibility.Collapsed;
        FitPages();
        ScrollViewer? shown = tag switch { "settings" => PageSettings, "about" => PageAbout, "calendar" => PageCalendar, "convert" => PageConvert, "azkar" => PageAzkar, _ => PageTimes };
        if (_navReady) SlideIn(shown);
        _navReady = true;
    }

    bool _navReady;
    Microsoft.UI.Xaml.Media.Animation.Storyboard? _navMotion;

    /// <summary>Page switch motion: the old page disappears at once and the new one rises from below with a fade, decelerating (about 300 ms), like WinUI's entrance navigation transition.</summary>
    void SlideIn(FrameworkElement page)
    {
        _navMotion?.Stop();
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) { page.Opacity = 1; page.RenderTransform = new TranslateTransform(); return; }
        var tr = new TranslateTransform(); page.RenderTransform = tr;
        var ease = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut };
        var slide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = 96, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(320)), EasingFunction = ease, EnableDependentAnimation = true };
        var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(220)), EasingFunction = ease };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slide, tr); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slide, "Y");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, page); Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard(); _navMotion = sb;
        page.Opacity = 0;
        sb.Children.Add(slide); sb.Children.Add(fade); sb.Begin();
    }

    static bool TaskbarIsLight()
    {
        try { return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is int v && v == 1; }
        catch { return false; }
    }

    // ---------- times ----------
    void BuildRows()
    {
        for (int i = 0; i < 6; i++)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var n = new TextBlock { Text = Times.Names[i], FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            var t = new TextBlock { Text = "--:--", FontSize = 18, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(n, 0); Grid.SetColumn(t, 1);
            g.Children.Add(n); g.Children.Add(t);
            var b = new Border { Child = g, Padding = new Thickness(14, 6, 14, 6), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            _rowName[i] = n; _rowTime[i] = t; _rowBox[i] = b;
            RowsPanel.Children.Add(b);
        }
    }

    int _reloadGeneration;
    async Task ReloadAsync()
    {
        int generation = ++_reloadGeneration;
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(_s))!;
        _lastTry = DateTime.Now;
        Times.Result r;
        try { r = await Times.Load(snapshot); } catch { return; }
        if (generation != _reloadGeneration) return;
        if (r.Today?.Tz != null) { _s.Location.Tz = r.Today.Tz; _s.Save(); }
        _res = r;
        if (r.Today != null)
        {
            _loadedDay = Times.Now(_s.Location.Tz).Date;
            for (int i = 0; i < 6; i++) _rowTime[i].Text = Times.F12(r.Today.T[Times.Keys[i]]);
        }
        ShowDatesAndCity(r);
        _hl = -2;
        Tick();
    }

    void OnAzkarChanged()
    {
        _azkarNext = _s.AzkarOn ? DateTime.Now.AddMinutes(Math.Max(1, _s.AzkarMinutes)) : DateTime.MaxValue;
    }

    void Adhan_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.NotifyAdhan = TglAdhan.IsOn;
        _s.Save();
    }

    void ShowDatesAndCity(Times.Result r)
    {
        TxtCity.Text = _s.Location.Display;
        var tn = Times.Now(_s.Location.Tz);
        string greg = L.IsArabic ? tn.ToString("dddd d MMMM yyyy", new CultureInfo("ar-EG"))
            : HijriUtil.Days[(int)tn.DayOfWeek] + " " + tn.Day + " " + HijriUtil.GMonths[tn.Month - 1] + " " + tn.Year;
        TxtDates.Text = r.Today != null ? greg + "  -  " + L.LocHijri(r.Today.Hijri) : greg;
        TxtStatus.Text = r.Today == null ? L.T("لا يوجد اتصال ولا مواقيت محفوظة") : (r.Offline ? L.T("دون اتصال - من آخر بيانات محفوظة") : "");
    }

    void Tick()
    {
        if (DateTime.Now >= _azkarNext)
        {
            _azkarNext = DateTime.Now.AddMinutes(Math.Max(1, _s.AzkarMinutes));
            try { _tray.Notify(L.T("منبه الأذكار"), AzkarData.RandomShort().Text); } catch { }
        }
        var now = Times.Now(_s.Location.Tz);
        bool stale = _loadedDay != now.Date || _res?.Today == null;
        if (stale && (now - _lastTry).TotalSeconds > (_res?.Today == null ? 300 : 5)) _ = ReloadAsync();

        if (App.Preview != null) { PreviewTick(now); return; }
        if (_res?.Today == null)
        {
            TxtNextLabel.Text = L.T("الصلاة القادمة");
            TxtNextName.Text = "--"; TxtNextTime.Text = ""; SetMainCount("--:--", "", null, false);
            SetHover(L.T("مواقيت الصلاة"), "--:--", "", _s.Location.Display, null, false);
            PushWidget("--", "", "--:--", L.T("الصلاة القادمة"), null, false);
            return;
        }
        if (_s.NotifyAdhan)
        {
            for (int i = 0; i < Times.Keys.Length; i++)
            {
                if (i == 1) continue; // sunrise is not an adhan
                var pt = Times.At(now, _res.Today.T[Times.Keys[i]]);
                var key = now.ToString("yyyyMMdd") + "-" + i;
                if (now >= pt && (now - pt).TotalSeconds < 90 && key != _lastAdhan)
                {
                    _lastAdhan = key;
                    bool custom = Sounds.Available("adhan", _s.AdhanSound);
                    try { _tray.Notify(L.T("مواقيت الصلاة"), L.T("حان الآن موعد أذان ") + Times.Names[i], custom); } catch { }
                    if (custom) Sounds.Play("adhan", _s.AdhanSound);
                }
                var iq = pt.AddMinutes(_s.IqamaFor(i));
                var ikey = now.ToString("yyyyMMdd") + "-i" + i;
                if (now >= iq && (now - iq).TotalSeconds < 90 && ikey != _lastIqama)
                {
                    _lastIqama = ikey;
                    bool custom = Sounds.Available("iqama", _s.IqamaSound);
                    try { _tray.Notify(L.T("مواقيت الصلاة"), L.T("حان وقت إقامة صلاة ") + Times.Names[i], custom); } catch { }
                    if (custom) Sounds.Play("iqama", _s.IqamaSound);
                }
            }
        }
        var st = Times.State(now, _res.Today.T, _res.Tomorrow?.T, _s);
        if (st == null)
        {
            TxtNextLabel.Text = L.T("الصلاة القادمة");
            TxtNextName.Text = "--"; SetMainCount("--:--", "", null, false);
            SetHover(L.T("مواقيت الصلاة"), "--:--", "", _s.Location.Display, null, false);
            PushWidget("--", "", "--:--", L.T("الصلاة القادمة"), null, false);
            return;
        }
        Render(st, Times.F12(_res.Today.T[Times.Keys[st.Idx]]));
    }

    void Render(Times.Disp d, string adhanTime)
    {
        string nm = Times.Names[d.Idx] + (d.Tomorrow ? L.T(" (غدًا)") : "");
        string tm = d.Time.ToString("h:mm tt", CultureInfo.InvariantCulture);
        string label = L.T("الصلاة القادمة"), count, unit, title; Windows.UI.Color? color = null; bool msg = false;
        switch (d.Phase)
        {
            case Times.Phase.Adhan:
                label = L.T("الصلاة الحالية"); count = L.T("حان وقت الاذان"); unit = ""; color = Green; msg = true;
                title = L.T("موعد صلاة ") + Times.Names[d.Idx]; tm = adhanTime; break;
            case Times.Phase.Iqama:
                label = L.T("الإقامة"); count = Times.FmtMinutes(d.Left); unit = L.T("دقيقة"); color = Amber;
                title = L.T("باقي على إقامة صلاة ") + Times.Names[d.Idx]; break;
            case Times.Phase.IqamaNow:
                label = L.T("الصلاة الحالية"); count = L.T("حان وقت الاقامة"); unit = ""; color = Green; msg = true;
                title = L.T("إقامة صلاة ") + Times.Names[d.Idx]; break;
            default:
                count = Times.FmtHM(d.Left); unit = L.T("ساعة");
                title = d.Idx == 1 ? L.T("باقي على الشروق") : L.T("باقي على صلاة ") + nm; break;
        }
        TxtNextLabel.Text = label;
        TxtNextName.Text = nm; TxtNextTime.Text = tm;
        SetMainCount(count, unit, color, msg);
        PushWidget(nm, tm, count, label, color, msg, unit);
        SetHover(title, count, unit, nm + " " + tm + "  -  " + _s.Location.Display, color, msg);

        int hl = d.Tomorrow ? -1 : d.Idx;
        if (hl != _hl)
        {
            _hl = hl;
            for (int i = 0; i < 6; i++)
            {
                bool on = i == hl;
                _rowBox[i].Background = on ? new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 196, 204)) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                _rowBox[i].BorderBrush = on ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 196, 204)) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                _rowName[i].FontWeight = on ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
                _rowTime[i].FontWeight = _rowName[i].FontWeight;
            }
        }
    }

    int _pt;
    void PreviewTick(DateTime now)
    {
        var at = now.Date.AddHours(15).AddMinutes(30);
        Times.Disp d = App.Preview switch
        {
            "adhan" => new Times.Disp(Times.Phase.Adhan, 3, at, false, TimeSpan.FromSeconds(40)),
            "iqama" => new Times.Disp(Times.Phase.Iqama, 3, at.AddMinutes(20), false, TimeSpan.FromSeconds(12 * 60 + 34)),
            "iqamanow" => new Times.Disp(Times.Phase.IqamaNow, 3, at.AddMinutes(20), false, TimeSpan.Zero),
            _ => new Times.Disp(Times.Phase.Next, 3, at, false, TimeSpan.FromMinutes(125)),
        };
        Render(d, "3:30 PM");
        _pt++;
        string part = App.PreviewPart ?? "main";
        if (_pt == 1)
        {
            if (part == "main") ShowFlyout(false);
            if (part == "widget") SetWidget(true);
            if (part == "calculation")
            {
                Nav.SelectedItem = Nav.FooterMenuItems[0];
                ShowFlyout(false);
            }
            if (part == "settings")
            {
                SetAccent(ParseHex("#C239B3"), false);   // CI preview: show a non-default accent
                Nav.SelectedItem = Nav.FooterMenuItems[0];
                ExpAccent.IsExpanded = true;
                ShowFlyout(false);
            }
            if (part == "calendar")
            {
                foreach (var m in Nav.MenuItems) if (m is NavigationViewItem ni && (ni.Tag as string) == "calendar") Nav.SelectedItem = ni;
                ShowFlyout(false);
            }
            if (part == "about")
            {
                Nav.SelectedItem = Nav.FooterMenuItems[1];
                ShowFlyout(false);
            }
            if (part == "picker")
            {
                SetAccent(ParseHex("#C239B3"), false);
                Nav.SelectedItem = Nav.FooterMenuItems[0];
                ShowFlyout(false);
            }
            if (part == "update") { _toast = new UpdateToast("1.2.5", "https://github.com/" + UpdateRepo + "/releases/latest", _s.Theme); _toast.ShowToast(); }
        }
        if (_pt == 3 && part == "calculation") CalculationCard.StartBringIntoView();
        if (_pt == 3 && part == "picker") AccentCustom_Click(this, new RoutedEventArgs());
        if (_pt == 2 && (part == "hover" || part == "hoverstress"))
        {
            var wa = DisplayArea.Primary.WorkArea;
            _hover.Pinned = true;
            _hover.Update(_hTitle, _hCount, _hUnit, _hSub, _hColor, _hMsg);
            _hover.ShowAt(wa.X + wa.Width / 2, wa.Y + wa.Height - 10, _s.Theme);
        }
        if (part == "main" && _pt == 4)
        {
            try
            {
                long ex = GetWindowLongPtr(_hwnd, -20).ToInt64();
                File.AppendAllText(Path.Combine(App.AppData, "preview.log"), $"main toolwindow={(ex & 0x80) != 0} appwindow={(ex & 0x40000) != 0} visible={AppWindow.IsVisible} opacity={RootGrid.Opacity:F2}\n");
            }
            catch { }
        }
        if (part == "hoverstress" && _pt > 3)
        {
            // show/hide the hover card repeatedly and log any frame where it came up with the wrong size or no layout
            var log = Path.Combine(App.AppData, "hoverstress.log");
            if (_pt % 2 == 0) _hover.Hide();
            else
            {
                var wa = DisplayArea.Primary.WorkArea;
                _hover.Update(_hTitle, _hCount, _hUnit, _hSub, _hColor, _hMsg);
                _hover.ShowAt(wa.X + wa.Width / 2, wa.Y + wa.Height - 10, _s.Theme);
            }
            try { File.AppendAllText(log, _pt + " " + _hover.Diag() + "\n"); } catch { }
        }
    }

    void SetMainCount(string count, string unit, Windows.UI.Color? color, bool msg)
    {
        TxtCountdown.Text = count; TxtUnit.Text = unit;
        TxtCountdown.Foreground = color.HasValue ? new SolidColorBrush(color.Value) : _countBrush;
        TxtCountdown.FontSize = msg ? 28 : 40;
    }

    void SetHover(string title, string count, string unit, string sub, Windows.UI.Color? color, bool msg)
    {
        _hTitle = title; _hCount = count; _hUnit = unit; _hSub = sub; _hColor = color; _hMsg = msg;
        if (_hover.Shown) _hover.Update(title, count, unit, sub, color, msg);
    }

    void PushWidget(string n, string t, string c, string header, Windows.UI.Color? color, bool msg, string unit = "")
    {
        _wName = n; _wTime = t; _wCount = c; _wUnit = unit; _wHeader = header; _wColor = color; _wMsg = msg;
        _widget?.SetInfo(n, t, c, header, color, msg, unit);
    }

    // ---------- settings ----------
    void LoadSettingsUi()
    {
        FillLangCombo();
        FillLocationCombos();
        TglAdhan.IsOn = _s.NotifyAdhan;
        BuildIqamaAndSounds();
        LoadCalculationUi();
        SyncCitySelection();
        TxtLat.Text = _s.Location.Lat.ToString("F4", CultureInfo.InvariantCulture);
        TxtLon.Text = _s.Location.Lon.ToString("F4", CultureInfo.InvariantCulture);
        (_s.Backdrop switch { "mica" => RbMica, "acrylic" => RbAcrylic, _ => RbMicaAlt }).IsChecked = true;
        (_s.Theme switch { "light" => RbThemeLight, "dark" => RbThemeDark, _ => RbThemeSys }).IsChecked = true;
        (_s.Dst switch { "off" => RbDstOff, "on" => RbDstOn, _ => RbDstAuto }).IsChecked = true;
        TglStartup.IsOn = _s.RunAtStartup;
        TglWidget.IsOn = _s.WidgetVisible;
        TglAutoUpdate.IsOn = _s.AutoUpdate;
        InitAccentUi();
    }

    void FillLocationCombos()
    {
        bool was = _loading; _loading = true;
        CmbCountry.Items.Clear(); CmbCity.Items.Clear();
        CmbCountry.Items.Add(L.T("كل دول العالم"));
        foreach (var c in Countries.All) CmbCountry.Items.Add(L.CountryName(c.Cc, c.Ar));
        foreach (var c in Cities.All) CmbCity.Items.Add(L.T(c.Ar));
        _loading = was;
    }

    void FillLangCombo()
    {
        bool was = _loading; _loading = true;
        CmbLang.Items.Clear();
        CmbLang.Items.Add("Auto / تلقائي");
        foreach (var n in L.Native) CmbLang.Items.Add(n);
        CmbLang.SelectedIndex = string.IsNullOrEmpty(_s.Lang) ? 0 : Math.Max(0, Array.IndexOf(L.Codes, _s.Lang) + 1);
        _loading = was;
    }

    void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbLang.SelectedIndex < 0) return;
        _s.Lang = CmbLang.SelectedIndex == 0 ? "" : L.Codes[CmbLang.SelectedIndex - 1];
        _s.Save();
        ApplyLanguage();
    }

    void ApplyFlow()
    {
        var fd = L.Flow;
        try
        {
            // mirror the window frame like the sidebar: caption (close) button moves to the left in RTL languages
            var hw = WinRT.Interop.WindowNative.GetWindowHandle(this);
            long ex = GetWindowLongPtr(hw, -20).ToInt64();
            ex = L.Rtl ? (ex | 0x00400000L) : (ex & ~0x00400000L);
            SetWindowLongPtr(hw, -20, new IntPtr(ex));
        }
        catch { }
        TitleStack.HorizontalAlignment = L.Rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        TitleStack.Margin = L.Rtl ? new Thickness(0, 0, 14, 0) : new Thickness(14, 0, 0, 0);
        TitleStack.FlowDirection = fd;
        foreach (FrameworkElement el in new FrameworkElement[] { Nav, PanelTimes, PanelCalendar, PanelConvert, PanelAzkar, PanelSettings, PanelAbout, BtnAccentCustom, BtnAccentReset })
            el.FlowDirection = fd;
    }

    public void ApplyLanguage()
    {
        L.Set(_s.Lang);
        ApplyFlow(); L.Refresh();
        Title = L.T("مواقيت الصلاة - Fluent Prayer Times");
        try { var v = typeof(App).Assembly.GetName().Version; if (v != null) TxtVersion.Text = L.T("الإصدار ") + v.Major + "." + v.Minor + "." + v.Build; } catch { }
        bool was = _loading; _loading = true;
        FillLocationCombos(); SyncCitySelection(); LoadCalculationUi();
        PanelCalendar.Children.Clear(); PanelCalendar.Children.Add(new CalendarPage());
        PanelConvert.Children.Clear(); PanelConvert.Children.Add(new ConverterPage());
        PanelAzkar.Children.Clear(); PanelAzkar.Children.Add(new AzkarPage(_s, OnAzkarChanged));
        BuildIqamaAndSounds();
        _loading = was;
        _widget?.ApplyLang();
        if (_res != null) ShowDatesAndCity(_res);
        _hl = -2;
        try { Tick(); } catch { }
    }

    void SyncCitySelection()
    {
        bool was = _loading; _loading = true;
        int i = _s.Location.UseCity ? Array.FindIndex(Cities.All, c => c.Name == _s.Location.Name) : -1;
        CmbCity.SelectedIndex = i;
        int ci = Array.FindIndex(Countries.All, c => c.Cc == _s.Location.Cc);
        CmbCountry.SelectedIndex = ci < 0 ? 0 : ci + 1;
        if (i < 0) CmbCity.PlaceholderText = L.T(_s.Location.NameAr);
        TxtLat.Text = _s.Location.Lat.ToString("F4", CultureInfo.InvariantCulture);
        TxtLon.Text = _s.Location.Lon.ToString("F4", CultureInfo.InvariantCulture);
        _loading = was;
    }

    void LocationChanged()
    {
        // Automatic choices follow each new location; explicit overrides stay unchanged.
        LoadCalculationUi();
        _s.Save();
        SyncCitySelection();
        _ = ReloadAsync();
    }

    void Country_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbCountry.SelectedIndex <= 0) return;
        var k = Countries.All[CmbCountry.SelectedIndex - 1];
        if (k.Cc == _s.Location.Cc && !_s.Location.UseCity && false) return;
        if (k.Cc == "EG") _s.Location = new Loc { Name = "Cairo", NameAr = "القاهرة", Lat = 30.0444, Lon = 31.2357, UseCity = true };
        else _s.Location = new Loc { Name = k.Capital, NameAr = k.Capital, Lat = k.Lat, Lon = k.Lon, UseCity = false, CountryAr = k.Ar, Cc = k.Cc };
        LocationChanged();
    }

    void City_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbCity.SelectedIndex < 0) return;
        var c = Cities.All[CmbCity.SelectedIndex];
        _s.Location = new Loc { Name = c.Name, NameAr = c.Ar, Lat = c.Lat, Lon = c.Lon, UseCity = true, CountryAr = "مصر", Cc = "EG" };
        LocationChanged();
    }

    void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) Search_Click(sender, e);
    }

    async void Search_Click(object sender, RoutedEventArgs e)
    {
        string q = TxtSearch.Text.Trim();
        if (q.Length < 2) return;
        try
        {
            string? cc = CmbCountry.SelectedIndex > 0 ? Countries.All[CmbCountry.SelectedIndex - 1].Cc : null;
            _places = await Times.Search(q, cc);
            _loading = true;
            LstPlaces.ItemsSource = _places.Select(p => p.Label).ToList();
            _loading = false;
            LstPlaces.Visibility = Visibility.Visible;
            if (_places.Count == 0) TxtDetect.Text = L.T("لا توجد نتائج");
        }
        catch { TxtDetect.Text = L.T("فشل البحث - تأكد من الاتصال بالإنترنت"); }
    }

    void Place_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LstPlaces.SelectedIndex < 0 || LstPlaces.SelectedIndex >= _places.Count) return;
        var p = _places[LstPlaces.SelectedIndex];
        _s.Location = new Loc { Name = p.Name, NameAr = p.Name, Lat = p.Lat, Lon = p.Lon, UseCity = false, CountryAr = p.Country, Cc = p.Cc };
        LstPlaces.Visibility = Visibility.Collapsed;
        LocationChanged();
    }

    async void Detect_Click(object sender, RoutedEventArgs e)
    {
        TxtDetect.Text = L.T("جارٍ تحديد موقعك...");
        var (p, how) = await Times.Detect();
        if (p == null) { TxtDetect.Text = L.T("تعذّر تحديد الموقع"); return; }
        _s.Location = new Loc { Name = p.Name, NameAr = p.Name, Lat = p.Lat, Lon = p.Lon, UseCity = false, CountryAr = p.Country, Cc = p.Cc };
        TxtDetect.Text = L.T("تم: ") + how;
        LocationChanged();
    }

    async void Coords_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtLat.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var la) &&
            double.TryParse(TxtLon.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) &&
            la >= -90 && la <= 90 && lo >= -180 && lo <= 180)
        {
            var rv = await Times.Reverse(la, lo);
            string nm = rv != null && rv.Value.city != "" ? rv.Value.city : L.T("إحداثيات") + $" ({la:F3}, {lo:F3})";
            _s.Location = new Loc { Name = nm, NameAr = nm, Lat = la, Lon = lo, UseCity = false, CountryAr = rv?.country ?? "", Cc = rv?.cc ?? "" };
            LocationChanged();
        }
        else TxtDetect.Text = L.T("إحداثيات غير صحيحة");
    }

    void Dst_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.Dst = (string)((RadioButton)sender).Tag;
        _s.Save();
        _ = ReloadAsync();
    }

    void Backdrop_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.Backdrop = (string)((RadioButton)sender).Tag;
        _s.Save();
        ApplyAppearance();
    }

    void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.Theme = (string)((RadioButton)sender).Tag;
        _s.Save();
        ApplyAppearance();
    }

    // ---------- iqama + sounds ----------
    static readonly string[] SoundTags = { "default", "short", "full" };
    void BuildIqamaAndSounds()
    {
        PanelIqama.Children.Clear();
        int[] order = { 0, 2, 3, 4, 5 };
        foreach (int i in order)
        {
            int idx = i;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = Times.Names[i], VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
            var nb = new NumberBox { Minimum = 2, Maximum = 180, Value = _s.IqamaFor(i), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 1, LargeChange = 5, Width = 140, FlowDirection = FlowDirection.LeftToRight, Header = null };
            nb.ValueChanged += (s2, a2) =>
            {
                if (_loading) return;
                double v = double.IsNaN(a2.NewValue) ? _s.IqamaFor(idx) : a2.NewValue;
                var arr = (_s.IqamaMin != null && _s.IqamaMin.Length >= 6) ? _s.IqamaMin : new[] { 20, 0, 20, 20, 15, 20 };
                arr[idx] = (int)Math.Clamp(Math.Round(v), 2, 180); _s.IqamaMin = arr; _s.Save();
            };
            Grid.SetColumn(name, 0); Grid.SetColumn(nb, 1);
            row.Children.Add(name); row.Children.Add(nb);
            PanelIqama.Children.Add(row);
        }
        PanelSounds.Children.Clear();
        PanelSounds.Children.Add(SoundRow(L.T("صوت الأذان"), "adhan", new[] { L.T("الافتراضي (صوت الإشعار)"), L.T("أذان مختصر"), L.T("أذان كامل") }, _s.AdhanSound, v => { _s.AdhanSound = v; _s.Save(); }));
        PanelSounds.Children.Add(SoundRow(L.T("صوت الإقامة"), "iqama", new[] { L.T("الافتراضي (صوت الإشعار)"), L.T("إقامة مختصرة"), L.T("إقامة كاملة") }, _s.IqamaSound, v => { _s.IqamaSound = v; _s.Save(); }));
    }

    UIElement SoundRow(string title, string kind, string[] labels, string cur, Action<string> save)
    {
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cmb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var l in labels) cmb.Items.Add(l);
        cmb.SelectedIndex = Math.Max(0, Array.IndexOf(SoundTags, cur));
        var test = new Button { Content = L.T("تجربة") };
        var note = HijriUtil.Sec(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });
        cmb.SelectionChanged += (s2, e2) => { if (_loading || cmb.SelectedIndex < 0) return; save(SoundTags[cmb.SelectedIndex]); note.Text = ""; };
        test.Click += (s2, e2) =>
        {
            var mode = SoundTags[Math.Max(0, cmb.SelectedIndex)];
            if (mode == "default") { note.Text = L.T("الصوت الافتراضي هو صوت إشعار ويندوز."); return; }
            note.Text = Sounds.Play(kind, mode) ? "" : L.T("ملف الصوت غير موجود بعد في مجلد Audio.");
        };
        Grid.SetColumn(cmb, 0); Grid.SetColumn(test, 1);
        line.Children.Add(cmb); line.Children.Add(test);
        box.Children.Add(line); box.Children.Add(note);
        return box;
    }

    // ---------- updates (same mechanism as Fluent Vantage Toolbar) ----------
    const string UpdateRepo = "MohamedElnaggar00/fluent-prayer-times";
    static Version? ReleaseVersion(string? tag) => Version.TryParse(tag?.TrimStart('v', 'V'), out var v) ? v : null;
    internal static string? UpdateDownload(System.Text.Json.JsonElement release, Version installed)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var next = ReleaseVersion(release.GetProperty("tag_name").GetString());
        if (next == null || next <= installed) return null;
        // The running app already needs the .NET 8 runtime, so the small runtime-dependent installer is preferred; the self-contained one is the fallback.
        foreach (var suffix in new[] { "installer-.NET-runtime-dependent-win-x64.exe", "installer-win-x64.exe" })
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name != $"FluentPrayerTimes-v{next.ToString(3)}-{suffix}") continue;
                var url = asset.GetProperty("browser_download_url").GetString();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/" + UpdateRepo + "/releases/download/")) return url;
            }
        throw new InvalidOperationException("New release has no supported installer.");
    }

    void Update_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdatesAsync();

    async Task CheckForUpdatesAsync()
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        BtnUpdate.IsEnabled = false; BtnUpdate.Content = L.T("جارٍ التحقق...");
        TxtUpdate.Text = "";
        string message;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FluentPrayerTimes/" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"));
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync("https://api.github.com/repos/" + UpdateRepo + "/releases/latest");
            response.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var installed = ReleaseVersion(typeof(MainWindow).Assembly.GetName().Version?.ToString(3)) ?? new Version(0, 0, 0);
            var url = UpdateDownload(json.RootElement, installed);
            if (url == null) message = L.T("أنت تستخدم أحدث إصدار.");
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                message = L.T("تم فتح رابط تنزيل التحديث في المتصفح. بعد اكتمال التنزيل، شغّل المثبّت للتحديث.");
            }
        }
        catch (Exception error)
        {
            try { Directory.CreateDirectory(App.AppData); File.AppendAllText(Path.Combine(App.AppData, "update.log"), DateTime.Now + " " + error.Message + "\n"); } catch { }
            message = L.T("تعذّر التحقق من التحديثات أو فتح التنزيل. تحقق من اتصال الإنترنت وحاول مجدداً.");
        }
        finally { _checkingUpdates = false; BtnUpdate.IsEnabled = true; BtnUpdate.Content = L.T("التحقق من التحديثات"); }
        if (_quit) return;
        TxtUpdate.Text = message;
        try { await new ContentDialog { XamlRoot = RootGrid.XamlRoot, Title = L.T("التحقق من التحديثات"), Content = message, CloseButtonText = L.T("إغلاق"), FlowDirection = L.Flow }.ShowAsync(); } catch { }
    }

    void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.RunAtStartup = TglStartup.IsOn;
        _s.Save();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (_s.RunAtStartup) k?.SetValue("FluentPrayerTimes", "\"" + Environment.ProcessPath + "\" --tray");
            else k?.DeleteValue("FluentPrayerTimes", false);
        }
        catch { }
    }
}


/// <summary>Borderless Windows 11 style popup menu, placed right above the tray icon.</summary>
sealed class TrayMenuWindow : Window
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

    readonly Border _root = new();
    readonly StackPanel _list = new() { Spacing = 2 };
    readonly IntPtr _hwnd;
    readonly Action<int> _onCmd;

    public TrayMenuWindow(Action<int> onCmd)
    {
        _onCmd = onCmd;
        _root.Padding = new Thickness(4);
        _root.Child = _list;
        Content = _root;
        Title = "Fluent Prayer Times Menu";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false; p.IsMaximizable = false; p.IsMinimizable = false; p.IsAlwaysOnTop = true;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        try { SystemBackdrop = new DesktopAcrylicBackdrop(); } catch { }
        AppWindow.Closing += (a, e) => { e.Cancel = true; a.Hide(); };
        Activated += (s, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) AppWindow.Hide(); };
        long ex = (long)GetWindowLongPtr(_hwnd, -20);
        SetWindowLongPtr(_hwnd, -20, (IntPtr)(ex | 0x80));
        WindowChrome.Apply(_hwnd);
    }

    void AddItem(string text, string glyph, int cmd, bool check = false)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ic = new FontIcon { Glyph = glyph, FontSize = 16 };
        var tb = new TextBlock { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tb, 1);
        g.Children.Add(ic); g.Children.Add(tb);
        if (check)
        {
            var ck = new FontIcon { Glyph = "\uE73E", FontSize = 14 };
            Grid.SetColumn(ck, 2);
            g.Children.Add(ck);
        }
        var b = new Button
        {
            Content = g, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 0, 12, 0), Height = 36, CornerRadius = new CornerRadius(4),
        };
        b.Click += (s, e) => { AppWindow.Hide(); _onCmd(cmd); };
        _list.Children.Add(b);
    }

    public void ShowAt(int ax, int ay, string theme, bool widgetOn)
    {
        var wa = DisplayArea.GetFromPoint(new PointInt32(ax, ay), DisplayAreaFallback.Nearest).WorkArea;
        _root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        _root.FlowDirection = L.Flow;
        _list.Children.Clear();
        AddItem(L.T("فتح"), "\uE8A7", 1);
        AddItem(L.T("ويدجت سطح المكتب"), "\uE8A1", 4, widgetOn);
        AddItem(L.T("الإعدادات"), "\uE713", 2);
        AddItem(L.T("التحقق من التحديثات"), "\uE895", 5);
        _list.Children.Add(new Border { Height = 1, Margin = new Thickness(4, 3, 4, 3), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        AddItem(L.T("خروج"), "\uE7E8", 3);

        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(230 * dpi), h = (int)((5 * 36 + 4 * 2 + 7 + 8) * dpi);
        int x = Math.Clamp(ax - w / 2, wa.X + 8, wa.X + wa.Width - w - 8);
        int y = ay > wa.Y + wa.Height / 2 ? wa.Y + wa.Height - h - 8 : wa.Y + 8;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
    }
}
