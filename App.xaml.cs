using Microsoft.UI.Xaml;

namespace FluentPrayerTimes;

public partial class App : Application
{
    public static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static Mutex? _mutex;
    /// <summary>CI-only: --preview=next|adhan|iqama|iqamanow and --part=main|widget|hover|hoverstress.</summary>
    public static string? Preview, PreviewPart, PreviewLang;
    /// <summary>CI-only end-to-end hooks (all null in normal use): --e2e-detect=ok|fail replaces the location lookup result, --e2e-clock=adhan|iqama pins the scheduler clock to Dhuhr, --e2e-log=path records notifications instead of showing them, --e2e-set=Name=value;... overrides notification settings in memory.</summary>
    public static string? E2EDetect, E2EClock, E2ELog, E2ESet;
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
            if (a.StartsWith("--lang=")) PreviewLang = a[7..];
            if (a.StartsWith("--e2e-detect=")) E2EDetect = a[13..];
            if (a.StartsWith("--e2e-clock=")) E2EClock = a[12..];
            if (a.StartsWith("--e2e-log=")) E2ELog = a[10..];
            if (a.StartsWith("--e2e-set=")) E2ESet = a[10..];
        }
        if (Environment.GetCommandLineArgs().Any(a => a == "--selftest")) { SelfTest.Run(); return; }
        if (Environment.GetCommandLineArgs().Any(a => a == "--update-e2e")) { SelfTest.RunUpdateE2E(); return; }
        L.Set(PreviewLang ?? Settings.Load().Lang);
        _mutex = new Mutex(true, "FluentPrayerTimes.SingleInstance", out bool created);
        if (!created) { Environment.Exit(0); return; }
        bool hidden = Environment.GetCommandLineArgs().Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) || Preview != null;
        _window = new MainWindow(hidden);
        StartQuitListener(_window);
    }

    /// <summary>Lets an installer or updater ask the running tray app to exit cleanly (named event, no window needed).</summary>
    static void StartQuitListener(MainWindow window)
    {
        try
        {
            var quit = new EventWaitHandle(false, EventResetMode.ManualReset, "FluentPrayerTimes.Quit");
            quit.Reset();
            new Thread(() => { quit.WaitOne(); window.RequestQuit(); }) { IsBackground = true, Name = "QuitListener" }.Start();
        }
        catch { }
    }
}
