using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace Flarial.Launcher.ViewModels;

public sealed class MessageBoxViewModel : ReactiveObject
{
    private readonly TaskCompletionSource<string> _tcs = new();
    private string? _pendingResult;

    public string Title { get; }
    public string Message { get; }
    public IEnumerable<string> Buttons { get; }
    public Signal<RxVoid> CloseRequested { get; } = new();

    public ReactiveCommand<string, RxVoid> SelectButtonCommand { get; }

    public Task<string> Result => _tcs.Task;

    public MessageBoxViewModel(string title, string message, IEnumerable<string> buttons)
    {
        Title = title;
        Message = message;
        Buttons = buttons;

        SelectButtonCommand = ReactiveCommand.Create<string>(button =>
        {
            _pendingResult = button;
            CloseRequested.OnNext(RxVoid.Default);
        });
    }

    public void CompleteClose() => _tcs.TrySetResult(_pendingResult ?? string.Empty);
}