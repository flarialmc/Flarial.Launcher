using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Flarial.Launcher.Management;
using Flarial.Runtime.Unmanaged;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.ViewModels;

public partial class SettingsAccountsViewModel : ViewModelBase
{
    [Reactive] bool _microsoftSignedIn;
    [Reactive] bool _microsoftBusy;
    [Reactive] string _microsoftText = "Not signed in";
    [Reactive] bool _xboxSignedIn;
    [Reactive] bool _xboxBusy;
    [Reactive] string _xboxText = "Not signed in";

    public SettingsGeneralViewModel General { get; }

    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> MicrosoftSignIn { get; }
    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> MicrosoftSignOut { get; }
    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> XboxSignIn { get; }
    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> XboxSignOut { get; }

    readonly MainWindowViewModel _main;
    static IAccountsService Service => AccountsService.Current;

    public SettingsAccountsViewModel(MainWindowViewModel main, SettingsGeneralViewModel general)
    {
        _main = main; General = general;
        MicrosoftSignIn = ReactiveCommand.CreateFromTask(OnMicrosoftSignInAsync);
        MicrosoftSignOut = ReactiveCommand.CreateFromTask(OnMicrosoftSignOutAsync);
        XboxSignIn = ReactiveCommand.CreateFromTask(OnXboxSignInAsync);
        XboxSignOut = ReactiveCommand.CreateFromTask(OnXboxSignOutAsync);
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var ms = await Service.GetMicrosoftAsync(default);
            MicrosoftSignedIn = ms.SignedIn;
            MicrosoftText = ms.SignedIn ? ms.Gamertag ?? ms.Email ?? "Signed in" : "Not signed in";

            var xb = await Service.GetXboxAsync(default);
            XboxSignedIn = xb.SignedIn;
            XboxText = !xb.SignedIn ? "Not signed in" : xb.Gamertag ?? "Signed in";
        }
        catch { /* backend not ready: keep signed-out state */ }
    }

    async Task OnMicrosoftSignInAsync()
    {
        MicrosoftBusy = true;
        try { await Service.SignInMicrosoftAsync(default); } catch { }
        await RefreshAsync(); MicrosoftBusy = false;
    }

    async Task OnMicrosoftSignOutAsync()
    {
        MicrosoftBusy = true;
        try { await Service.SignOutMicrosoftAsync(default); } catch { }
        await RefreshAsync(); MicrosoftBusy = false;
    }

    async Task OnXboxSignOutAsync()
    {
        XboxBusy = true;
        try { await Service.SignOutXboxAsync(default); } catch { }
        await RefreshAsync(); XboxBusy = false;
    }

    async Task OnXboxSignInAsync()
    {
        XboxBusy = true;
        using CancellationTokenSource cts = new();
        try
        {
            var code = await Service.BeginXboxDeviceCodeAsync(cts.Token);
            XboxText = "Waiting for sign-in...";

            var poll = Service.PollXboxDeviceCodeAsync(cts.Token);
            _ = poll.ContinueWith(_ => _main.CurrentDialog?.SelectButtonCommand.Execute("Done"), TaskScheduler.FromCurrentSynchronizationContext());

            // Re-show until the user cancels or the poll finishes; "Open Browser" copies the code and opens the page.
            while (!poll.IsCompleted)
            {
                var message = $"Open {code.VerificationUri} and enter this code:\n\n        {code.UserCode}\n\n• \"Open Browser\" copies the code and opens the page.\n• This dialog closes once you finish signing in.";
                var key = await _main.ShowMessageBoxAsync("🎮 Xbox Live Sign In", message, ["Open Browser", "Cancel"]);
                if (key == "Open Browser")
                {
                    if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } w } && w.Clipboard is { } cb)
                        await cb.SetTextAsync(code.UserCode);
                    NativeMethods.ShellExecute(code.VerificationUri);
                }
                else if (key == "Cancel") { cts.Cancel(); break; }
            }

            try { await poll; } catch (OperationCanceledException) { }
        }
        catch (OperationCanceledException) { }
        catch { }
        await RefreshAsync(); XboxBusy = false;
    }
}
