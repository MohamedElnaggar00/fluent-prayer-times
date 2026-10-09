using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FluentPrayerTimes;

public class Loc {
    public string Name { get; set; } = "Alexandria";   // English name used for timingsByCity
    public string NameAr { get; set; } = "الإسكندرية";
    public double Lat { get; set; } = 31.2001;
    public double Lon { get; set; } = 29.9187;
    public bool UseCity { get; set; } = true;           // true: timingsByCity, false: coordinates
    public string CountryAr { get; set; } = "مصر";
    public string Cc { get; set; } = "EG";               // ISO country code
    public string? Tz { get; set; }                      // IANA zone of the place (learned from the API)
    public string Display { get { var n = L.T(NameAr); if (!L.IsArabic && !UseCity && System.Text.RegularExpressions.Regex.IsMatch(n, "[\u0600-\u06FF]")) n = ""; var c = string.IsNullOrEmpty(Cc) ? L.T(CountryAr) : L.CountryName(Cc, CountryAr); return n == "" ? c : string.IsNullOrEmpty(c) ? n : n + L.T("، ") + c; } }
    public string Key => UseCity ? "c:" + Name : $"g:{Lat:F3},{Lon:F3}";
}
public class Settings {
    public Loc Location { get; set; } = new();
    public bool RunAtStartup { get; set; } = false;
    public string Backdrop { get; set; } = "micaalt";   // micaalt | mica | acrylic
    public string Theme { get; set; } = "system";
    public bool WidgetVisible { get; set; } = false;
    public bool WidgetPinned { get; set; } = true;
    public int WidgetSnap { get; set; } = 2;   // 0..7: TL, T, TR, L, R, BL, B, BR
    public int WidgetX { get; set; } = int.MinValue;
    public int WidgetY { get; set; } = int.MinValue;       // system | light | dark
    public bool NotifyAdhan { get; set; } = true;
    public string Accent { get; set; } = "";           // "" = built-in teal, else #RRGGBB
    public bool AutoUpdate { get; set; } = true;
    public string Lang { get; set; } = "";             // "" = follow Windows display language
    public string UpdateSeen { get; set; } = "";
    public bool AzkarOn { get; set; } = false;
    public int AzkarMinutes { get; set; } = 30;
    public int CalculationMethod { get; set; } = -1; // -1: local authority chosen by provider
    public int AsrSchool { get; set; } = -1; // -1: provider default; 0: standard; 1: Hanafi
    public int HighLatitude { get; set; } = -1; // -1: provider default; 0: none; 1: midnight; 2: seventh; 3: angle
    public double FajrAngle { get; set; } = 18;
    public double IshaAngle { get; set; } = 17;
    public int CustomRule { get; set; } = 0; // 0: angles; 1: Fajr angle/Isha minutes; 2: both minutes; 3: Fajr minutes/Isha angle
    public double FajrMinutes { get; set; } = 90;
    public double IshaMinutes { get; set; } = 90;
    public string TimeZoneOverride { get; set; } = "";
    public string Dst { get; set; } = "auto";           // auto | off | on
    public int[] IqamaMin { get; set; } = { 20, 0, 20, 20, 15, 20 };   // minutes from adhan to iqama, indexed like Times.Keys (sunrise unused)
    public string AdhanSound { get; set; } = "default";  // default | short | full
    public string IqamaSound { get; set; } = "default";  // default | short | full
    public int IqamaFor(int i)
    {
        var a = IqamaMin; int v = a != null && i >= 0 && i < a.Length ? a[i] : 0;
        if (v <= 0) v = i == 4 ? 15 : 20;
        return Math.Clamp(v, 2, 180);
    }
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static string F => Path.Combine(Dir, "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(F)) ?? new(); } catch { return new(); } }
    public void Save() { try { Directory.CreateDirectory(Dir); File.WriteAllText(F, JsonSerializer.Serialize(this)); } catch { } }
    // Timezone sent to Aladhan. auto = the API's own zone for the place (follows Egypt's DST rules)
    public string? TzParam => !string.IsNullOrWhiteSpace(TimeZoneOverride) ? TimeZoneOverride : Location.Cc != "EG" ? null : Dst switch { "off" => "Etc/GMT-2", "on" => "Etc/GMT-3", _ => null };
}
public record City(string Name, string Ar, double Lat, double Lon);
public record Day(Dictionary<string, string> T, string Hijri, string? Tz = null, int Method = -1, int School = 0, int HighLatitude = 3);

public static class Cities {
    public static readonly City[] All = {
        new("Cairo","القاهرة",30.0444,31.2357), new("Giza","الجيزة",30.0131,31.2089), new("Alexandria","الإسكندرية",31.2001,29.9187),
        new("Port Said","بورسعيد",31.2653,32.3019), new("Suez","السويس",29.9668,32.5498), new("Ismailia","الإسماعيلية",30.5965,32.2715),
        new("Damietta","دمياط",31.4165,31.8133), new("Mansoura","المنصورة",31.0409,31.3785), new("Tanta","طنطا",30.7865,31.0004),
        new("Zagazig","الزقازيق",30.5877,31.5020), new("Banha","بنها",30.4659,31.1848), new("Shibin El Kom","شبين الكوم",30.5503,31.0107),
        new("Damanhur","دمنهور",31.0341,30.4682), new("Kafr El Sheikh","كفر الشيخ",31.1107,30.9388), new("Fayoum","الفيوم",29.3084,30.8428),
        new("Beni Suef","بني سويف",29.0661,31.0994), new("Minya","المنيا",28.1099,30.7503), new("Asyut","أسيوط",27.1783,31.1859),
        new("Sohag","سوهاج",26.5591,31.6948), new("Qena","قنا",26.1551,32.7160), new("Luxor","الأقصر",25.6872,32.6396),
        new("Aswan","أسوان",24.0889,32.8998), new("Hurghada","الغردقة",27.2579,33.8116), new("Sharm El Sheikh","شرم الشيخ",27.9158,34.3300),
        new("El Arish","العريش",31.1316,33.7984), new("Marsa Matruh","مرسى مطروح",31.3543,27.2373), new("Kharga","الخارجة",25.4513,30.5464),
        new("6th of October City","مدينة 6 أكتوبر",29.9285,30.9188), new("New Cairo","القاهرة الجديدة",30.0300,31.4700),
        new("Borg El Arab","برج العرب",30.8784,29.5897), new("Ras Sudr","رأس سدر",29.5958,32.7160),
    };
}

public static class Times {
    public static readonly string[] Keys = { "Fajr", "Sunrise", "Dhuhr", "Asr", "Maghrib", "Isha" };
    public static string[] Names => new[] { L.T("الفجر"), L.T("الشروق"), L.T("الظهر"), L.T("العصر"), L.T("المغرب"), L.T("العشاء") };
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static string CacheFile => Path.Combine(Dir, "cache3.json");
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static readonly Regex Diac = new("[\u064B-\u0652\u0670]");
    public static string CalculationQuery(Settings s)
    {
        string q = "";
        if (s.CalculationMethod >= 0) q += "&method=" + s.CalculationMethod;
        if (s.AsrSchool >= 0) q += "&school=" + s.AsrSchool;
        if (s.HighLatitude >= 0) q += "&latitudeAdjustmentMethod=" + s.HighLatitude;
        if (s.CalculationMethod == 99)
            q += "&methodSettings=" + s.FajrAngle.ToString(CultureInfo.InvariantCulture) + ",null," + s.IshaAngle.ToString(CultureInfo.InvariantCulture);
        if (s.TzParam != null) q += "&timezonestring=" + Uri.EscapeDataString(s.TzParam);
        return q;
    }
    public static string CacheKey(Settings s, DateTime d) => $"{s.Location.Key}|{s.Location.Cc}|{s.Location.Lat.ToString(CultureInfo.InvariantCulture)}|{s.Location.Lon.ToString(CultureInfo.InvariantCulture)}|{CalculationQuery(s)}|{s.CustomRule}|{s.FajrMinutes.ToString(CultureInfo.InvariantCulture)}|{s.IshaMinutes.ToString(CultureInfo.InvariantCulture)}|{d:yyyy-MM-dd}";
    static string CK(Settings s, DateTime d) => CacheKey(s, d);

    static Dictionary<string, Day> Read() {
        try { return JsonSerializer.Deserialize<Dictionary<string, Day>>(File.ReadAllText(CacheFile)) ?? new(); } catch { return new(); }
    }
    static async Task<Day> Fetch(Settings s, DateTime d) {
        var l = s.Location; var inv = CultureInfo.InvariantCulture;
        // Coordinates avoid assuming an Egyptian country for a worldwide city selection.
        string url = $"https://api.aladhan.com/v1/timings/{d:dd-MM-yyyy}?latitude={l.Lat.ToString(inv)}&longitude={l.Lon.ToString(inv)}{CalculationQuery(s)}";
        string json = await Http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var t = data.GetProperty("timings"); var r = new Dictionary<string, string>();
        foreach (var k in Keys)
        {
            string value = t.GetProperty(k).GetString() ?? "";
            if (value.Length < 5 || !DateTime.TryParseExact(value[..5], "HH:mm", inv, DateTimeStyles.None, out _))
                throw new InvalidOperationException("Prayer time is unavailable for the selected calculation settings: " + k);
            r[k] = value[..5];
        }
        if (s.CalculationMethod == 99)
        {
            if (s.CustomRule is 2 or 3) r["Fajr"] = At(d, r["Sunrise"]).AddMinutes(-s.FajrMinutes).ToString("HH:mm", inv);
            if (s.CustomRule is 1 or 2) r["Isha"] = At(d, r["Maghrib"]).AddMinutes(s.IshaMinutes).ToString("HH:mm", inv);
        }
        var h = data.GetProperty("date").GetProperty("hijri");
        string hij = $"{h.GetProperty("day").GetString()!.TrimStart('0')} {Diac.Replace(h.GetProperty("month").GetProperty("ar").GetString()!, "")} {h.GetProperty("year").GetString()} هـ";
        string? zone = null;
        try { zone = data.GetProperty("meta").GetProperty("timezone").GetString(); } catch { }
        var meta = data.GetProperty("meta");
        int method = meta.GetProperty("method").TryGetProperty("id", out var id) ? id.GetInt32() : s.CalculationMethod;
        int school = meta.TryGetProperty("school", out var sc) && sc.GetString() == "HANAFI" ? 1 : 0;
        int high = meta.TryGetProperty("latitudeAdjustmentMethod", out var hl) ? hl.GetString() switch { "NONE" => 0, "MIDDLE_OF_THE_NIGHT" => 1, "ONE_SEVENTH" => 2, _ => 3 } : 3;
        return new Day(r, hij, zone, method, school, high);
    }
    static async Task<Day> Get(Settings s, DateTime d, Dictionary<string, Day> cache) {
        var k = CK(s, d); if (cache.TryGetValue(k, out var c)) return c;
        var r = await Fetch(s, d); cache[k] = r; return r;
    }
    public record Result(Day? Today, Day? Tomorrow, bool Offline);
    public static async Task<Result> Load(Settings s) {
        var cache = Read(); var now = Now(s.Location.Tz); var tom = now.Date.AddDays(1);
        Day? today = null, tomorrow = null; bool off = false;
        try { today = await Get(s, now, cache); } catch { off = true; }
        if (today?.Tz != null && today.Tz != s.Location.Tz)
        {
            s.Location.Tz = today.Tz;
            now = Now(s.Location.Tz); tom = now.Date.AddDays(1);
            try { today = await Get(s, now, cache); } catch { off = true; }
        }
        try { tomorrow = await Get(s, tom, cache); } catch { cache.TryGetValue(CK(s, tom), out tomorrow); }
        var keep = cache.Where(kv => kv.Key == CK(s, now) || kv.Key == CK(s, tom)).ToDictionary(kv => kv.Key, kv => kv.Value);
        try { Directory.CreateDirectory(Dir); File.WriteAllText(CacheFile, JsonSerializer.Serialize(keep)); } catch { }
        return new(today, tomorrow, off);
    }
    /// <summary>Current wall-clock time in the place's own time zone (falls back to the PC clock).</summary>
    public static DateTime Now(string? tz)
    {
        if (string.IsNullOrEmpty(tz)) return DateTime.Now;
        try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tz)); } catch { return DateTime.Now; }
    }
    public static DateTime At(DateTime day, string hm, int add = 0) {
        var p = hm.Split(':'); return day.Date.AddDays(add).AddHours(int.Parse(p[0])).AddMinutes(int.Parse(p[1]));
    }
    public static string F12(string hm) => At(DateTime.Today, hm).ToString("h:mm tt", CultureInfo.InvariantCulture);
    public static (int idx, DateTime time, bool tomorrow)? Next(DateTime now, Dictionary<string, string> today, Dictionary<string, string>? tomorrow) {
        for (int i = 0; i < Keys.Length; i++) { var t = At(now, today[Keys[i]]); if (t > now) return (i, t, false); }
        if (tomorrow != null) return (0, At(now, tomorrow["Fajr"], 1), true);
        return null;
    }
    public static (string num, string unit) FmtDyn(TimeSpan s)
    {
        if (s < TimeSpan.Zero) s = TimeSpan.Zero;
        if (s.TotalHours >= 1) return ($"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}", L.T("ساعة"));
        if (s.TotalMinutes >= 1) return ($"{s.Minutes:00}:{s.Seconds:00}", L.T("دقيقة"));
        return ($"{s.Seconds:00}", L.T("ثانية"));
    }
    public enum Phase { Next, Adhan, Iqama, IqamaNow }
    public record Disp(Phase Phase, int Idx, DateTime Time, bool Tomorrow, TimeSpan Left);
    /// <summary>Which stage the countdown is in: next prayer, adhan (1 min), waiting for iqama, iqama reached (1 min).</summary>
    public static Disp? State(DateTime now, Dictionary<string, string> today, Dictionary<string, string>? tomorrow, Settings s)
    {
        var one = TimeSpan.FromMinutes(1);
        for (int i = 0; i < Keys.Length; i++)
        {
            if (i == 1) continue; // sunrise has no adhan or iqama
            var pt = At(now, today[Keys[i]]);
            var el = now - pt;
            if (el < TimeSpan.Zero) continue;
            if (el < one) return new Disp(Phase.Adhan, i, pt, false, one - el);
            var iq = pt.AddMinutes(s.IqamaFor(i));
            if (now < iq) return new Disp(Phase.Iqama, i, iq, false, iq - now);
            if (now - iq < one) return new Disp(Phase.IqamaNow, i, iq, false, TimeSpan.Zero);
        }
        var nx = Next(now, today, tomorrow);
        return nx == null ? null : new Disp(Phase.Next, nx.Value.idx, nx.Value.time, nx.Value.tomorrow, nx.Value.time - now);
    }
    /// <summary>Hours:minutes (00:00), rounded up so the last partial minute still shows 00:01.</summary>
    public static string FmtHM(TimeSpan s)
    {
        if (s < TimeSpan.Zero) s = TimeSpan.Zero;
        int m = (int)Math.Ceiling(s.TotalMinutes);
        return $"{m / 60:00}:{m % 60:00}";
    }
    /// <summary>Minutes:seconds (00:00) for the iqama countdown.</summary>
    public static string FmtMinutes(TimeSpan s) => Math.Max(0, (int)Math.Ceiling(s.TotalMinutes)).ToString(CultureInfo.InvariantCulture);
    public static string FmtMS(TimeSpan s)
    {
        if (s < TimeSpan.Zero) s = TimeSpan.Zero;
        int t = (int)Math.Ceiling(s.TotalSeconds);
        return $"{t / 60:00}:{t % 60:00}";
    }
    public static string Fmt(TimeSpan s) { if (s < TimeSpan.Zero) s = TimeSpan.Zero; return $"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}"; }

    // ---- location services ----
    public record Place(string Label, double Lat, double Lon, string Name = "", string Country = "", string Cc = "");
    public static async Task<List<Place>> Search(string q, string? cc = null) {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(q)}&count=15&language=ar&format=json" + (string.IsNullOrEmpty(cc) ? "" : "&countryCode=" + cc);
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var res = new List<Place>();
        if (doc.RootElement.TryGetProperty("results", out var arr))
            foreach (var e in arr.EnumerateArray()) {
                string n = e.GetProperty("name").GetString() ?? "";
                string a = e.TryGetProperty("admin1", out var ad) ? ad.GetString() ?? "" : "";
                string c = e.TryGetProperty("country", out var co) ? co.GetString() ?? "" : "";
                string code = e.TryGetProperty("country_code", out var cd) ? cd.GetString() ?? "" : "";
                string label = n + (a == "" || a == n ? "" : " - " + a) + (c == "" ? "" : " - " + c);
                res.Add(new Place(label, e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble(), n, c, code.ToUpperInvariant()));
            }
        return res;
    }
    /// <summary>Reverse geocoding (city + country in Arabic) for a coordinate pair.</summary>
    public static async Task<(string city, string country, string cc)?> Reverse(double lat, double lon) {
        try {
            var inv = CultureInfo.InvariantCulture;
            var url = $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat.ToString(inv)}&longitude={lon.ToString(inv)}&localityLanguage=ar";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var r = doc.RootElement;
            string Get(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            string city = Get("city"); if (city == "") city = Get("locality"); if (city == "") city = Get("principalSubdivision");
            string country = Get("countryName"), cc = Get("countryCode").ToUpperInvariant();
            if (city == "" && country == "") return null;
            return (city, country, cc);
        } catch { return null; }
    }
    // Windows: real location via the OS (Wi-Fi/GPS) using PowerShell GeoCoordinateWatcher; fallback: IP-based
    public static async Task<(Place? p, string how)> Detect() {
        double lat = 0, lon = 0; string how = ""; bool ok = false;
        if (OperatingSystem.IsWindows()) {
            try {
                const string ps = "Add-Type -AssemblyName System.Device; $w=New-Object System.Device.Location.GeoCoordinateWatcher; $w.Start(); $i=0; while(($w.Status -ne 'Ready') -and ($i -lt 60)){Start-Sleep -Milliseconds 250;$i++}; $l=$w.Position.Location; if($l.IsUnknown){exit 2}; [string]::Format([cultureinfo]::InvariantCulture,'{0},{1}',$l.Latitude,$l.Longitude)";
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + ps.Replace("\"", "\\\"") + "\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var pr = System.Diagnostics.Process.Start(psi)!;
                var outp = await pr.StandardOutput.ReadToEndAsync(); await pr.WaitForExitAsync();
                var parts = outp.Trim().Split(',');
                if (pr.ExitCode == 0 && parts.Length == 2) {
                    lat = double.Parse(parts[0], CultureInfo.InvariantCulture); lon = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    how = L.T("نظام الموقع في ويندوز"); ok = true;
                }
            } catch { }
        }
        if (!ok) {
            try {
                using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://ipwho.is/"));
                var r = doc.RootElement;
                if (r.GetProperty("success").GetBoolean()) { lat = r.GetProperty("latitude").GetDouble(); lon = r.GetProperty("longitude").GetDouble(); how = L.T("عنوان IP (تقريبي)"); ok = true; }
            } catch { }
        }
        if (!ok) return (null, "");
        var rv = await Reverse(lat, lon);
        if (rv == null) return (new Place("موقعك الحالي", lat, lon, "موقعك الحالي"), how);
        var (city, country, cc) = rv.Value;
        return (new Place(city, lat, lon, city == "" ? "موقعك الحالي" : city, country, cc), how);
    }
}
