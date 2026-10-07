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
    readonly HoverWindow _hover;
    string _hTitle = "", _hCount = "--:--:--", _hUnit = "", _hSub = "";
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
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "مواقيت الصلاة - Fluent Prayer Times";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);

        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        AppWindow.Resize(new SizeInt32((int)(470 * dpi), (int)(610 * dpi)));
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }
        AppWindow.Closing += (s, a) => { if (!_quit) { a.Cancel = true; NativeMotion.Window(_hwnd, false); } };

        try
        {
            ImgTitle.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "tray.png")));
            ImgLogo.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "tray.png")));
            var v = typeof(App).Assembly.GetName().Version;
            if (v != null) TxtVersion.Text = "الإصدار " + v.Major + "." + v.Minor + "." + v.Build;
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
        _tray = new TrayIcon(_hwnd, Path.Combine(AppContext.BaseDirectory, "app.ico"),
            new[] { (CmdOpen, "فتح"), (CmdWidget, "ويدجت سطح المكتب"), (CmdSettings, "الإعدادات"), (CmdExit, "خروج") },
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
            _hover.Update(_hTitle, _hCount, _hUnit, _hSub);
            _hover.ShowAt(hx, hy, _s.Theme);
        });

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
        _hover.Hide();
        _timer.Stop();
        _tray.Dispose();
        Application.Current.Exit();
        Environment.Exit(0);
    }

    void ToggleFlyout()
    {
        _hover.Hide();
        bool minimized = (AppWindow.Presenter as OverlappedPresenter)?.State == OverlappedPresenterState.Minimized;
        if (AppWindow.IsVisible && !minimized) NativeMotion.Window(_hwnd, false);
        else ShowFlyout(true);
    }

    void ShowFlyout(bool nearCursor)
    {
        if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized) p.Restore();
        Place(nearCursor);
        WindowChrome.Apply(_hwnd);
        if (!AppWindow.IsVisible) NativeMotion.Window(_hwnd, true);
        Activate();
        SetForegroundWindow(_hwnd);
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
            _loadedDay = Times.Now(_s.Location.Tz).Date;
            for (int i = 0; i < 6; i++) _rowTime[i].Text = Times.F12(r.Today.T[Times.Keys[i]]);
        }
        TxtCity.Text = _s.Location.Display;
        var ar = new CultureInfo("ar-EG");
        string greg = Times.Now(_s.Location.Tz).ToString("dddd d MMMM yyyy", ar);
        TxtDates.Text = r.Today != null ? greg + "  -  " + r.Today.Hijri : greg;
        TxtStatus.Text = r.Today == null ? "لا يوجد اتصال ولا مواقيت محفوظة" : (r.Offline ? "دون اتصال - من آخر بيانات محفوظة" : "");
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

    void Tick()
    {
        if (DateTime.Now >= _azkarNext)
        {
            _azkarNext = DateTime.Now.AddMinutes(Math.Max(1, _s.AzkarMinutes));
            try { _tray.Notify("منبه الأذكار", AzkarData.RandomShort().Text); } catch { }
        }
        var now = Times.Now(_s.Location.Tz);
        bool stale = _loadedDay != now.Date || _res?.Today == null;
        if (stale && (now - _lastTry).TotalSeconds > (_res?.Today == null ? 300 : 5)) _ = ReloadAsync();

        if (_res?.Today == null)
        {
            TxtNextName.Text = "--"; TxtNextTime.Text = ""; TxtCountdown.Text = "--:--:--"; TxtUnit.Text = "";
            SetHover("مواقيت الصلاة", "--:--:--", "", _s.Location.Display);
            PushWidget("--", "", "--:--:--");
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
                    try { _tray.Notify("مواقيت الصلاة", "حان الآن موعد أذان " + Times.Names[i]); } catch { }
                }
            }
        }
        var nx = Times.Next(now, _res.Today.T, _res.Tomorrow?.T);
        if (nx == null) { TxtNextName.Text = "--"; TxtCountdown.Text = "--:--:--"; TxtUnit.Text = ""; SetHover("مواقيت الصلاة", "--:--:--", "", _s.Location.Display); return; }
        var (idx, time, tomorrow) = nx.Value;
        var left = time - now;
        TxtNextName.Text = Times.Names[idx] + (tomorrow ? " (غدًا)" : "");
        TxtNextTime.Text = time.ToString("h:mm tt", CultureInfo.InvariantCulture);
        var (cnum, cunit) = Times.FmtDyn(left);
        TxtCountdown.Text = cnum; TxtUnit.Text = cunit;
        PushWidget(TxtNextName.Text, TxtNextTime.Text, cnum + " " + cunit);
        SetHover(idx == 1 ? "باقي على الشروق" : "باقي على صلاة " + Times.Names[idx] + (tomorrow ? " (غدًا)" : ""), cnum, cunit,
                 Times.Names[idx] + " " + TxtNextTime.Text + "  -  " + _s.Location.Display);

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

    void SetHover(string title, string count, string unit, string sub)
    {
        _hTitle = title; _hCount = count; _hUnit = unit; _hSub = sub;
        if (_hover.Shown) _hover.Update(title, count, unit, sub);
    }

    void PushWidget(string n, string t, string c)
    {
        _wName = n; _wTime = t; _wCount = c;
        _widget?.SetInfo(n, t, c);
    }

    // ---------- settings ----------
    void LoadSettingsUi()
    {
        CmbCountry.Items.Add("كل دول العالم");
        foreach (var c in Countries.All) CmbCountry.Items.Add(c.Ar);
        TglAdhan.IsOn = _s.NotifyAdhan;
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
        int ci = Array.FindIndex(Countries.All, c => c.Cc == _s.Location.Cc);
        CmbCountry.SelectedIndex = ci < 0 ? 0 : ci + 1;
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
            if (_places.Count == 0) TxtDetect.Text = "لا توجد نتائج";
        }
        catch { TxtDetect.Text = "فشل البحث - تأكد من الاتصال بالإنترنت"; }
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
        TxtDetect.Text = "جارٍ تحديد موقعك...";
        var (p, how) = await Times.Detect();
        if (p == null) { TxtDetect.Text = "تعذّر تحديد الموقع"; return; }
        _s.Location = new Loc { Name = p.Name, NameAr = p.Name, Lat = p.Lat, Lon = p.Lon, UseCity = false, CountryAr = p.Country, Cc = p.Cc };
        TxtDetect.Text = "تم: " + how;
        LocationChanged();
    }

    async void Coords_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtLat.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var la) &&
            double.TryParse(TxtLon.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) &&
            la >= -90 && la <= 90 && lo >= -180 && lo <= 180)
        {
            var rv = await Times.Reverse(la, lo);
            string nm = rv != null && rv.Value.city != "" ? rv.Value.city : $"إحداثيات ({la:F3}, {lo:F3})";
            _s.Location = new Loc { Name = nm, NameAr = nm, Lat = la, Lon = lo, UseCity = false, CountryAr = rv?.country ?? "", Cc = rv?.cc ?? "" };
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
        _root.FlowDirection = FlowDirection.RightToLeft;
        _list.Children.Clear();
        AddItem("فتح", "\uE8A7", 1);
        AddItem("ويدجت سطح المكتب", "\uE8A1", 4, widgetOn);
        AddItem("الإعدادات", "\uE713", 2);
        _list.Children.Add(new Border { Height = 1, Margin = new Thickness(4, 3, 4, 3), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)) });
        AddItem("خروج", "\uE7E8", 3);

        double dpi = GetDpiForWindow(_hwnd) / 96.0;
        if (dpi < 1) dpi = 1;
        int w = (int)(230 * dpi), h = (int)((4 * 36 + 3 * 2 + 7 + 8) * dpi);
        int x = Math.Clamp(ax - w / 2, wa.X + 8, wa.X + wa.Width - w - 8);
        int y = ay > wa.Y + wa.Height / 2 ? wa.Y + wa.Height - h - 8 : wa.Y + 8;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
    }
}
