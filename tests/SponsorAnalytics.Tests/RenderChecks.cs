using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flarial.Launcher.Controls;
using Flarial.Runtime.Services;

static class RenderChecks
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        // Saturate the sender so render checks cannot send network telemetry.
        var pending = typeof(SponsorAnalyticsService).GetField("s_pending", BindingFlags.Static | BindingFlags.NonPublic)!;
        pending.SetValue(null, 32);
        var recorded = typeof(SponsorBanner).GetField("_recorded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var banner = new SponsorBanner("salad", "launcher-salad") { Width = 256, Height = 40, Background = new ImageBrush { Source = new WriteableBitmap(new PixelSize(320, 50), new Vector(96, 96)) } };
        var host = new Border { Child = banner, IsVisible = false };
        var window = new Window { Width = 400, Height = 200, Content = host };
        window.Show();
        window.Activate();
        Dispatcher.UIThread.RunJobs();

        Check(!(bool)recorded.GetValue(banner)!, "Attachment/download does not count");
        host.IsVisible = false;
        Render(banner);
        Check(!(bool)recorded.GetValue(banner)!, "Hidden ancestor does not count");
        window.WindowState = WindowState.Minimized;
        host.IsVisible = true;
        Render(banner);
        Check(!(bool)recorded.GetValue(banner)!, "Minimized window does not count");
        window.WindowState = WindowState.Normal;
        window.Activate();
        Render(banner);
        Check((bool)recorded.GetValue(banner)!, "Visible active render counts");
        Render(banner);
        Check((int)pending.GetValue(null)! == 32, "Telemetry saturation stays bounded");
        host.Child = null;
        host.Child = banner;
        Check(!(bool)recorded.GetValue(banner)!, "New attachment permits new impression");
        Render(banner);
        Check((bool)recorded.GetValue(banner)!, "Reattached banner counts after render");
        window.Hide();
        host.Child = null;
        host.Child = banner;
        Render(banner);
        Check(!(bool)recorded.GetValue(banner)!, "Hidden window does not count");
        var empty = new SponsorBanner("salad", "launcher-salad");
        host.Child = empty;
        window.Show();
        window.Activate();
        Render(empty);
        Check(!(bool)recorded.GetValue(empty)!, "Missing bitmap does not count");
        window.Close();
        pending.SetValue(null, 0);
        Console.WriteLine("Render visibility, attachment, minimized-window, and capacity checks passed.");
    }
    static void Render(SponsorBanner banner)
    {
        banner.Measure(new Size(256, 40));
        banner.Arrange(new Rect(0, 0, 256, 40));
        using (var context = new DrawingGroup().Open()) banner.Render(context);
        Dispatcher.UIThread.RunJobs();
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
