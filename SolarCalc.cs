using System.Globalization;
namespace FluentPrayerTimes;

/// <summary>
/// Local prayer-time calculation, ported from the Wathkkir Android app's calculator (same solar model, same constants).
/// Seconds are kept until the very end, then ONE rule rounds every consumer's value: the five prayers to the NEAREST minute,
/// sunrise DOWN (Wathkkir decision 2026-10-21). Aladhan only returns whole minutes, so it cannot be rounded after the fact.
/// Only fixed-angle methods are handled here; anything else returns null and the caller keeps Aladhan's values.
/// </summary>
public static class SolarCalc
{
    // Aladhan method id -> (Fajr angle, Isha angle). Methods that need minutes-after-Maghrib, Ramadan rules or special
    // Maghrib angles are intentionally absent: they stay on Aladhan.
    static readonly Dictionary<int, (double Fajr, double Isha)> Angles = new()
    {
        [1] = (18.0, 18.0),    // University of Islamic Sciences, Karachi
        [2] = (15.0, 15.0),    // ISNA
        [3] = (18.0, 17.0),    // Muslim World League
        [5] = (19.5, 17.5),    // Egyptian General Authority of Survey
        [9] = (18.0, 17.5),    // Kuwait
    };

    public static bool Supports(int method) => Angles.ContainsKey(method);

    static double Rad(double d) => d * Math.PI / 180.0;
    static double Deg(double r) => r * 180.0 / Math.PI;
    static double Fix(double a) { a %= 360.0; return a < 0 ? a + 360.0 : a; }

    /// <summary>Six prayer times as HH:mm in Keys order (Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha), or null when the
    /// method or the sun geometry is not supported (the caller then uses Aladhan's values).</summary>
    public static Dictionary<string, string>? Compute(DateTime date, double lat, double lon, double utcOffsetHours,
        int method, double? customFajr, double? customIsha, int asrFactor)
    {
        double fajrAngle, ishaAngle;
        if (method == 99)
        {
            if (customFajr == null || customIsha == null) return null;
            fajrAngle = customFajr.Value; ishaAngle = customIsha.Value;
        }
        else if (Angles.TryGetValue(method, out var a)) { fajrAngle = a.Fajr; ishaAngle = a.Isha; }
        else return null;
        if (Math.Abs(lat) > 48 || asrFactor < 1 || asrFactor > 2) return null;

        int year = date.Year, month = date.Month, day = date.Day;
        double d = year * 367.0 - Math.Floor((year + Math.Floor((month + 9.0) / 12.0)) * 7.0 / 4.0)
                   + Math.Floor(month * 275.0 / 9.0) + day - 730531.5;
        double g = Fix(0.98560028 * d + 357.529);
        double q = Fix(0.98564736 * d + 280.459);
        double l = Fix(Math.Sin(Rad(g)) * 1.915 + q + Math.Sin(Rad(2.0 * g)) * 0.02);
        double e = 23.439 - 3.6E-7 * d;
        double ra = Fix(Deg(Math.Atan2(Math.Cos(Rad(e)) * Math.Sin(Rad(l)), Math.Cos(Rad(l))))) / 15.0;
        double decl = Deg(Math.Asin(Math.Sin(Rad(e)) * Math.Sin(Rad(l))));
        double eqTime = q / 15.0 - ra;
        double transit = ((utcOffsetHours * 15.0 - lon) / 15.0 + 12.0) - eqTime;

        double? Below(double ang)
        {
            double c = (-Math.Sin(Rad(ang)) - Math.Sin(Rad(lat)) * Math.Sin(Rad(decl))) / (Math.Cos(Rad(lat)) * Math.Cos(Rad(decl)));
            return c < -1 || c > 1 ? null : Deg(Math.Acos(c));
        }
        double? Above(double alt)
        {
            double c = (Math.Sin(Rad(alt)) - Math.Sin(Rad(lat)) * Math.Sin(Rad(decl))) / (Math.Cos(Rad(lat)) * Math.Cos(Rad(decl)));
            return c < -1 || c > 1 ? null : Deg(Math.Acos(c));
        }
        var sunH = Below(0.833); var fajrH = Below(fajrAngle); var ishaH = Below(ishaAngle);
        double asrAlt = Deg(Math.Atan(1.0 / (Math.Tan(Rad(Math.Abs(lat - decl))) + asrFactor)));
        var asrH = Above(asrAlt);
        if (sunH == null || fajrH == null || ishaH == null || asrH == null) return null;

        // Whole seconds of the day, truncated exactly like the Android hoursToMillis (hour, minute, then floor seconds).
        static int Secs(double h)
        {
            int hh = (int)Math.Floor(h); double md = (h - hh) * 60.0; int mm = (int)Math.Floor(md);
            int ss = (int)Math.Floor((md - mm) * 60.0);
            return hh * 3600 + mm * 60 + ss;
        }
        static string Hm(int totalMinutes) { int m = ((totalMinutes % 1440) + 1440) % 1440; return $"{m / 60:00}:{m % 60:00}"; }
        static string Near(int secs) => Hm((int)Math.Floor((secs + 30) / 60.0));
        static string Down(int secs) => Hm((int)Math.Floor(secs / 60.0));

        return new Dictionary<string, string>
        {
            ["Fajr"] = Near(Secs(transit - fajrH.Value / 15.0)),
            ["Sunrise"] = Down(Secs(transit - sunH.Value / 15.0)),
            ["Dhuhr"] = Near(Secs(transit)),
            ["Asr"] = Near(Secs(asrH.Value / 15.0 + transit)),
            ["Maghrib"] = Near(Secs(sunH.Value / 15.0 + transit)),
            ["Isha"] = Near(Secs(ishaH.Value / 15.0 + transit)),
        };
    }
}
