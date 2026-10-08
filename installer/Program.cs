using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace FluentPrayerTimesSetup;

static class Program
{
    const string Product = "Fluent Prayer Times";
    static string Target => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FluentPrayerTimes");
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [STAThread] static void Main(string[] args)
    {
        if (args.Contains("--uninstall"))
        {
            if (MessageBox.Show("هل تريد إزالة برنامج مواقيت الصلاة؟ ستبقى إعداداتك الشخصية محفوظة.", Product, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            // Run the removal from a temporary copy so the installed executable is not locked.
            string copy = Path.Combine(Path.GetTempPath(), "FluentPrayerTimes-Uninstall-" + Guid.NewGuid() + ".exe");
            File.Copy(Environment.ProcessPath!, copy);
            Process.Start(new ProcessStartInfo(copy, "--remove") { UseShellExecute = true });
            return;
        }
        if (args.Contains("--remove"))
        {
            try
            {
                StopApp();
                Directory.Delete(Target, true);
                string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product + ".lnk");
                if (File.Exists(shortcut)) File.Delete(shortcut);
                string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Product + ".lnk");
                if (File.Exists(desk)) File.Delete(desk);
                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FluentPrayerTimes", false);
                MessageBox.Show("تمت إزالة البرنامج. لم تُحذف إعداداتك الشخصية.", Product);
            }
            catch (Exception e) { MessageBox.Show("تعذرت الإزالة: " + e.Message, Product); }
            return;
        }
        if (args.Contains("--verify-install")) { Install(_ => { }); return; }
        var app = new Application();
        var window = new Window { Title = Product + " - تثبيت", Width = 490, Height = 615, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(243,243,246)), FontFamily = new FontFamily("Segoe UI"), FlowDirection = FlowDirection.RightToLeft };
        window.SourceInitialized += (_, _) => { var h = new WindowInteropHelper(window).Handle; int backdrop = 2, corner = 2; try { DwmSetWindowAttribute(h, 38, ref backdrop, 4); DwmSetWindowAttribute(h, 33, ref corner, 4); } catch { } };
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock { Text = "عن البرنامج", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,18) });
        var about = new StackPanel { Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center };
        about.Children.Add(new Border { Width = 104, Height = 104, CornerRadius = new CornerRadius(18), Background = new SolidColorBrush(Color.FromRgb(0,103,192)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,14), Child = new Image { Width = 72, Height = 72, Source = new BitmapImage(new Uri("pack://application:,,,/logo.png")) } });
        about.Children.Add(Text("برنامج مواقيت الصلاة", 24));
        about.Children.Add(Text("الإصدار 1.2.0", 14));
        about.Children.Add(Text("brought to you by app.instinct AI", 14));
        about.Children.Add(Text("المطور: محمد النجار", 14));
        panel.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(224,224,230)), BorderThickness = new Thickness(1), Child = about });
        panel.Children.Add(new TextBlock { Text = "سيُثبَّت البرنامج في Program Files. تُفحَص المتطلبات وتُنزَّل من Microsoft عند الحاجة. يلزم اتصال بالإنترنت للمتطلبات الناقصة.", TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0,18,0,12), Foreground = Brushes.DimGray });
        var status = new TextBlock { Text = "جاهز للتثبيت", TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0,0,0,10) };
        panel.Children.Add(status);
        var progress = new ProgressBar { Height = 6, Visibility = Visibility.Collapsed, IsIndeterminate = true, Margin = new Thickness(0,0,0,12), Foreground = new SolidColorBrush(Color.FromRgb(22,163,74)), Background = new SolidColorBrush(Color.FromRgb(222,222,228)), BorderThickness = new Thickness(0) };
        panel.Children.Add(progress);
        var install = new Button { Content = "install now", Height = 44, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, Background = new SolidColorBrush(Color.FromRgb(76,194,255)), BorderThickness = new Thickness(0) };
        panel.Children.Add(install);
        var done = new StackPanel { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center };
        done.Children.Add(new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = new SolidColorBrush(Color.FromRgb(22,163,74)), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe Fluent Icons"), FontSize = 22, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        done.Children.Add(new TextBlock { Text = "تم التثبيت", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,8,0,0) });
        panel.Children.Add(done);
        Action showDone = () => { progress.Visibility = Visibility.Collapsed; install.Visibility = Visibility.Collapsed; status.Visibility = Visibility.Collapsed; done.Visibility = Visibility.Visible; };
        bool busy = false;
        window.Closing += (_, e) => { if (busy) e.Cancel = true; };
        install.Click += async (_, _) =>
        {
            if (!install.IsEnabled) return;
            busy = true; install.IsEnabled = false; progress.Visibility = Visibility.Visible;
            try
            {
                await Task.Run(() => Install(text => window.Dispatcher.Invoke(() => status.Text = text)));
                showDone();
                LaunchApp();
            }
            catch (Exception e) { status.Text = "تعذر التثبيت: " + e.Message + " يمكنك المحاولة مجددًا."; install.IsEnabled = true; progress.Visibility = Visibility.Collapsed; }
            finally { busy = false; }
        };
        window.Content = panel;
        if (args.Length == 2 && (args[0] == "--preview" || args[0] == "--preview-busy" || args[0] == "--preview-done"))
        {
            if (args[0] == "--preview-busy") { install.IsEnabled = false; progress.Visibility = Visibility.Visible; status.Text = "جارٍ تثبيت ملفات البرنامج..."; }
            if (args[0] == "--preview-done") showDone();
            window.Content = null;
            var preview = new Border { Width = 490, Height = 580, Background = window.Background, Child = panel, FlowDirection = FlowDirection.RightToLeft };
            preview.Measure(new Size(490,580)); preview.Arrange(new Rect(0,0,490,580)); preview.UpdateLayout();
            var bitmap = new RenderTargetBitmap(490,580,96,96,PixelFormats.Pbgra32);
            bitmap.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(args[1]); encoder.Save(file); return;
        }
        app.Run(window);
    }
    /// <summary>Starts the installed app de-elevated (through Explorer) so it does not run as administrator.</summary>
    static void LaunchApp()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Path.Combine(Target, "FluentPrayerTimes.exe") + "\"") { UseShellExecute = true }); } catch { }
    }
    static TextBlock Text(string text, double size) => new() { Text = text, FontSize = size, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0,4,0,4) };
    static void StopApp() { foreach (var p in Process.GetProcessesByName("FluentPrayerTimes")) { p.Kill(); p.WaitForExit(10000); p.Dispose(); } }
    static string Run(string exe, string arguments)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        p.WaitForExit(); Task.WaitAll(output, error);
        if (p.ExitCode != 0 && p.ExitCode != 3010) throw new Exception("فشل تثبيت أحد المتطلبات (" + p.ExitCode + "). " + error.Result);
        return output.Result;
    }
    static bool DotNetPresent()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.NETCore.App");
        return Directory.Exists(root) && Directory.GetDirectories(root).Any(d => Version.TryParse(Path.GetFileName(d), out var v) && v.Major == 8);
    }
    static bool VcPresent() => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64")?.GetValue("Installed") is int installed && installed == 1;
    static bool WarPresent() => Run("powershell.exe", "-NoProfile -NonInteractive -Command \"[bool](Get-AppxPackage -Name Microsoft.WindowsAppRuntime.1.6 | Where-Object { $_.Architecture -eq 'X64' -and $_.Status -eq 'Ok' })\"").Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
    static void Dependency(string url, string arguments)
    {
        string file = Path.Combine(Path.GetTempPath(), "FluentPrayerTimes-" + Guid.NewGuid() + ".exe");
        try
        {
            using (var source = Http.GetStreamAsync(url).GetAwaiter().GetResult()) using (var dest = File.Create(file)) source.CopyTo(dest);
            // Refuse to run an unsigned download or a publisher other than Microsoft.
            string cmd = "$s=Get-AuthenticodeSignature -LiteralPath '" + file.Replace("'", "''") + "'; if($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { exit 1 }";
            Run("powershell.exe", "-NoProfile -NonInteractive -Command \"" + cmd + "\"");
            Run(file, arguments);
        }
        finally { try { File.Delete(file); } catch { } }
    }
    static void Install(Action<string> status)
    {
        if (!Environment.Is64BitOperatingSystem) throw new Exception("يتطلب البرنامج Windows x64.");
        status("جارٍ فحص المتطلبات...");
        if (!DotNetPresent()) { status("جارٍ تنزيل وتثبيت .NET 8..."); Dependency("https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe", "/install /quiet /norestart"); if (!DotNetPresent()) throw new Exception("لم يكتمل تثبيت .NET 8."); }
        if (!VcPresent()) { status("جارٍ تنزيل وتثبيت Visual C++..."); Dependency("https://aka.ms/vc14/vc_redist.x64.exe", "/install /quiet /norestart"); if (!VcPresent()) throw new Exception("لم يكتمل تثبيت Visual C++."); }
        if (!WarPresent()) { status("جارٍ تنزيل وتثبيت Windows App Runtime 1.6..."); Dependency("https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe", "--quiet"); if (!WarPresent()) throw new Exception("لم يكتمل تثبيت Windows App Runtime 1.6."); }
        status("جارٍ تثبيت ملفات البرنامج...");
        StopApp(); Directory.CreateDirectory(Target);
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(n => n.EndsWith(".payload.zip"))) ?? throw new Exception("حزمة البرنامج غير موجودة.");
        using var zip = new ZipArchive(payload);
        zip.ExtractToDirectory(Target, true);
        File.Copy(Environment.ProcessPath!, Path.Combine(Target, "Uninstall.exe"), true);
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product + ".lnk");
        string escaped = Target.Replace("'", "''");
        Run("powershell.exe", "-NoProfile -NonInteractive -Command \"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + shortcut.Replace("'", "''") + "');$s.TargetPath='" + escaped + "\\FluentPrayerTimes.exe';$s.WorkingDirectory='" + escaped + "';$s.IconLocation='" + escaped + "\\app.ico';$s.Save()\"");
        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Product + ".lnk");
        Run("powershell.exe", "-NoProfile -NonInteractive -Command \"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + desktop.Replace("'", "''") + "');$s.TargetPath='" + escaped + "\\FluentPrayerTimes.exe';$s.WorkingDirectory='" + escaped + "';$s.IconLocation='" + escaped + "\\app.ico';$s.Save()\"");
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FluentPrayerTimes");
        key.SetValue("DisplayName", Product); key.SetValue("DisplayVersion", "1.2.0"); key.SetValue("Publisher", "Mohamed Elnaggar"); key.SetValue("InstallLocation", Target);
        key.SetValue("DisplayIcon", Path.Combine(Target, "app.ico")); key.SetValue("UninstallString", "\"" + Path.Combine(Target, "Uninstall.exe") + "\" --uninstall");
        key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
    }
}
