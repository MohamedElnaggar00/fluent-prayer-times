using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;

namespace FluentPrayerTimes;

/// <summary>"محول التاريخ": Hijri to Gregorian and back, with a swap button.</summary>
sealed class ConverterPage : StackPanel
{
    bool _fromHijri = true;
    bool _busy;
    readonly TextBlock _fromLbl = new() { FontWeight = FontWeights.SemiBold, FontSize = 16 };
    readonly TextBlock _toLbl = new() { FontWeight = FontWeights.SemiBold, FontSize = 16 };
    readonly NumberBox _day = new() { Header = "اليوم", Minimum = 1, Maximum = 31, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 100 };
    readonly ComboBox _month = new() { Header = "الشهر", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly NumberBox _year = new() { Header = "السنة", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 120 };
    readonly TextBlock _res = new() { FontSize = 26, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _resDay = HijriUtil.Sec(new TextBlock { FontSize = 14 });

    public ConverterPage()
    {
        Spacing = 10;
        Children.Add(new TextBlock { Text = "محول التاريخ", FontSize = 24, FontWeight = FontWeights.SemiBold });

        var inp = new StackPanel { Spacing = 10 };
        inp.Children.Add(_fromLbl);
        var g = new Grid { ColumnSpacing = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_day, 0); Grid.SetColumn(_month, 1); Grid.SetColumn(_year, 2);
        g.Children.Add(_day); g.Children.Add(_month); g.Children.Add(_year);
        inp.Children.Add(g);
        Children.Add(HijriUtil.Card(inp));

        var swap = new Button { Content = new FontIcon { Glyph = "\uE8AB" }, HorizontalAlignment = HorizontalAlignment.Center };
        ToolTipService.SetToolTip(swap, "عكس اتجاه التحويل");
        swap.Click += (a, b) => Swap();
        Children.Add(swap);

        var outp = new StackPanel { Spacing = 4 };
        outp.Children.Add(_toLbl); outp.Children.Add(_res); outp.Children.Add(_resDay);
        Children.Add(HijriUtil.Card(outp));

        _day.ValueChanged += (a, b) => Convert();
        _year.ValueChanged += (a, b) => Convert();
        _month.SelectionChanged += (a, b) => Convert();
        SetMode(true, DateTime.Today);
    }

    void SetMode(bool fromHijri, DateTime date)
    {
        _busy = true;
        _fromHijri = fromHijri;
        _fromLbl.Text = fromHijri ? "من: تاريخ هجري" : "من: تاريخ ميلادي";
        _toLbl.Text = fromHijri ? "إلى: تاريخ ميلادي" : "إلى: تاريخ هجري";
        _month.Items.Clear();
        foreach (var n in fromHijri ? HijriUtil.HMonths : HijriUtil.GMonths) _month.Items.Add(n);
        if (fromHijri) { var (y, m, d) = HijriUtil.ToHijri(date); _year.Value = y; _month.SelectedIndex = m - 1; _day.Value = d; _year.Minimum = HijriUtil.MinYear; _year.Maximum = HijriUtil.MaxYear; }
        else { _year.Value = date.Year; _month.SelectedIndex = date.Month - 1; _day.Value = date.Day; _year.Minimum = 1901; _year.Maximum = 2076; }
        _busy = false;
        Convert();
    }

    DateTime? Current()
    {
        if (double.IsNaN(_day.Value) || double.IsNaN(_year.Value) || _month.SelectedIndex < 0) return null;
        int d = (int)_day.Value, m = _month.SelectedIndex + 1, y = (int)_year.Value;
        try
        {
            if (_fromHijri) return HijriUtil.ValidHijri(y, m, d) ? HijriUtil.ToGreg(y, m, d) : null;
            if (y < 1901 || y > 2076 || d > DateTime.DaysInMonth(y, m)) return null;
            return new DateTime(y, m, d);
        }
        catch { return null; }
    }

    void Convert()
    {
        if (_busy) return;
        var dt = Current();
        if (dt == null) { _res.Text = "تاريخ غير صحيح"; _resDay.Text = ""; return; }
        var v = dt.Value;
        if (_fromHijri) _res.Text = v.Day + " " + HijriUtil.GMonths[v.Month - 1] + " " + v.Year + " م";
        else { var (y, m, d) = HijriUtil.ToHijri(v); _res.Text = d + " " + HijriUtil.HMonths[m - 1] + " " + y + " هـ"; }
        _resDay.Text = "يوم " + HijriUtil.Days[(int)v.DayOfWeek];
    }

    void Swap()
    {
        var dt = Current() ?? DateTime.Today;
        SetMode(!_fromHijri, dt);
    }
}
