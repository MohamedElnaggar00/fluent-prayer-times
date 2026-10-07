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
    public string Key => UseCity ? "c:" + Name : $"g:{Lat:F3},{Lon:F3}";
}
public class Settings {
    public Loc Location { get; set; } = new();
    public bool RunAtStartup { get; set; } = false;
    public string Backdrop { get; set; } = "micaalt";   // micaalt | mica | acrylic
    public string Theme { get; set; } = "system";
    public bool WidgetVisible { get; set; } = false;
    public bool WidgetPinned { get; set; } = true;
    public int WidgetX { get; set; } = int.MinValue;
    public int WidgetY { get; set; } = int.MinValue;       // system | light | dark
    public string Dst { get; set; } = "auto";           // auto | off | on
    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static string F => Path.Combine(Dir, "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(F)) ?? new(); } catch { return new(); } }
    public void Save() { try { Directory.CreateDirectory(Dir); File.WriteAllText(F, JsonSerializer.Serialize(this)); } catch { } }
    // Timezone sent to Aladhan. auto = the API's own zone for the place (follows Egypt's DST rules)
    public string? TzParam => Dst switch { "off" => "Etc/GMT-2", "on" => "Etc/GMT-3", _ => null };
}
public record City(string Name, string Ar, double Lat, double Lon);
public record Day(Dictionary<string, string> T, string Hijri);

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
    public static readonly string[] Names = { "الفجر", "الشروق", "الظهر", "العصر", "المغرب", "العشاء" };
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static string CacheFile => Path.Combine(Dir, "cache2.json");
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static readonly Regex Diac = new("[\u064B-\u0652\u0670]");
    static string CK(Settings s, DateTime d) => $"{s.Location.Key}|{s.Dst}|{d:yyyy-MM-dd}";

    static Dictionary<string, Day> Read() {
        try { return JsonSerializer.Deserialize<Dictionary<string, Day>>(File.ReadAllText(CacheFile)) ?? new(); } catch { return new(); }
    }
    static async Task<Day> Fetch(Settings s, DateTime d) {
        var l = s.Location; var inv = CultureInfo.InvariantCulture;
        string tz = s.TzParam != null ? "&timezonestring=" + s.TzParam : "";
        string byCoords = $"https://api.aladhan.com/v1/timings/{d:dd-MM-yyyy}?latitude={l.Lat.ToString(inv)}&longitude={l.Lon.ToString(inv)}&method=5{tz}";
        string byCity = $"https://api.aladhan.com/v1/timingsByCity/{d:dd-MM-yyyy}?city={Uri.EscapeDataString(l.Name)}&country=Egypt&method=5{tz}";
        string json;
        if (l.UseCity) { try { json = await Http.GetStringAsync(byCity); } catch { json = await Http.GetStringAsync(byCoords); } }
        else json = await Http.GetStringAsync(byCoords);
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var t = data.GetProperty("timings"); var r = new Dictionary<string, string>();
        foreach (var k in Keys) r[k] = t.GetProperty(k).GetString()![..5];
        var h = data.GetProperty("date").GetProperty("hijri");
        string hij = $"{h.GetProperty("day").GetString()!.TrimStart('0')} {Diac.Replace(h.GetProperty("month").GetProperty("ar").GetString()!, "")} {h.GetProperty("year").GetString()} هـ";
        return new Day(r, hij);
    }
    static async Task<Day> Get(Settings s, DateTime d, Dictionary<string, Day> cache) {
        var k = CK(s, d); if (cache.TryGetValue(k, out var c)) return c;
        var r = await Fetch(s, d); cache[k] = r; return r;
    }
    public record Result(Day? Today, Day? Tomorrow, bool Offline);
    public static async Task<Result> Load(Settings s) {
        var cache = Read(); var now = DateTime.Now; var tom = now.Date.AddDays(1);
        Day? today = null, tomorrow = null; bool off = false;
        try { today = await Get(s, now, cache); } catch { off = true; }
        try { tomorrow = await Get(s, tom, cache); } catch { cache.TryGetValue(CK(s, tom), out tomorrow); }
        var keep = cache.Where(kv => kv.Key == CK(s, now) || kv.Key == CK(s, tom)).ToDictionary(kv => kv.Key, kv => kv.Value);
        try { Directory.CreateDirectory(Dir); File.WriteAllText(CacheFile, JsonSerializer.Serialize(keep)); } catch { }
        return new(today, tomorrow, off);
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
        if (s.TotalHours >= 1) return ($"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}", "ساعة");
        if (s.TotalMinutes >= 1) return ($"{s.Minutes:00}:{s.Seconds:00}", "دقيقة");
        return ($"{s.Seconds:00}", "ثانية");
    }
    public static string Fmt(TimeSpan s) { if (s < TimeSpan.Zero) s = TimeSpan.Zero; return $"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}"; }

    // ---- location services ----
    public record Place(string Label, double Lat, double Lon);
    public static async Task<List<Place>> Search(string q) {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(q)}&count=10&language=ar&format=json&countryCode=EG";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var res = new List<Place>();
        if (doc.RootElement.TryGetProperty("results", out var arr))
            foreach (var e in arr.EnumerateArray()) {
                string n = e.GetProperty("name").GetString() ?? "";
                string a = e.TryGetProperty("admin1", out var ad) ? ad.GetString() ?? "" : "";
                res.Add(new Place(a == "" ? n : $"{n} - {a}", e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble()));
            }
        return res;
    }
    // Windows: real location via the OS (Wi-Fi/GPS) using PowerShell GeoCoordinateWatcher; fallback: IP-based
    public static async Task<(Place? p, string how)> Detect() {
        if (OperatingSystem.IsWindows()) {
            try {
                const string ps = "Add-Type -AssemblyName System.Device; $w=New-Object System.Device.Location.GeoCoordinateWatcher; $w.Start(); $i=0; while(($w.Status -ne 'Ready') -and ($i -lt 60)){Start-Sleep -Milliseconds 250;$i++}; $l=$w.Position.Location; if($l.IsUnknown){exit 2}; [string]::Format([cultureinfo]::InvariantCulture,'{0},{1}',$l.Latitude,$l.Longitude)";
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + ps.Replace("\"", "\\\"") + "\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var pr = System.Diagnostics.Process.Start(psi)!;
                var outp = await pr.StandardOutput.ReadToEndAsync(); await pr.WaitForExitAsync();
                var parts = outp.Trim().Split(',');
                if (pr.ExitCode == 0 && parts.Length == 2)
                    return (new Place("موقعك الحالي", double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture)), "نظام الموقع في ويندوز");
            } catch { }
        }
        try {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://ipwho.is/"));
            var r = doc.RootElement;
            if (r.GetProperty("success").GetBoolean())
                return (new Place("موقعك (تقريبي)", r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble()), "عنوان IP (تقريبي)");
        } catch { }
        return (null, "");
    }
}
