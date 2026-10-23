using System.Net.Http;
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
            // GitHub reports download URLs with the repository's canonical casing (Fluent-Prayer-Times), not the lowercase name the app queries with.
            string Rc(string tag, string asset) => "{\"draft\":false,\"prerelease\":false,\"tag_name\":\"" + tag + "\",\"assets\":[{\"name\":\"" + asset + "\",\"browser_download_url\":\"https://github.com/MohamedElnaggar00/Fluent-Prayer-Times/releases/download/" + tag + "/" + asset + "\"}]}";
            Check("update found when GitHub reports canonical repo casing", MainWindow.UpdateDownload(Rel(Rc("v9.0.0", "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == "https://github.com/MohamedElnaggar00/Fluent-Prayer-Times/releases/download/v9.0.0/FluentPrayerTimes-v9.0.0-installer-win-x64.exe");
            bool foreign = false; try { MainWindow.UpdateDownload(Rel("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v9.0.0\",\"assets\":[{\"name\":\"FluentPrayerTimes-v9.0.0-installer-win-x64.exe\",\"browser_download_url\":\"https://github.com/someone-else/other/releases/download/v9.0.0/FluentPrayerTimes-v9.0.0-installer-win-x64.exe\"}]}"), new Version(1, 1, 1)); } catch (InvalidOperationException) { foreign = true; }
            Check("foreign repository download url is still rejected", foreign);
            var withDigest = MainWindow.UpdateAssetFor(Rel("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v9.0.0\",\"assets\":[{\"name\":\"FluentPrayerTimes-v9.0.0-installer-win-x64.exe\",\"size\":1234,\"digest\":\"sha256:ABCDEF\",\"browser_download_url\":\"https://github.com/MohamedElnaggar00/Fluent-Prayer-Times/releases/download/v9.0.0/FluentPrayerTimes-v9.0.0-installer-win-x64.exe\"}]}"), new Version(1, 1, 1));
            Check("update asset carries size and sha256 digest", withDigest != null && withDigest.Size == 1234 && withDigest.Sha256 == "ABCDEF");
            Check("download host: github.com and its asset hosts only", UpdateInstaller.IsTrustedSource(new Uri("https://github.com/a/b")) && UpdateInstaller.IsTrustedSource(new Uri("https://release-assets.githubusercontent.com/x")) && !UpdateInstaller.IsTrustedSource(new Uri("https://evil.example/x")) && !UpdateInstaller.IsTrustedSource(new Uri("http://github.com/a")) && !UpdateInstaller.IsTrustedSource(new Uri("https://githubusercontent.com.evil.example/x")));
            {
                var tmp = Path.Combine(Path.GetTempPath(), "fpt-verify-" + Guid.NewGuid().ToString("N"));
                var good = new byte[4096]; good[0] = (byte)'M'; good[1] = (byte)'Z'; File.WriteAllBytes(tmp, good);
                var goodHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(good));
                bool Fails(UpdateAsset a) { try { UpdateInstaller.Verify(tmp, a); return false; } catch (InvalidDataException) { return true; } }
                UpdateInstaller.Verify(tmp, new UpdateAsset("u", "n.exe", 4096, goodHash.ToLowerInvariant()));
                Check("installer verification accepts matching size and checksum", true);
                Check("installer verification rejects wrong size", Fails(new UpdateAsset("u", "n.exe", 4097, null)));
                Check("installer verification rejects wrong checksum", Fails(new UpdateAsset("u", "n.exe", 4096, new string('0', 64))));
                var bad = (byte[])good.Clone(); bad[0] = 0; File.WriteAllBytes(tmp, bad);
                Check("installer verification rejects non-executable file", Fails(new UpdateAsset("u", "n.exe", 4096, null)));
                File.Delete(tmp);
            }
            Check("same version gives none", MainWindow.UpdateDownload(Rel(Rj("v1.1.1", false, false, "FluentPrayerTimes-v1.1.1-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("older gives none", MainWindow.UpdateDownload(Rel(Rj("v1.0.0", false, false, "FluentPrayerTimes-v1.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("prerelease ignored", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, true, "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            Check("draft ignored", MainWindow.UpdateDownload(Rel(Rj("v9.0.0", true, false, "FluentPrayerTimes-v9.0.0-installer-win-x64.exe")), new Version(1, 1, 1)) == null);
            var eg = new Settings(); var us = new Settings { Location = new Loc { Cc = "US" }, Dst = "off" };
            Check("DST card only for Egypt", eg.DstApplies() && !us.DstApplies() && us.TzParam == null);
            // Local calculation: same numbers as Wathkkir for the same place (30.8619 N, 29.5845 E, Egyptian method, Cairo time +3 until 29 Oct).
            string Row(DateTime dd, int off, int method = 5) { var rr = SolarCalc.Compute(dd, 30.8619, 29.5845, off, method, null, null, 1); return rr == null ? "null" : string.Join(" ", Times.Keys.Select(k => rr[k])); }
            Check("local calc 23 Oct matches Wathkkir", Row(new DateTime(2026, 10, 23), 3) == "05:42 07:09 12:46 15:57 18:23 19:41");
            Check("local calc 24 Oct", Row(new DateTime(2026, 10, 24), 3) == "05:42 07:10 12:46 15:57 18:22 19:40");
            Check("local calc 26 Oct", Row(new DateTime(2026, 10, 26), 3) == "05:44 07:11 12:46 15:55 18:20 19:38");
            var ns = new Settings { AdhanSound = "none", IqamaSound = "none" };
            var nr = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(ns))!;
            Check("no-sound choice persists", nr.AdhanSound == "none" && nr.IqamaSound == "none");
            Check("no-sound plays no audio file", !Sounds.Available("adhan", "none") && !Sounds.Available("iqama", "none"));
            Check("default sound setting unchanged", new Settings().AdhanSound == "default" && new Settings().IqamaSound == "default");
            var nt = new Settings { AdhanNotify = false };
            var ntr = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(nt))!;
            Check("adhan and iqama notifications are independent", !ntr.AdhanNotify && ntr.IqamaNotify && new Settings().AdhanNotify && new Settings().IqamaNotify);
            Check("old settings.json keeps both notifications on and no location lookup", JsonSerializer.Deserialize<Settings>("{\"NotifyAdhan\":true}") is { AdhanNotify: true, IqamaNotify: true, LocationPending: false });
            Check("new Settings() does not ask for a location lookup by itself", !new Settings().LocationPending);
            Check("local calc unsupported method falls back", Row(new DateTime(2026, 10, 23), 3, 7) == "null");
            string RowX(DateTime dd, double la, double lo, int off, int method, int asr, int hl) { var rr = SolarCalc.Compute(dd, la, lo, off, method, null, null, asr, hl); return rr == null ? "null" : string.Join(" ", Times.Keys.Select(k => rr[k])); }
            Check("local calc Hanafi Asr (Cairo 23 Oct)", RowX(new DateTime(2026, 10, 23), 30.0444, 31.2357, 3, 5, 2, 3) == "05:35 07:02 12:39 16:39 18:17 19:35");
            Check("local calc high latitude angle rule (Oslo 21 Jun)", RowX(new DateTime(2026, 6, 21), 59.9139, 10.7522, 2, 3, 1, 3) == "02:21 03:53 13:19 18:00 22:44 00:12");
            Check("local calc high latitude middle of night", RowX(new DateTime(2026, 6, 21), 59.9139, 10.7522, 2, 3, 1, 1) == "01:19 03:53 13:19 18:00 22:44 01:19");
            Check("local calc high latitude one seventh", RowX(new DateTime(2026, 6, 21), 59.9139, 10.7522, 2, 3, 1, 2) == "03:09 03:53 13:19 18:00 22:44 23:28");
            Check("local calc no high-latitude rule falls back", RowX(new DateTime(2026, 6, 21), 59.9139, 10.7522, 2, 3, 1, 0) == "null");
            Check("local calc polar day falls back", RowX(new DateTime(2026, 6, 21), 69.65, 18.96, 2, 3, 1, 3) == "null");
            Check("local calc mid-latitude matches Aladhan (50N Dec)", RowX(new DateTime(2026, 12, 21), 50.0, 8.0, 1, 3, 1, 3) == "06:24 08:23 12:26 14:12 16:28 18:21");
            var tzs = new Settings { Location = new Loc { Cc = "US" }, Dst = "off" };
            Check("offset Cairo before DST starts (23 Apr)", Times.OffsetHours(tzs, "Africa/Cairo", new DateTime(2026, 4, 23)) == 2);
            Check("offset Cairo after DST starts (24 Apr)", Times.OffsetHours(tzs, "Africa/Cairo", new DateTime(2026, 4, 24)) == 3);
            Check("offset Cairo last DST day (29 Oct)", Times.OffsetHours(tzs, "Africa/Cairo", new DateTime(2026, 10, 29)) == 3);
            Check("offset Cairo after DST ends (30 Oct)", Times.OffsetHours(tzs, "Africa/Cairo", new DateTime(2026, 10, 30)) == 2);
            var mz = new Settings { TimeZoneOverride = "Etc/GMT-3" };
            Check("manual UTC+3 overrides zone", Times.OffsetHours(mz, "Africa/Cairo", new DateTime(2026, 12, 1)) == 3);
            mz.TimeZoneOverride = "Etc/GMT+5";
            Check("manual UTC-5 offset", Times.OffsetHours(mz, null, new DateTime(2026, 12, 1)) == -5);
            Check("DST transition changes local times by an hour", RowX(new DateTime(2026, 10, 30), 30.8619, 29.5845, 2, 5, 1, 3).StartsWith("04:") && RowX(new DateTime(2026, 10, 29), 30.8619, 29.5845, 3, 5, 1, 3).StartsWith("05:"));
            bool threw = false; try { MainWindow.UpdateDownload(Rel(Rj("v9.0.0", false, false, "other.zip")), new Version(1, 1, 1)); } catch (InvalidOperationException) { threw = true; }
            Check("missing installer asset reports error", threw);
        }
        catch (Exception ex) { log.Add("EXCEPTION " + ex); fails++; }
        try { Directory.CreateDirectory(App.AppData); File.WriteAllLines(Path.Combine(App.AppData, "selftest.log"), log); } catch { }
        Environment.Exit(fails == 0 ? 0 : 1);
    }

    /// <summary>CI-only end-to-end check (--update-e2e): downloads the real published installer from GitHub through the app's own download code and checks progress, cancel, checksum failure and host rejection. Nothing is installed or launched.</summary>
    public static void RunUpdateE2E()
    {
        var log = new List<string>(); int fails = 0;
        void Check(string name, bool ok) { log.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }
        try { Task.Run(async () =>
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FluentPrayerTimes-ci");
            var json = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/MohamedElnaggar00/fluent-prayer-times/releases/latest"));
            var asset = MainWindow.UpdateAssetFor(json.RootElement, new Version(1, 3, 3));
            Check("live release offers an installer to 1.3.3", asset != null && asset.Sha256 != null && asset.Size > 1_000_000);
            if (asset == null) return;
            log.Add("asset " + asset.Name + " " + asset.Size + " bytes");
            var steps = new List<double>();
            var file = await UpdateInstaller.DownloadAsync(asset, p => steps.Add(p), CancellationToken.None);
            Check("download finished and file exists", File.Exists(file) && new FileInfo(file).Length == asset.Size);
            Check("progress reported and reached 100", steps.Count > 5 && steps[^1] >= 99.9 && steps.Zip(steps.Skip(1), (a, b) => b >= a).All(x => x));
            Check("downloaded installer passes verification", Verifies(file, asset));
            Check("no partial file left after success", !Directory.GetFiles(UpdateInstaller.Dir, "*.part").Any());
            using var cts = new CancellationTokenSource();
            bool canceled = false;
            try { await UpdateInstaller.DownloadAsync(asset, p => { if (p > 3) cts.Cancel(); }, cts.Token); } catch (OperationCanceledException) { canceled = true; }
            Check("cancel stops the download", canceled);
            Check("cancel leaves no files behind", Directory.GetFiles(UpdateInstaller.Dir).Length == 0);
            bool badHash = false;
            try { await UpdateInstaller.DownloadAsync(asset with { Sha256 = new string('0', 64) }, _ => { }, CancellationToken.None); } catch (InvalidDataException) { badHash = true; }
            Check("wrong checksum is rejected", badHash);
            Check("rejected download leaves no installer behind", Directory.GetFiles(UpdateInstaller.Dir).Length == 0);
            bool badHost = false;
            try { await UpdateInstaller.DownloadAsync(asset with { Url = "https://example.com/" + asset.Name }, _ => { }, CancellationToken.None); } catch (InvalidOperationException) { badHost = true; }
            Check("untrusted host is refused before any download", badHost);
        }).GetAwaiter().GetResult(); }
        catch (Exception ex) { log.Add("EXCEPTION " + ex); fails++; }
        try { Directory.Delete(UpdateInstaller.Dir, true); } catch { }
        try { Directory.CreateDirectory(App.AppData); File.WriteAllLines(Path.Combine(App.AppData, "update-e2e.log"), log); } catch { }
        Environment.Exit(fails == 0 ? 0 : 1);
    }
    static bool Verifies(string file, UpdateAsset asset) { try { UpdateInstaller.Verify(file, asset); return true; } catch { return false; } }
}
