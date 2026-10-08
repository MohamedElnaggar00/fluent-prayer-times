using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentPrayerTimes;

/// <summary>Localization: Arabic text is the dictionary key; Strings.Table holds the other languages.</summary>
public static class L
{
    public static readonly string[] Codes = { "ar", "en", "fr", "de", "es", "tr", "ur", "id", "ru", "pt", "fa" };
    public static readonly string[] Native = { "العربية", "English", "Français", "Deutsch", "Español", "Türkçe", "اردو", "Bahasa Indonesia", "Русский", "Português", "فارسی" };
    // Strings.Table columns: en, fr, de, es, tr, ur, id, ru, pt, fa
    static readonly string[] Cols = { "en", "fr", "de", "es", "tr", "ur", "id", "ru", "pt", "fa" };

    public static string Lang { get; private set; } = "ar";
    public static bool Rtl => Lang == "ar" || Lang == "ur" || Lang == "fa";
    public static FlowDirection Flow => Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public static bool IsArabic => Lang == "ar";

    /// <summary>Windows display language, English when unsupported.</summary>
    public static string Detect()
    {
        try
        {
            string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (c == "iw") c = "he";
            if (c == "in") c = "id";
            if (Array.IndexOf(Codes, c) >= 0) return c;
        }
        catch { }
        return "en";
    }

    public static void Set(string setting)
    {
        string c = string.IsNullOrEmpty(setting) ? Detect() : setting;
        Lang = Array.IndexOf(Codes, c) >= 0 ? c : "en";
    }

    public static string T(string ar)
    {
        if (Lang == "ar" || !Strings.Table.TryGetValue(ar, out var row)) return ar;
        int i = Array.IndexOf(Cols, Lang);
        if (i >= 0 && i < row.Length && !string.IsNullOrEmpty(row[i])) return row[i];
        if (row.Length > 0 && row[0] != null) return row[0];
        return ar;
    }

    // ---- tracked XAML elements ----
    static readonly List<(WeakReference<DependencyObject> el, string prop, string orig)> Tracked = new();

    public static void Register(object? root)
    {
        switch (root)
        {
            case null: return;
            case TextBlock tb: Track(tb, "Text", tb.Text); return;
            case TextBox tx: Track(tx, "PlaceholderText", tx.PlaceholderText); return;
            case ComboBox cb: Track(cb, "PlaceholderText", cb.PlaceholderText); break;
            case ToggleSwitch ts:
                Track(ts, "Header", ts.Header as string); Track(ts, "OnContent", ts.OnContent as string); Track(ts, "OffContent", ts.OffContent as string); return;
            case NavigationView nv:
                foreach (var m in nv.MenuItems) Register(m);
                foreach (var m in nv.FooterMenuItems) Register(m);
                Register(nv.Content); return;
            case Expander ex: Register(ex.Header); Register(ex.Content); return;
            case ContentControl cc:
                if (cc.Content is string s) Track(cc, "Content", s); else Register(cc.Content);
                return;
            case Border b: Register(b.Child); return;
            case Panel p: foreach (var c in p.Children) Register(c); return;
        }
    }

    static void Track(DependencyObject d, string prop, string? orig)
    {
        if (string.IsNullOrEmpty(orig) || !HasArabic(orig)) return;
        Tracked.Add((new WeakReference<DependencyObject>(d), prop, orig));
    }

    static bool HasArabic(string s) { foreach (var ch in s) if (ch >= '\u0600' && ch <= '\u06FF') return true; return false; }

    public static void Refresh()
    {
        foreach (var (wr, prop, orig) in Tracked)
        {
            if (!wr.TryGetTarget(out var d)) continue;
            string t = T(orig);
            switch (d)
            {
                case TextBlock tb when prop == "Text": tb.Text = t; break;
                case TextBox tx: tx.PlaceholderText = t; break;
                case ComboBox cb: cb.PlaceholderText = t; break;
                case ToggleSwitch ts:
                    if (prop == "Header") ts.Header = t; else if (prop == "OnContent") ts.OnContent = t; else ts.OffContent = t; break;
                case ContentControl cc: cc.Content = t; break;
            }
        }
    }

    public static string CountryName(string cc, string ar)
    {
        if (Lang == "ar") return ar;
        try { return new RegionInfo(cc).EnglishName; } catch { return ar; }
    }

    static readonly string[] ArMonths = { "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة", "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة" };
    public static string LocHijri(string h)
    {
        if (Lang == "ar" || string.IsNullOrEmpty(h)) return h;
        h = h.Replace("ربيع الثاني", "ربيع الآخر").Replace("جمادى الثانية", "جمادى الآخرة").Replace("ذوالقعدة", "ذو القعدة").Replace("ذوالحجة", "ذو الحجة");
        foreach (var m in ArMonths) h = h.Replace(m, T(m));
        return h.Replace(" هـ", " " + T("هـ"));
    }
}
