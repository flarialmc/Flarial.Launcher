using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Flarial.Launcher.Dialogs.Metadata;
using Flarial.Launcher.Management;
using Flarial.Launcher.Models;
using Flarial.Runtime.Core;
using Flarial.Runtime.Game;
using Flarial.Runtime.Versions;
using ReactiveUI;
using Windows.Management.Deployment;

namespace Flarial.Launcher.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IProgress<int>, IProgress<DeploymentProgress>
{
    readonly SemaphoreSlim _semaphore = new(1, 1);

    public MessageBoxViewModel? CurrentDialog
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public HomeViewModel HomeViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public NotificationAreaViewModel NotificationArea { get; }
    public VersionRegistry VersionRegistry { get; private set; }

    internal readonly AccountModel _account;
    readonly AppSettings _settings = ((App)Application.Current!).Settings;

    public MainWindowViewModel()
    {
        HomeViewModel = new HomeViewModel(this);
        SettingsViewModel = new SettingsViewModel(this);
        NotificationArea = new NotificationAreaViewModel();

        _account = new();
        VersionRegistry = null!;
    }

    public async Task<string> ShowMessageBoxAsync(string title, string message, IEnumerable<string> buttons)
    {
        await _semaphore.WaitAsync(); try
        {
            try
            {
                CurrentDialog = new(title, message, buttons);
                return await CurrentDialog.Result;
            }
            finally { CurrentDialog = null; }
        }
        finally { _semaphore.Release(); }
    }

    public void Report(int value)
    {
        HomeViewModel.LauncherStatus = $"Downloading launcher update... {value}%";
    }

    public void Report(DeploymentProgress value)
    {
        HomeViewModel.LauncherStatus = $"Installing launcher update... {value}%";
    }

    async Task LoginWithAccountAsync()
    {
        HomeViewModel.LauncherStatus = "Logging into services...";
        await SettingsViewModel.SettingsGeneralViewModel.LoginAsync();
        SettingsViewModel.SettingsGeneralViewModel.LoginActive = false;
    }

    public async void OnLoaded()
    {
        if (!await FlarialLauncher.CanConnectAsync())
        {
            await ConnectionFailureDialog._.ShowAsync();
            Environment.Exit(1);
            return;
        }

        if (await FlarialLauncher.CheckForUpdatesAsync() && (_settings.AutomaticUpdates || await LauncherUpdateAvailableDialog._.ShowAsync()))
        {
            HomeViewModel.LauncherStatus = "Downloading launcher update...";
            await FlarialLauncher.UpdateAsync(this);
            return;
        }

        var loginWithAccountTask = LoginWithAccountAsync();
        VersionRegistry = await VersionRegistry.GetAsync();

        _ = Task.Run(() =>
        {
            foreach (var version in VersionRegistry) Dispatcher.UIThread.Post(() =>
            {
                SettingsViewModel.SettingsVersionsViewModel.Versions.Add(new(this, version));
            }, DispatcherPriority.Background);
        });

        HomeViewModel.OnPackageStatusChanged();
        Minecraft.PackageStatusChanged += HomeViewModel.OnPackageStatusChanged;

        await loginWithAccountTask;

        HomeViewModel.LauncherStatus = "Ready!";
        HomeViewModel.IsLaunching = false;
    }
}