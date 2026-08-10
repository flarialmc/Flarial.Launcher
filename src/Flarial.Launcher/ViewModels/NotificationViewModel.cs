using System;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace Flarial.Launcher.ViewModels;

public class NotificationViewModel : ReactiveObject
{
    public string Message { get; }

    public Signal<RxVoid> CloseRequested { get; } = new();

    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> CloseCommand { get; }

    private readonly Action _onDismissed;

    public NotificationViewModel(string message, Action onDismissed)
    {
        Message = message;
        _onDismissed = onDismissed;
        CloseCommand = ReactiveCommand.Create(() => CloseRequested.OnNext(RxVoid.Default));
    }

    public void CompleteDismiss() => _onDismissed();
}