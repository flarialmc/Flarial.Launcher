using System.Linq;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Flarial.Launcher.Dialogs.Metadata;
using Flarial.Launcher.Views;
using Flarial.Runtime.Game;
using Flarial.Runtime.Versions;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.ViewModels;

public enum VersionItemState { Downloading, Installing, Installed, NotInstalled, Selected }

public sealed partial class VersionItemViewModel : ViewModelBase, IProgress<(int Percentage, bool Installing)>
{
    public string Version { get; }

    [Reactive] VersionItemState _state = VersionItemState.NotInstalled;
    [Reactive] double _installPercentage;
    [Reactive] bool _isProgressing;

    public ReactiveCommand<RxVoid, RxVoid> DeleteCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> InstallCommand { get; }

    static Flarial.Runtime.Platform.IGameService Game => Flarial.Runtime.Platform.Platform.Game;

    /// <summary>Installed (downloaded) = "Select"; the one the launcher starts = "Selected".</summary>
    VersionItemState DiskState => !Game.InstalledVersions.Any(v => v == _versionItem.Version) ? VersionItemState.NotInstalled
        : Game.InstalledVersion == _versionItem.Version ? VersionItemState.Selected : VersionItemState.Installed;

    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> SelectCommand { get; }

    [Reactive]
    bool _gameRunning;

    public bool IsDeletable => State is VersionItemState.Installed or VersionItemState.Selected;
    public bool IsSelected => State is VersionItemState.Selected;

    void OnPackageStatusChanged()
    {
        GameRunning = Game.IsRunning;
        if (State is VersionItemState.Downloading or VersionItemState.Installing) return;
        State = DiskState;
    }

    public bool IsInstalling => State is VersionItemState.Installing;
    public bool IsDownloading => State is VersionItemState.Downloading;

    public bool IsInstalled => State is VersionItemState.Installed;
    public bool IsNotSelected => !IsSelected;
    public bool IsNotInstalled => State is VersionItemState.NotInstalled;

    readonly MainWindow _mainWindow;
    readonly VersionItem _versionItem;
    readonly SettingsVersionsViewModel _settingsVersionsViewModel;

    readonly DeleteVersionDialog _deleteVersionDialog;
    readonly InstallVersionDialog _installVersionDialog;
    readonly InstalledVersionDialog _installedVersionDialog;
    readonly InstallingVersionDialog _installingVersionDialog;
    readonly DownloadLinksMissingDialog _downloadLinksMissingDialog;

    Task? InstallingVersionDialogTask
    {
        get
        {
            if (field is null) return null;
            if (field.IsCompleted) return null;
            return field;
        }
        set;
    }

    public VersionItemViewModel(MainWindowViewModel mainWindowViewModel, VersionItem versionItem)
    {
        var application = Application.Current!;
        var applicationLifetime = (IClassicDesktopStyleApplicationLifetime)application.ApplicationLifetime!;

        _versionItem = versionItem;
        _mainWindow = (MainWindow)applicationLifetime.MainWindow!;
        _settingsVersionsViewModel = mainWindowViewModel.SettingsViewModel.SettingsVersionsViewModel;

        _deleteVersionDialog = new(versionItem);
        _installVersionDialog = new(versionItem);
        _installedVersionDialog = new(versionItem);
        _installingVersionDialog = new(versionItem);
        _downloadLinksMissingDialog = new(versionItem);

        this.WhenAnyValue(static _ => _.State).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsNotInstalled));
            this.RaisePropertyChanged(nameof(IsDownloading));
            this.RaisePropertyChanged(nameof(IsInstalled));
            this.RaisePropertyChanged(nameof(IsInstalling));
            this.RaisePropertyChanged(nameof(IsDeletable));
            this.RaisePropertyChanged(nameof(IsSelected));
        });

        Version = $"{versionItem}";
        State = DiskState;
        GameRunning = Game.IsRunning;
        Minecraft.PackageStatusChanged += OnPackageStatusChanged;
        // changing builds is blocked while the game runs or any install is in progress
        var idle = this.WhenAnyValue(static _ => _.GameRunning).CombineLatest(_settingsVersionsViewModel.WhenAnyValue(static _ => _.IsInstalling), static (running, installing) => !running && !installing);
        var canDelete = this.WhenAnyValue(static _ => _.State).Select(static _ => _ is VersionItemState.Installed or VersionItemState.Selected).CombineLatest(idle, static (a, b) => a && b);
        var canSelect = this.WhenAnyValue(static _ => _.State).Select(static _ => _ == VersionItemState.Installed).CombineLatest(idle, static (a, b) => a && b);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync, canDelete);
        SelectCommand = ReactiveCommand.CreateFromTask(SelectAsync, canSelect);
        InstallCommand = ReactiveCommand.CreateFromTask(InstallAsync, this.WhenAnyValue(static _ => _.State).Select(static _ => _ == VersionItemState.NotInstalled));
    }

    public void Report((int Percentage, bool Installing) value)
    {
        InstallPercentage = value.Percentage;
        if (value.Installing) State = VersionItemState.Installing;
    }

    async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (!(args.Cancel = IsProgressing)) return;
        if (InstallingVersionDialogTask is { }) return;

        try { await (InstallingVersionDialogTask = _installingVersionDialog.ShowAsync()); }
        finally { InstallingVersionDialogTask = null; }
    }

    private async Task InstallAsync()
    {
        if (!GamingServices.IsInstalled)
        {
            await GamingServicesMissingDialog._.ShowAsync();
            return;
        }

        if (!Minecraft.IsInstalled && Flarial.Runtime.Platform.Platform.Game.RequiresInstalledGame)
        {
            await NotInstalledDialog._.ShowAsync();
            return;
        }

        if (Minecraft.IsSideloaded)
        {
            await SideloadedInstallDialog._.ShowAsync();
            return;
        }

        if (!Flarial.Runtime.Platform.Platform.Game.RequiresInstalledGame && !Flarial.Runtime.Platform.Platform.MicrosoftAccount.IsSignedIn)
        {
            await MicrosoftSignInRequiredDialog._.ShowAsync();
            return;
        }

        if (!await _installVersionDialog.ShowAsync())
            return;

        try
        {
            IsProgressing = true;
            _settingsVersionsViewModel.IsInstalling = true;

            InstallPercentage = 0;
            State = VersionItemState.Downloading;

            var task = await _versionItem.InstallAsync(this);

            if (task is null)
            {
                await _downloadLinksMissingDialog.ShowAsync();
                return;
            }

            _mainWindow.Closing += OnClosing;
            try { await task; }
            catch (Exception e)
            {
                Flarial.Runtime.Linux.LinuxPlatform.Notify?.Invoke($"Installing Minecraft {_versionItem} failed: {e.Message}");
                return;
            }
        }
        finally
        {
            InstallPercentage = 0;
            State = DiskState;

            IsProgressing = false;
            _settingsVersionsViewModel.IsInstalling = false;

            _mainWindow.Closing -= OnClosing;
        }

        await _installedVersionDialog.ShowAsync();
    }

    async Task SelectAsync()
    {
        if (!await Task.Run(() => Game.SelectVersion(_versionItem.Version)))
            Flarial.Runtime.Linux.LinuxPlatform.Notify?.Invoke($"Could not select Minecraft {_versionItem}.");
    }

    async Task DeleteAsync()
    {
        if (!await _deleteVersionDialog.ShowAsync()) return;
        if (!await Task.Run(() => Game.DeleteVersion(_versionItem.Version)))
            Flarial.Runtime.Linux.LinuxPlatform.Notify?.Invoke($"Could not delete Minecraft {_versionItem}.");
    }
}