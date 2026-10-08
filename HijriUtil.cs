using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluentPrayerTimes;

public static class HijriUtil
{
    public static readonly UmAlQuraCalendar Cal = new();
    public static string[] HMonths => new[] { L.T("محرم"), L.T("صفر"), L.T("ربيع الأول"), L.T("ربيع الآخر"), L.T("جمادى الأولى"), L.T("جمادى الآخرة"), L.T("رجب"), L.T("شعبان"), L.T("رمضان"), L.T("شوال"), L.T("ذو القعدة"), L.T("ذو الحجة") };
    public static string[] GMonths => new[] { L.T("يناير"), L.T("فبراير"), L.T("مارس"), L.T("أبريل"), L.T("مايو"), L.T("يونيو"), L.T("يوليو"), L.T("أغسطس"), L.T("سبتمبر"), L.T("أكتوبر"), L.T("نوفمبر"), L.T("ديسمبر") };
    // index = (int)DayOfWeek, Sunday = 0
    public static string[] Days => new[] { L.T("الأحد"), L.T("الاثنين"), L.T("الثلاثاء"), L.T("الأربعاء"), L.T("الخميس"), L.T("الجمعة"), L.T("السبت") };
    public static int MinYear => Cal.GetYear(Cal.MinSupportedDateTime) + 1;
    public static int MaxYear => Cal.GetYear(Cal.MaxSupportedDateTime) - 1;

    public static (int y, int m, int d) ToHijri(DateTime dt) => (Cal.GetYear(dt), Cal.GetMonth(dt), Cal.GetDayOfMonth(dt));
    public static DateTime ToGreg(int y, int m, int d) => Cal.ToDateTime(y, m, d, 0, 0, 0, 0);
    public static bool ValidHijri(int y, int m, int d) =>
        y >= MinYear && y <= MaxYear && m >= 1 && m <= 12 && d >= 1 && d <= Cal.GetDaysInMonth(y, m);

    /// <summary>Islamic occasions for a Hijri month/day.</summary>
    public static List<string> Events(int m, int d)
    {
        var l = new List<string>();
        switch (m, d)
        {
            case (1, 1): l.Add(L.T("رأس السنة الهجرية")); break;
            case (1, 9): l.Add(L.T("يوم تاسوعاء")); break;
            case (1, 10): l.Add(L.T("يوم عاشوراء")); break;
            case (3, 12): l.Add(L.T("المولد النبوي الشريف")); break;
            case (7, 27): l.Add(L.T("ذكرى الإسراء والمعراج")); break;
            case (8, 15): l.Add(L.T("ليلة النصف من شعبان")); break;
            case (9, 1): l.Add(L.T("أول أيام شهر رمضان المبارك")); break;
            case (9, 21): l.Add(L.T("بداية العشر الأواخر من رمضان")); break;
            case (9, 27): l.Add(L.T("ليلة السابع والعشرين (يُرجى فيها ليلة القدر)")); break;
            case (10, 1): l.Add(L.T("عيد الفطر المبارك")); break;
            case (12, 1): l.Add(L.T("بداية العشر الأوائل من ذي الحجة")); break;
            case (12, 9): l.Add(L.T("يوم عرفة")); break;
            case (12, 10): l.Add(L.T("عيد الأضحى المبارك")); break;
            case (12, 11): l.Add(L.T("أول أيام التشريق")); break;
            case (12, 12): l.Add(L.T("ثاني أيام التشريق")); break;
            case (12, 13): l.Add(L.T("ثالث أيام التشريق")); break;
        }
        if (m != 9 && !(m == 12 && d == 13) && d >= 13 && d <= 15)
            l.Add(L.T("صيام أيام البيض (اليوم ") + (d == 13 ? L.T("الأول") : d == 14 ? L.T("الثاني") : L.T("الثالث")) + ")");
        return l;
    }

    const string NS = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";
    // Built with XamlReader so {ThemeResource} follows the element's own (light/dark) theme.
    public static Border Card(UIElement child, Thickness? pad = null)
    {
        var b = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<Border {NS} Background='{{ThemeResource CardBackgroundFillColorDefaultBrush}}' BorderBrush='{{ThemeResource CardStrokeColorDefaultBrush}}' BorderThickness='1' CornerRadius='8'/>");
        b.Padding = pad ?? new Thickness(16); b.Child = child; return b;
    }
    public static Border Themed(string bgBrushKey, double opacity = 1)
        => (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<Border {NS} Background='{{ThemeResource {bgBrushKey}}}' Opacity='{opacity.ToString(CultureInfo.InvariantCulture)}'/>");
    public static TextBlock Themed(TextBlock s, string fgBrushKey)
    {
        var t = (TextBlock)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<TextBlock {NS} Foreground='{{ThemeResource {fgBrushKey}}}'/>");
        t.Text = s.Text; t.FontSize = s.FontSize; t.FontWeight = s.FontWeight; t.HorizontalAlignment = s.HorizontalAlignment;
        t.VerticalAlignment = s.VerticalAlignment; t.TextWrapping = s.TextWrapping; t.TextAlignment = s.TextAlignment; t.Margin = s.Margin;
        t.TextTrimming = s.TextTrimming; t.FlowDirection = s.FlowDirection;
        return t;
    }
    public static TextBlock Sec(TextBlock s) => Themed(s, "TextFillColorSecondaryBrush");
    public static Windows.UI.Color Sky => Windows.UI.Color.FromArgb(255, 3, 169, 244);
}
