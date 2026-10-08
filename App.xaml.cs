using Microsoft.UI.Xaml;

namespace FluentPrayerTimes;

public partial class App : Application
{
    public static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static Mutex? _mutex;
    /// <summary>CI-only: --preview=next|adhan|iqama|iqamanow and --part=main|widget|hover|hoverstress.</summary>
    public static string? Preview, PreviewPart;
    MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            try { Directory.CreateDirectory(AppData); File.AppendAllText(Path.Combine(AppData, "crash.log"), DateTime.Now + "\n" + e.Exception + "\n\n"); } catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        foreach (var a in Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("--preview=")) Preview = a[10..];
            if (a.StartsWith("--part=")) PreviewPart = a[7..];
        }
        if (Environment.GetCommandLineArgs().Any(a => a == "--selftest")) { SelfTest.Run(); return; }
        _mutex = new Mutex(true, "FluentPrayerTimes.SingleInstance", out bool created);
        if (!created) { Environment.Exit(0); return; }
        bool hidden = Environment.GetCommandLineArgs().Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) || Preview != null;
        _window = new MainWindow(hidden);
    }
}
