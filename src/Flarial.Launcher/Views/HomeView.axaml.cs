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

                using MemoryStream stream = new(bytes, false);

                Border image = new()
                {
                    Tag = promotion,
                    Width = 320 * 0.8,
                    Height = 50 * 0.8,
                    Cursor = s_cursor,
                    CornerRadius = new CornerRadius(5),
                    Background = new ImageBrush { Stretch = Stretch.UniformToFill, Source = new Bitmap(stream) }
                };

                image.PointerPressed += OnPointerPressed;
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                Promotions.Children.Add(image);
            }, DispatcherPriority.Background);
        });
    }

    static async void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is Control { Tag: Promotion promotion } control)
        {
            control.IsEnabled = false; try
            {
                NativeMethods.ShellExecute(promotion.Uri);
                _ = promotion.OnClickAsync();
            }
            finally { control.IsEnabled = true; }
        }
    }
}