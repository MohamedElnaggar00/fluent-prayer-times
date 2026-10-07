using Microsoft.UI.Xaml;

namespace FluentPrayerTimes;

public partial class App : Application
{
    public static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentPrayerTimes");
    static Mutex? _mutex;
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
        _mutex = new Mutex(true, "FluentPrayerTimes.SingleInstance", out bool created);
        if (!created) { Environment.Exit(0); return; }
        bool hidden = Environment.GetCommandLineArgs().Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        _window = new MainWindow(hidden);
    }
}
