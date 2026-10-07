using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluentPrayerTimes;

/// <summary>"منبه الأذكار": random dhikr + periodic reminder settings.</summary>
sealed class AzkarPage : StackPanel
{
    readonly Settings _s;
    readonly Action _changed;
    readonly TextBlock _text = new() { FontSize = 22, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, IsTextSelectionEnabled = true, LineHeight = 38 };
    readonly TextBlock _cat = HijriUtil.Sec(new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
    readonly ComboBox _cmb = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly NumberBox _custom = new() { Minimum = 1, Maximum = 1440, Value = 30, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, MinWidth = 120 };
    readonly ToggleSwitch _tgl = new() { OnContent = "مفعّل", OffContent = "متوقف" };
    readonly TextBlock _status = HijriUtil.Sec(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });
    static readonly int[] Presets = { 15, 30, 45, 60, 120 };
    Dhikr _cur = AzkarData.Random();
    bool _init = true;

    static Border Card(UIElement child, Thickness? pad = null) => HijriUtil.Card(child, pad);

    public AzkarPage(Settings s, Action changed)
    {
        _s = s; _changed = changed; Spacing = 10;
        Children.Add(new TextBlock { Text = "منبه الأذكار", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var dh = new StackPanel { Spacing = 8 };
        dh.Children.Add(_text); dh.Children.Add(_cat);
        var next = new Button { Content = "ذكر آخر", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        next.Click += (a, b) => { _cur = AzkarData.Random(_cur); Show(); };
        dh.Children.Add(next);
        Children.Add(Card(dh, new Thickness(16, 20, 16, 16)));

        var st = new StackPanel { Spacing = 10 };
        st.Children.Add(new TextBlock { Text = "التنبيه بالأذكار", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        st.Children.Add(_tgl);
        st.Children.Add(new TextBlock { Text = "إرسال تنبيه بالأذكار كل:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var m in Presets) _cmb.Items.Add(m + " دقيقة");
        st.Children.Add(_cmb);
        st.Children.Add(new TextBlock { Text = "أو أدخل عددًا مخصصًا من الدقائق:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(_custom);
        var apply = new Button { Content = "تطبيق" };
        apply.Click += (a, b) => ApplyCustom();
        row.Children.Add(apply);
        st.Children.Add(row);
        st.Children.Add(_status);
        Children.Add(Card(st));

        Children.Add(HijriUtil.Sec(new TextBlock { Text = AzkarData.Source, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 2, 4, 0) }));

        int idx = Array.IndexOf(Presets, _s.AzkarMinutes);
        _cmb.SelectedIndex = idx;
        if (idx < 0) _custom.Value = _s.AzkarMinutes;
        _tgl.IsOn = _s.AzkarOn;
        _cmb.SelectionChanged += (a, b) => { if (_init || _cmb.SelectedIndex < 0) return; SetMinutes(Presets[_cmb.SelectedIndex]); };
        _tgl.Toggled += (a, b) => { if (_init) return; _s.AzkarOn = _tgl.IsOn; Save(); };
        _init = false;
        Show(); UpdateStatus();
    }

    void Show() { _text.Text = _cur.Text; _cat.Text = _cur.Cat; }

    void ApplyCustom()
    {
        if (double.IsNaN(_custom.Value)) return;
        int m = (int)Math.Clamp(Math.Round(_custom.Value), 1, 1440);
        SetMinutes(m);
        _init = true; int i = Array.IndexOf(Presets, m); _cmb.SelectedIndex = i; _init = false;
    }

    void SetMinutes(int m) { _s.AzkarMinutes = m; Save(); }
    void Save() { _s.Save(); _changed(); UpdateStatus(); }
    void UpdateStatus() => _status.Text = _s.AzkarOn ? "التنبيه مفعّل: كل " + _s.AzkarMinutes + " دقيقة." : "التنبيه متوقف. فعّله ليصلك ذكر كل " + _s.AzkarMinutes + " دقيقة.";
}
