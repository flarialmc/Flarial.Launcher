using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flarial.Runtime.Services;
using Flarial.Launcher.Controls;
using Flarial.Runtime.Unmanaged;

namespace Flarial.Launcher.Views;

public sealed partial class HomeView : UserControl
{
    static readonly Cursor s_cursor = new(StandardCursorType.Hand);

    public HomeView()
    {
        InitializeComponent();
    }

    async void OnInitialized(object? sender, EventArgs args)
    {
        Initialized -= OnInitialized;

        _ = Task.Run(async () =>
        {
            foreach (var promotion in await PromotionService.GetAsync()) Dispatcher.Post(async () =>
            {
                if (await promotion.GetImageAsync() is not { } bytes)
                    return;

                try
                {
                    using MemoryStream stream = new(bytes, false);
                    var brush = new ImageBrush { Stretch = Stretch.UniformToFill, Source = new Bitmap(stream) };
                    Control image = SponsorAnalyticsService.TryIdentify(promotion.Uri, promotion.CampaignId, out var sponsor, out var campaign)
                        ? new SponsorBanner(sponsor, campaign) { Background = brush }
                        : new Border { CornerRadius = new CornerRadius(5), Background = brush };
                    image.Width = 320 * 0.8;
                    image.Height = 50 * 0.8;
                    image.Cursor = s_cursor;
                    image.Tag = promotion.Uri;
                    image.PointerPressed += OnPointerPressed;
                    RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                    Promotions.Children.Add(image);
                }
                catch (Exception) { }
            }, DispatcherPriority.Background);
        });
    }

    static void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed) return;
        var file = (sender as Control)?.Tag as string;
        if (file is null) return;
        NativeMethods.ShellExecute(file);
        if (sender is SponsorBanner banner) banner.RecordClick();
    }
}