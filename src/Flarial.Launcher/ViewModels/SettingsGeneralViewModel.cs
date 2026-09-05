using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Flarial.Launcher.Controls.SegmentedBar;
using Flarial.Launcher.Management;
using Flarial.Launcher.Models;
using Flarial.Runtime.Identity;
using Flarial.Runtime.Unmanaged;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.ViewModels;

public sealed partial class SettingsGeneralViewModel : ViewModelBase
{
    [Reactive] string? _customDllPath = null;
    [Reactive] bool _customDllSelected = false;
    [Reactive] bool _loginActive = true;
    [Reactive] bool _loginAvailable = false;
    [Reactive] bool _accountAvailable = false;

    public AvaloniaList<SegmentItem> BuildTypes { get; }
    public AccountModel Account => _mainWindowViewModel._account;

    readonly SegmentItem _customItem = new() { Title = "Custom", Tag = BuildType.Custom };
    readonly SegmentItem _releaseItem = new() { Title = "Release", Tag = BuildType.Release };
    readonly SegmentItem _betaItem = new() { Title = "Beta", Tag = BuildType.Beta, IsEnabled = false };

    internal bool HasBetaAccess
    {
        get;
        set
        {
            if (SelectedBuild == _betaItem && !value)
                SelectedBuild = _releaseItem;
            _betaItem.IsEnabled = field = value;
        }
    }

    public SegmentItem? SelectedBuild
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            OnBuildChanged(field);
        }
    }

    public bool PerformanceMode
    {
        get;
        set
        {
            _settings.PerformanceMode = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    public bool AutomaticUpdates
    {
        get;
        set
        {
            _settings.AutomaticUpdates = value;
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    static readonly FilePickerOpenOptions s_options = new()
    {
        FileTypeFilter = [new("Dynamic Link Libraries") { Patterns = ["*.dll"] }]
    };

    public ReactiveCommand<RxVoid, RxVoid> Open { get; }
    public ReactiveCommand<RxVoid, RxVoid> Login { get; }
    public ReactiveCommand<RxVoid, RxVoid> Logout { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenClientFolder { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenLauncherFolder { get; }

    async Task OnOpenAsync()
    {
        var application = Application.Current!;
        var lifetime = (IClassicDesktopStyleApplicationLifetime)application.ApplicationLifetime!;
        var files = await lifetime.MainWindow!.StorageProvider.OpenFilePickerAsync(s_options);

        if (files.Any())
        {
            var path = files[0].TryGetLocalPath()!;
            CustomDllPath = _settings.CustomDllPath = path;
        }
    }

    void OnOpenLauncherFolder() => NativeMethods.ShellExecute(".");

    void OnOpenClientFolder() => NativeMethods.ShellExecute(Directory.CreateDirectory(@"..\Client").FullName);

    readonly AppSettings _settings;
    readonly MainWindowViewModel _mainWindowViewModel;

    public SettingsGeneralViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _settings = ((App)Application.Current!).Settings;

        BuildTypes = [_releaseItem, _betaItem, _customItem];

        switch (_settings.BuildType)
        {
            case BuildType.Beta:
                SelectedBuild = _betaItem;
                break;

            case BuildType.Release:
                SelectedBuild = _releaseItem;
                break;

            case BuildType.Custom:
                SelectedBuild = _customItem;
                CustomDllSelected = true;
                break;
        }

        CustomDllPath = _settings.CustomDllPath;
        PerformanceMode = _settings.PerformanceMode;
        AutomaticUpdates = _settings.AutomaticUpdates;

        Open = ReactiveCommand.CreateFromTask(OnOpenAsync);
        Login = ReactiveCommand.CreateFromTask(OnLoginAsync);
        Logout = ReactiveCommand.CreateFromTask(OnLogoutAsync);
        OpenClientFolder = ReactiveCommand.Create(OnOpenClientFolder);
        OpenLauncherFolder = ReactiveCommand.Create(OnOpenLauncherFolder);
    }

    async Task OnLoginAsync()
    {
        LoginAvailable = false;

        if (!await AuthenticationManager.AuthenticateAsync())
        {
            await OnLogoutAsync();
            return;
        }

        await LoginAsync();
    }

    internal async Task LoginAsync()
    {
        LoginAvailable = false;

        if (await AccountManager.LoginAsync() is not { } account)
        {
            await OnLogoutAsync();
            return;
        }

        HasBetaAccess = account.HasBetaAccess;
        AccountAvailable = true;

        Account.Login(account);
    }

    async Task OnLogoutAsync()
    {
        HasBetaAccess = false;
        await AccountManager.LogoutAsync();

        Account.Logout();
        LoginAvailable = true;
        AccountAvailable = false;
    }

    private void OnBuildChanged(SegmentItem? item)
    {
        if (item == null) return;
        var buildType = (BuildType)item.Tag!;

        _settings.BuildType = buildType;
        CustomDllSelected = buildType is BuildType.Custom;
    }
}