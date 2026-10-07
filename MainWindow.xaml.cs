using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Windows.Graphics;

namespace FluentPrayerTimes;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CurPt p);
    [StructLayout(LayoutKind.Sequential)] struct CurPt { public int X, Y; }

    const int CmdOpen = 1, CmdSettings = 2, CmdExit = 3, CmdWidget = 4;
    WidgetWindow? _widget;
    string _wName = "--", _wTime = "", _wCount = "--:--:--";

    readonly Settings _s = Settings.Load();
    readonly IntPtr _hwnd;
    readonly TrayIcon _tray;
    readonly TrayMenuWindow _menu;
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

    public MainWindow(bool startHidden)
    {
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "مواقيت الصلاة - Fluent Prayer Times";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);

        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        AppWindow.Resize(new SizeInt32((int)(470 * dpi), (int)(610 * dpi)));
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }
        AppWindow.Closing += (s, a) => { if (!_quit) { a.Cancel = true; s.Hide(); } };

        try
        {
            ImgLogo.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "tray.png")));
            var v = typeof(App).Assembly.GetName().Version;
            if (v != null) TxtVersion.Text = "الإصدار " + v.Major + "." + v.Minor + "." + v.Build;
        }
        catch { }
        BuildRows();
        LoadSettingsUi();
        ApplyAppearance();
        Nav.SelectedItem = Nav.MenuItems[0];
        _loading = false;

        _menu = new TrayMenuWindow(OnMenu);
        _tray = new TrayIcon(_hwnd, Path.Combine(AppContext.BaseDirectory, "app.ico"),
            new[] { (CmdOpen, "فتح"), (CmdWidget, "ويدجت سطح المكتب"), (CmdSettings, "الإعدادات"), (CmdExit, "خروج") },
            ToggleFlyout, OnMenu, OnContext);

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) => Tick();
        _timer.Start();
        _ = ReloadAsync();

        if (_s.WidgetVisible) SetWidget(true);
        if (!startHidden) ShowFlyout(false);
    }

    // ---------- window / tray ----------
    bool OnContext(int x, int y)
    {
        try { _menu.ShowAt(x, y, _s.Theme, _s.WidgetVisible); return true; }
        catch { return false; }
    }

    void OnMenu(int cmd)
    {
        if (cmd == CmdOpen) ShowFlyout(false);
        else if (cmd == CmdSettings) { Nav.SelectedItem = Nav.FooterMenuItems[0]; ShowFlyout(false); }
        else if (cmd == CmdWidget) SetWidget(!_s.WidgetVisible);
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
            _widget.SetInfo(_wName, _wTime, _wCount);
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

    void Quit()
    {
        _quit = true;
        _timer.Stop();
        _tray.Dispose();
        Application.Current.Exit();
        Environment.Exit(0);
    }

    void ToggleFlyout()
    {
        bool minimized = (AppWindow.Presenter as OverlappedPresenter)?.State == OverlappedPresenterState.Minimized;
        if (AppWindow.IsVisible && !minimized) AppWindow.Hide();
        else ShowFlyout(true);
    }

    void ShowFlyout(bool nearCursor)
    {
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        Place(nearCursor);
        AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
    }

    void Place(bool nearCursor)
    {
        try
        {
            int cx = 0, cy = 0;
            if (nearCursor && GetCursorPos(out var c)) { cx = c.X; cy = c.Y; }
            var area = nearCursor
                ? DisplayArea.GetFromPoint(new PointInt32(cx, cy), DisplayAreaFallback.Nearest)
                : DisplayArea.Primary;
            var wa = area.WorkArea;
            int w = AppWindow.Size.Width, h = AppWindow.Size.Height;
            if (h > wa.Height - 16) h = wa.Height - 16;
            if (w > wa.Width - 16) w = wa.Width - 16;
            int x, y;
            if (nearCursor)
            {
                x = Math.Clamp(cx - w / 2, wa.X + 8, wa.X + wa.Width - w - 8);
                y = cy > wa.Y + wa.Height / 2 ? wa.Y + wa.Height - h - 8 : wa.Y + 8;
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

    // ---------- navigation ----------
    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string tag = (args.SelectedItemContainer?.Tag as string) ?? "times";
        PageTimes.Visibility = tag == "times" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
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

    async Task ReloadAsync()
    {
        _lastTry = DateTime.Now;
        Times.Result r;
        try { r = await Times.Load(_s); } catch { return; }
        _res = r;
        if (r.Today != null)
        {
            _loadedDay = DateTime.Now.Date;
            for (int i = 0; i < 6; i++) _rowTime[i].Text = Times.F12(r.Today.T[Times.Keys[i]]);
        }
        TxtCity.Text = _s.Location.NameAr;
        var ar = new CultureInfo("ar-EG");
        string greg = DateTime.Now.ToString("dddd d MMMM yyyy", ar);
        TxtDates.Text = r.Today != null ? greg + "  -  " + r.Today.Hijri : greg;
        TxtStatus.Text = r.Today == null ? "مفيش اتصال ومفيش مواقيت محفوظة" : (r.Offline ? "أوفلاين - من آخر بيانات محفوظة" : "");
        _hl = -2;
        Tick();
    }

    void Tick()
    {
        var now = DateTime.Now;
        bool stale = _loadedDay != now.Date || _res?.Today == null;
        if (stale && (now - _lastTry).TotalSeconds > (_res?.Today == null ? 300 : 5)) _ = ReloadAsync();

        if (_res?.Today == null)
        {
            TxtNextName.Text = "--"; TxtNextTime.Text = ""; TxtCountdown.Text = "--:--:--";
            _tray.SetTip("مواقيت الصلاة");
            PushWidget("--", "", "--:--:--");
            return;
        }
        var nx = Times.Next(now, _res.Today.T, _res.Tomorrow?.T);
        if (nx == null) { TxtNextName.Text = "--"; TxtCountdown.Text = "--:--:--"; _tray.SetTip("مواقيت الصلاة"); return; }
        var (idx, time, tomorrow) = nx.Value;
        var left = time - now;
        TxtNextName.Text = Times.Names[idx] + (tomorrow ? " (غدًا)" : "");
        TxtNextTime.Text = time.ToString("h:mm tt", CultureInfo.InvariantCulture);
        TxtCountdown.Text = Times.Fmt(left);
        PushWidget(TxtNextName.Text, TxtNextTime.Text, TxtCountdown.Text);
        _tray.SetTip((idx == 1 ? "باقي على الشروق: " : "باقي على صلاة " + Times.Names[idx] + ": ") + Times.Fmt(left));

        int hl = tomorrow ? -1 : idx;
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

    void PushWidget(string n, string t, string c)
    {
        _wName = n; _wTime = t; _wCount = c;
        _widget?.SetInfo(n, t, c);
    }

    // ---------- settings ----------
    void LoadSettingsUi()
    {
        foreach (var c in Cities.All) CmbCity.Items.Add(c.Ar);
        SyncCitySelection();
        TxtLat.Text = _s.Location.Lat.ToString("F4", CultureInfo.InvariantCulture);
        TxtLon.Text = _s.Location.Lon.ToString("F4", CultureInfo.InvariantCulture);
        (_s.Backdrop switch { "mica" => RbMica, "acrylic" => RbAcrylic, _ => RbMicaAlt }).IsChecked = true;
        (_s.Theme switch { "light" => RbThemeLight, "dark" => RbThemeDark, _ => RbThemeSys }).IsChecked = true;
        (_s.Dst switch { "off" => RbDstOff, "on" => RbDstOn, _ => RbDstAuto }).IsChecked = true;
        TglStartup.IsOn = _s.RunAtStartup;
        TglWidget.IsOn = _s.WidgetVisible;
    }

    void SyncCitySelection()
    {
        bool was = _loading; _loading = true;
        int i = _s.Location.UseCity ? Array.FindIndex(Cities.All, c => c.Name == _s.Location.Name) : -1;
        CmbCity.SelectedIndex = i;
        if (i < 0) CmbCity.PlaceholderText = _s.Location.NameAr;
        TxtLat.Text = _s.Location.Lat.ToString("F4", CultureInfo.InvariantCulture);
        TxtLon.Text = _s.Location.Lon.ToString("F4", CultureInfo.InvariantCulture);
        _loading = was;
    }

    void LocationChanged()
    {
        _s.Save();
        SyncCitySelection();
        _ = ReloadAsync();
    }

    void City_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbCity.SelectedIndex < 0) return;
        var c = Cities.All[CmbCity.SelectedIndex];
        _s.Location = new Loc { Name = c.Name, NameAr = c.Ar, Lat = c.Lat, Lon = c.Lon, UseCity = true };
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
            _places = await Times.Search(q);
            _loading = true;
            LstPlaces.ItemsSource = _places.Select(p => p.Label).ToList();
            _loading = false;
            LstPlaces.Visibility = Visibility.Visible;
            if (_places.Count == 0) TxtDetect.Text = "مفيش نتائج";
        }
        catch { TxtDetect.Text = "فشل البحث - تأكد من الاتصال"; }
    }

    void Place_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LstPlaces.SelectedIndex < 0 || LstPlaces.SelectedIndex >= _places.Count) return;
        var p = _places[LstPlaces.SelectedIndex];
        _s.Location = new Loc { Name = p.Label, NameAr = p.Label, Lat = p.Lat, Lon = p.Lon, UseCity = false };
        LstPlaces.Visibility = Visibility.Collapsed;
        LocationChanged();
    }

    async void Detect_Click(object sender, RoutedEventArgs e)
    {
        TxtDetect.Text = "بحدد موقعك...";
        var (p, how) = await Times.Detect();
        if (p == null) { TxtDetect.Text = "ماقدرتش أحدد الموقع"; return; }
        _s.Location = new Loc { Name = p.Label, NameAr = p.Label, Lat = p.Lat, Lon = p.Lon, UseCity = false };
        TxtDetect.Text = "تم: " + how;
        LocationChanged();
    }

    void Coords_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtLat.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var la) &&
            double.TryParse(TxtLon.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) &&
            la >= -90 && la <= 90 && lo >= -180 && lo <= 180)
        {
            _s.Location = new Loc { Name = "إحداثيات", NameAr = $"إحداثيات ({la:F3}, {lo:F3})", Lat = la, Lon = lo, UseCity = false };
            LocationChanged();
        }
        else TxtDetect.Text = "إحداثيات غير صحيحة";
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


/// <summary>Invisible 1px host window that shows a modern Windows 11 MenuFlyout at the tray icon.</summary>
sealed class TrayMenuWindow : Window
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);

    readonly Grid _root = new();
    readonly IntPtr _hwnd;
    readonly Action<int> _onCmd;

    public TrayMenuWindow(Action<int> onCmd)
    {
        _onCmd = onCmd;
        Content = _root;
        Title = "Fluent Prayer Times Menu";
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false; p.IsMaximizable = false; p.IsMinimizable = false; p.IsAlwaysOnTop = true;
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        AppWindow.Closing += (a, e) => { e.Cancel = true; a.Hide(); };
        // tool window + almost fully transparent so only the flyout is visible
        long ex = (long)GetWindowLongPtr(_hwnd, -20);
        SetWindowLongPtr(_hwnd, -20, (IntPtr)(ex | 0x80 | 0x80000));
        SetLayeredWindowAttributes(_hwnd, 0, 1, 2);
    }

    public void ShowAt(int x, int y, string theme, bool widgetOn)
    {
        var wa = DisplayArea.GetFromPoint(new PointInt32(x, y), DisplayAreaFallback.Nearest).WorkArea;
        bool lower = y > wa.Y + wa.Height / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, 1, 1));
        _root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        _root.FlowDirection = FlowDirection.RightToLeft;

        var f = new MenuFlyout { ShouldConstrainToRootBounds = false };
        void Add(string text, string glyph, int cmd)
        {
            var it = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
            it.Click += (s, e) => _onCmd(cmd);
            f.Items.Add(it);
        }
        Add("فتح", "\uE8A7", 1);
        var w = new ToggleMenuFlyoutItem { Text = "ويدجت سطح المكتب", IsChecked = widgetOn };
        w.Click += (s, e) => _onCmd(4);
        f.Items.Add(w);
        Add("الإعدادات", "\uE713", 2);
        f.Items.Add(new MenuFlyoutSeparator());
        Add("خروج", "\uE7E8", 3);
        f.Closed += (s, e) => AppWindow.Hide();

        AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
        f.ShowAt(_root, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(0, 0),
            Placement = lower ? Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedRight
                              : Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
        });
    }
}
