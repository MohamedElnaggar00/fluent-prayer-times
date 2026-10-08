using Windows.Media.Core;
using Windows.Media.Playback;
namespace FluentPrayerTimes;

/// <summary>Adhan / iqama audio. "default" = the normal Windows notification sound.
/// "full" plays {kind}-full.* from the Audio folder. "short" plays {kind}-short.* if present, otherwise the full file until a short file is supplied.</summary>
static class Sounds
{
    public const int ShortSeconds = 16;
    static MediaPlayer? _p;
    static System.Threading.Timer? _cut;
    static readonly string[] Ext = { ".mp3", ".wav", ".m4a", ".wma", ".aac" };
    public static string Dir => Path.Combine(AppContext.BaseDirectory, "Audio");
    static string? File_(string name)
    {
        foreach (var e in Ext)
        {
            var f = Path.Combine(Dir, name + e);
            if (File.Exists(f)) return f;
        }
        return null;
    }
    /// <summary>Returns the file to play and whether playback must be cut after ShortSeconds; null for default or a missing file.</summary>
    public static (string path, bool trim)? Find(string kind, string mode)
    {
        if (mode == "full") { var f = File_(kind + "-full"); return f == null ? null : (f, false); }
        if (mode == "short")
        {
            var s = File_(kind + "-short"); if (s != null) return (s, false);
            var f = File_(kind + "-full"); if (f != null) return (f, false);   // no short file yet: play the full one (the user will supply the short files)
        }
        return null;
    }
    public static bool Available(string kind, string mode) => Find(kind, mode) != null;
    public static bool Play(string kind, string mode)
    {
        var r = Find(kind, mode);
        if (r == null) return false;
        try
        {
            Stop();
            _p = new MediaPlayer { Source = MediaSource.CreateFromUri(new Uri(r.Value.path)) };
            _p.Play();
            if (r.Value.trim) _cut = new System.Threading.Timer(_ => Stop(), null, ShortSeconds * 1000, System.Threading.Timeout.Infinite);
            return true;
        }
        catch { return false; }
    }
    public static void Stop()
    {
        try { _cut?.Dispose(); } catch { }
        _cut = null;
        try { _p?.Dispose(); } catch { }
        _p = null;
    }
}
