using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flarial.Runtime.Services;

namespace Flarial.Launcher.Controls;

internal sealed class SponsorBanner(string sponsorId, string campaignId) : Control
{
    public ImageBrush? Background { get; init; }
    bool _recorded;
    int _attachment;
    Window? _window;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _recorded = false;
        _attachment++;
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is { }) _window.Activated += OnActivated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is { }) _window.Activated -= OnActivated;
        _window = null;
        _attachment++;
        base.OnDetachedFromVisualTree(e);
    }

    void OnActivated(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Background, null, new Rect(Bounds.Size), 5, 5);
        if (_recorded || Background is not ImageBrush { Source: IImage source } ||
            source.Size.Width <= 0 || source.Size.Height <= 0 || !CanCount()) return;
        var attachment = _attachment;
        Dispatcher.UIThread.Post(() =>
        {
            if (_recorded || attachment != _attachment || !CanCount()) return;
            _recorded = true;
            SponsorAnalyticsService.Record(sponsorId, campaignId, "impression");
        }, DispatcherPriority.Background);
    }

    bool CanCount()
    {
        if (_window is not { IsVisible: true, IsActive: true } window || window.WindowState == WindowState.Minimized ||
            Bounds.Width <= 0 || Bounds.Height <= 0) return false;
        for (Visual? visual = this; visual is { }; visual = visual.GetVisualParent())
        {
            if (!visual.IsVisible || visual.Opacity <= 0) return false;
            var position = this.TranslatePoint(default, visual);
            if (position is null) return false;
            if (visual.ClipToBounds && !new Rect(visual.Bounds.Size).Intersects(new Rect(position.Value, Bounds.Size))) return false;
        }
        var origin = this.TranslatePoint(default, window);
        return origin is { } point && new Rect(window.ClientSize).Intersects(new Rect(point, Bounds.Size));
    }

    public void RecordClick() => SponsorAnalyticsService.Record(sponsorId, campaignId, "click");
}
