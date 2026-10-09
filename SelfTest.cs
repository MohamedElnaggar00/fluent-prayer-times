using System.Text.Json;
namespace FluentPrayerTimes;

/// <summary>CI-only logic checks (--selftest). Writes selftest.log and exits 0 on success, 1 on failure.</summary>
static class SelfTest
{
    public static void Run()
    {
        var log = new List<string>(); int fails = 0;
        void Check(string name, bool ok) { log.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
        try
        {
            var s = new Settings();
            var t = new Dictionary<string, string> { ["Fajr"] = "04:00", ["Sunrise"] = "05:30", ["Dhuhr"] = "12:00", ["Asr"] = "15:30", ["Maghrib"] = "18:00", ["Isha"] = "19:30" };
            var tom = new Dictionary<string, string>(t);
            var day = new DateTime(2026, 10, 9);
            Times.Disp D(string hm, int sec = 0) => Times.State(day.Date.AddHours(int.Parse(hm[..2])).AddMinutes(int.Parse(hm[3..])).AddSeconds(sec), t, tom, s)!;
            Check("defaults Fajr20 Dhuhr20 Asr20 Maghrib15 Isha20", s.IqamaFor(0) == 20 && s.IqamaFor(2) == 20 && s.IqamaFor(3) == 20 && s.IqamaFor(4) == 15 && s.IqamaFor(5) == 20);
            Check("before Dhuhr: next, Dhuhr", D("11:00").Phase == Times.Phase.Next && D("11:00").Idx == 2);
            Check("11:00 -> 01:00", Times.FmtHM(D("11:00").Left) == "01:00");
            Check("11:59:30 -> 00:01 (no seconds, rounds up)", Times.FmtHM(D("11:59", 30).Left) == "00:01");
            Check("12:00:00 adhan", D("12:00").Phase == Times.Phase.Adhan);
            Check("12:00:59 adhan", D("12:00", 59).Phase == Times.Phase.Adhan);
            Check("12:01:00 iqama countdown 19:00", D("12:01").Phase == Times.Phase.Iqama && Times.FmtMS(D("12:01").Left) == "19:00");
            Check("12:19:30 iqama 00:30", Times.FmtMS(D("12:19", 30).Left) == "00:30");
            Check("12:20:00 iqama reached", D("12:20").Phase == Times.Phase.IqamaNow);
            Check("12:20:59 iqama reached", D("12:20", 59).Phase == Times.Phase.IqamaNow);
            Check("12:21:00 back to next (Asr)", D("12:21").Phase == Times.Phase.Next && D("12:21").Idx == 3);
            Check("Maghrib uses 15 min", D("18:15").Phase == Times.Phase.IqamaNow && D("18:14").Phase == Times.Phase.Iqama);
            Check("Sunrise has no adhan/iqama", D("05:30").Phase == Times.Phase.Next);
            Check("after Isha iqama -> tomorrow Fajr", D("20:00").Phase == Times.Phase.Next && D("20:00").Tomorrow && D("20:00").Idx == 0);
            s.IqamaMin = new[] { 30, 0, 25, 20, 10, 40 };
            Check("custom iqama per prayer", s.IqamaFor(0) == 30 && s.IqamaFor(2) == 25 && s.IqamaFor(4) == 10 && s.IqamaFor(5) == 40);
            Check("custom Dhuhr 25: 12:24 iqama, 12:25 now", D("12:24").Phase == Times.Phase.Iqama && D("12:25").Phase == Times.Phase.IqamaNow);
            var round = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s));
            Check("settings round trip", round != null && round.IqamaFor(0) == 30 && round.AdhanSound == "default");
            Check("old settings.json without new fields", JsonSerializer.Deserialize<Settings>("{\"NotifyAdhan\":true}")!.IqamaFor(4) == 15);

            Check("minutes only, partial minute rounds up", Times.FmtMinutes(TimeSpan.FromSeconds(30)) == "1" && Times.FmtMinutes(TimeSpan.FromMinutes(13)) == "13");
            Check("location method starts automatic", s.CalculationMethod == -1 && !Times.CalculationQuery(s).Contains("method="));
            var oldKey = Times.CacheKey(s, day);
            s.CalculationMethod = 5; s.AsrSchool = 1; s.HighLatitude = 0;
            Check("calculation overrides transmitted", Times.CalculationQuery(s).Contains("method=5") && Times.CalculationQuery(s).Contains("school=1") && Times.CalculationQuery(s).Contains("latitudeAdjustmentMethod=0"));
            Check("method changes invalidate cache", oldKey != Times.CacheKey(s, day));
            s.CalculationMethod = 99; s.FajrAngle = 19.5; s.IshaAngle = 17.5;
            Check("custom angles invariant", Times.CalculationQuery(s).Contains("methodSettings=19.5,null,17.5"));
            Check("custom settings round trip", JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!.FajrAngle == 19.5);
            JsonElement Rel(string json) => JsonDocument.Parse(json).RootElement;
            string Rj(string tag, bool draft, bool pre, string asset) => "{\"draft\":" + (draft ? "true" : "false") + ",\"prerelease\":" + (pre ? "true" : "false") + ",\"tag_name\":\"" + tag + "\",\"assets\":[{\"name\":\"" + asset + "\",\"browser_download_url\":\"https://github.com/MohamedElnaggar00/fluent-prayer-times/releases/download/" + tag + "/" + asset + "\"}]}";
            Check("newer stable release gives installer url", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, false, "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) != null);
            Check("runtime-dependent installer accepted", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, false, "FluentPrayerTimes-v9.0.0-installer-.NET-runtime-dependent-win-x64.exe")), new Version(1, 1, 1)) != null);
            Check("same version gives none", MainWindow.UpdateDownload(Rel(Rj("v1.1.1", false, false, "FluentPrayerTimes-v1.1.1-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("older gives none", MainWindow.UpdateDownload(Rel(Rj("v1.0.0", false, false, "FluentPrayerTimes-v1.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("prerelease ignored", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, true, "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("draft ignored", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", true, false, "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            bool threw = false; try { MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, false, "other.zip")), new Version(1, 1, 1)); } catch (InvalidOperationException) { threw = true; }
            Check("missing installer asset reports error", threw);
        }
        catch (Exception ex) { log.Add("EXCEPTION " + ex); fails++; }
        try { Directory.CreateDirectory(App.AppData); File.WriteAllLines(Path.Combine(App.AppData, "selftest.log"), log); } catch { }
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
