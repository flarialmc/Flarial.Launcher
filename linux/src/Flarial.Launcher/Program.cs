using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Flarial.Runtime.Game;
using Flarial.Runtime.Linux;
using ReactiveUI.Avalonia;

namespace Flarial.Launcher;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LinuxPlatform.Use();
        Flarial.Launcher.Management.AccountsService.Current = new Flarial.Launcher.Management.LinuxAccountsService();
        using Mutex mutex = new(false, "54874D29-646C-4536-B6D1-8E05053BE00E", out var created);
        if (!created) return;

        Environment.CurrentDirectory = Directory.CreateDirectory(LinuxPlatform.LauncherDataDirectory).FullName;

        if (args.Length > 0 && args[0].StartsWith("--selftest-"))
            Environment.Exit(LinuxPlatform.SelfTestAsync(args[0]["--selftest-".Length..]).GetAwaiter().GetResult());

        for (var index = 0; index < args.Length; index++)
            switch (args[index])
            {
                case "--inject":
                    if (!(index + 1 < args.Length)) continue;
                    Injector.Launch(new(args[index + 1]));
                    return;
            }

        var builder = AppBuilder.Configure<App>();

        builder.UseSkia();
        builder.UseX11();
        builder.UseHarfBuzz();
        builder.UseReactiveUI(static _ => _.WithExceptionHandler(new ExceptionHandler()));

        builder.With(new SkiaOptions { MaxGpuResourceSizeBytes = long.MaxValue });
        builder.With(new CompositionOptions { UseRegionDirtyRectClipping = true });
        builder.With(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });

        builder.StartWithClassicDesktopLifetime(args);
    }
}