using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Text;

namespace FluentPrayerTimes;

/// <summary>Hijri month calendar with Gregorian day numbers, occasion markers and an occasions list.</summary>
sealed class CalendarPage : StackPanel
{
    int _y, _m;
    readonly TextBlock _title = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    readonly TextBlock _sub = HijriUtil.Sec(new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center });
    readonly Grid _grid = new() { FlowDirection = L.Flow, RowSpacing = 2, ColumnSpacing = 2 };
    readonly StackPanel _list = new() { Spacing = 6 };
    static string[] Head => new[] { L.T("السبت"), L.T("الأحد"), L.T("الاثنين"), L.T("الثلاثاء"), L.T("الأربعاء"), L.T("الخميس"), L.T("الجمعة") };

    public CalendarPage()
    {
        Spacing = 10;
        (_y, _m, _) = HijriUtil.ToHijri(DateTime.Today);
        Children.Add(new TextBlock { Text = L.T("التقويم الهجري"), FontSize = 24, FontWeight = FontWeights.SemiBold });

        var nav = new Grid();
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var prev = new Button { Content = new FontIcon { Glyph = L.Rtl ? "\uE76C" : "\uE76B" } };   // chevron: previous (right side in RTL)
        var next = new Button { Content = new FontIcon { Glyph = L.Rtl ? "\uE76B" : "\uE76C" } };
        prev.Click += (a, b) => Step(-1);
        next.Click += (a, b) => Step(1);
        var mid = new StackPanel { Spacing = 2 };
        mid.Children.Add(_title); mid.Children.Add(_sub);
        Grid.SetColumn(prev, 0); Grid.SetColumn(mid, 1); Grid.SetColumn(next, 2);
        nav.Children.Add(prev); nav.Children.Add(mid); nav.Children.Add(next);
        var today = new Button { Content = L.T("اليوم"), HorizontalAlignment = HorizontalAlignment.Center };
        today.Click += (a, b) => { (_y, _m, _) = HijriUtil.ToHijri(DateTime.Today); Render(); };

        var box = new StackPanel { Spacing = 8 };
        box.Children.Add(nav);
        box.Children.Add(_grid);
        Children.Add(HijriUtil.Card(box, new Thickness(8, 12, 8, 12)));
        Children.Add(today);
        var evBox = new StackPanel { Spacing = 8 };
        evBox.Children.Add(new TextBlock { Text = L.T("المناسبات في هذا الشهر"), FontSize = 16, FontWeight = FontWeights.SemiBold });
        evBox.Children.Add(_list);
        Children.Add(HijriUtil.Card(evBox));
        Render();
    }

    void Step(int d)
    {
        _m += d;
        if (_m > 12) { _m = 1; _y++; } else if (_m < 1) { _m = 12; _y--; }
        _y = Math.Clamp(_y, HijriUtil.MinYear, HijriUtil.MaxYear);
        Render();
    }

    void Render()
    {
        var first = HijriUtil.ToGreg(_y, _m, 1);
        int days = HijriUtil.Cal.GetDaysInMonth(_y, _m);
        var last = first.AddDays(days - 1);
        _title.Text = HijriUtil.HMonths[_m - 1] + " " + _y;
        _sub.Text = first.Month == last.Month
            ? HijriUtil.GMonths[first.Month - 1] + " " + first.Year
            : HijriUtil.GMonths[first.Month - 1] + " - " + HijriUtil.GMonths[last.Month - 1] + " " + last.Year;

        _grid.Children.Clear(); _grid.RowDefinitions.Clear(); _grid.ColumnDefinitions.Clear();
        for (int c = 0; c < 7; c++) _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int offset = ((int)first.DayOfWeek + 1) % 7;   // Saturday = column 0
        int rows = (offset + days + 6) / 7;
        for (int r = 0; r < rows + 1; r++) _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < 7; c++)
        {
            var h = HijriUtil.Sec(new TextBlock { Text = Head[c], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetColumn(h, c); Grid.SetRow(h, 0); _grid.Children.Add(h);
        }
        var sky = new SolidColorBrush(HijriUtil.Sky);
        var todayDt = DateTime.Today;
        for (int d = 1; d <= days; d++)
        {
            var dt = first.AddDays(d - 1);
            int idx = offset + d - 1;
            bool isToday = dt.Date == todayDt;
            bool ev = HijriUtil.Events(_m, d).Count > 0;
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
            sp.Children.Add(new TextBlock { Text = d.ToString(), FontSize = 15, FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(HijriUtil.Sec(new TextBlock { Text = dt.Day.ToString(), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }));
            sp.Children.Add(new Ellipse { Width = 6, Height = 6, Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Fill = ev ? sky : new SolidColorBrush(Microsoft.UI.Colors.Transparent) });
            var cell = HijriUtil.Themed("ControlFillColorDefaultBrush");
            cell.Child = sp; cell.CornerRadius = new CornerRadius(4); cell.Padding = new Thickness(0, 3, 0, 2); cell.MinHeight = 52;
            if (isToday) { cell.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 3, 169, 244)); cell.BorderThickness = new Thickness(1.5); cell.BorderBrush = sky; }
            Grid.SetColumn(cell, idx % 7); Grid.SetRow(cell, 1 + idx / 7);
            _grid.Children.Add(cell);
        }

        _list.Children.Clear();
        bool any = false;
        for (int d = 1; d <= days; d++)
        {
            var evs = HijriUtil.Events(_m, d);
            var dt = first.AddDays(d - 1);
            foreach (var e in evs)
            {
                any = true;
                var row = new Grid { ColumnSpacing = 10, FlowDirection = L.Flow };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var bar = new Border { Background = sky, CornerRadius = new CornerRadius(2) };
                var date = new StackPanel();
                date.Children.Add(new TextBlock { Text = dt.Day.ToString(), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
                date.Children.Add(HijriUtil.Sec(new TextBlock { Text = HijriUtil.Days[(int)dt.DayOfWeek], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }));
                var txt = new StackPanel { Spacing = 1 };
                txt.Children.Add(new TextBlock { Text = e, FontSize = 14, TextWrapping = TextWrapping.Wrap });
                txt.Children.Add(HijriUtil.Sec(new TextBlock { Text = d + " " + HijriUtil.HMonths[_m - 1], FontSize = 12 }));
                Grid.SetColumn(bar, 0); Grid.SetColumn(date, 1); Grid.SetColumn(txt, 2);
                row.Children.Add(bar); row.Children.Add(date); row.Children.Add(txt);
                _list.Children.Add(row);
            }
        }
        if (!any) _list.Children.Add(HijriUtil.Sec(new TextBlock { Text = L.T("لا توجد مناسبات في هذا الشهر"), FontSize = 13 }));
    }
}
