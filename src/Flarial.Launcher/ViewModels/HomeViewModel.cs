using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Flarial.Launcher.Dialogs.Metadata;
using Flarial.Launcher.Management;
using Flarial.Launcher.Models;
using Flarial.Launcher.Types;
using Flarial.Runtime.Core;
using Flarial.Runtime.Game;
using Flarial.Runtime.Versions;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase, IProgress<int>
{
    [Reactive] bool _isLaunching = true;
    [Reactive] string _launcherStatus = "Preparing...";
    [Reactive] string _launcherVersion = FlarialLauncher.Version;

    [Reactive] string _gameVersion = "0.0.0";
    [Reactive] IImmutableSolidColorBrush _gameVersionColor = Brushes.Gray;

    UnsupportedVersionDialog UnsupportedVersionDialog => field ??= new(_model.VersionRegistry);

    public DiscordAccountModel DiscordAccount => _model._discordAccount;

    readonly MainWindowViewModel _model;
    readonly AppSettings _settings = ((App)Application.Current!).Settings;

    public ReactiveCommand<RxVoid, RxVoid> Launch { get; }
    public ReactiveCommand<RxVoid, RxVoid> CloseWindow { get; }
    public ReactiveCommand<RxVoid, RxVoid> MinimizeWindow { get; }

    public HomeViewModel(MainWindowViewModel model)
    {
        _model = model;

        Launch = ReactiveCommand.CreateFromTask(OnLaunchAsync);
        CloseWindow = ReactiveCommand.Create(static () => MessageBus.Current.SendMessage(WindowStateArgs.Close));
        MinimizeWindow = ReactiveCommand.Create(static () => MessageBus.Current.SendMessage(WindowStateArgs.Minimize));
    }

    async Task OnLaunchAsync()
    {
        IsLaunching = true;
        try
        {
            var path = _settings.CustomDllPath;
            var beta = _settings.BuildType is BuildType.Beta;
            var release = _settings.BuildType is BuildType.Release;

            FlarialClient? client = null;
            if (beta) client = FlarialClientBeta._;
            if (release) client = FlarialClientRelease._;

            if (!GamingServices.IsInstalled)
            {
                await GamingServicesMissingDialog._.ShowAsync();
                return;
            }

            var isRunning = Minecraft.IsRunning;
            var isInstalled = Minecraft.IsInstalled;

            if (isInstalled && !isRunning)
            {
                if (Minecraft.IsSideloaded)
                {
                    if (!await SideloadedBootstrapDialog._.ShowAsync())
                        return;
                }

                if (release && !_model.VersionRegistry.IsSupported)
                {
                    await UnsupportedVersionDialog.ShowAsync();
                    return;
                }
            }
            else if (!isRunning)
            {
                await GameNotFoundDialog._.ShowAsync();
                return;
            }

            if (client is null)
            {
                Library library = new(path);

                if (!library.IsLoadable)
                {
                    await InvalidCustomDllDialog._.ShowAsync();
                    return;
                }

                LauncherStatus = "Launching...";
                if (!await Task.Run(() => Injector.Launch(library)))
                {
                    await LaunchFailureDialog._.ShowAsync();
                    return;
                }

                return;
            }

            if (beta && !await ClientBetaActiveDialog._.ShowAsync())
                return;

            LauncherStatus = "Verifying...";
            if (!await client.DownloadAsync(this))
            {
                await ClientUpdateFailureDialog._.ShowAsync();
                return;
            }

            if (FlarialClient.IsRunning)
            {
                await ClientAlreadyInjectedDialog._.ShowAsync();
                return;
            }

            LauncherStatus = "Launching...";
            if (!await Task.Run(client.Launch))
            {
                await LaunchFailureDialog._.ShowAsync();
                return;
            }
        }
        finally
        {
            IsLaunching = false;
            LauncherStatus = "Ready!";
        }
    }

    public void Report(int value) => LauncherStatus = $"Downloading... {value}%";

    public void OnPackageStatusChanged()
    {
        if (!Minecraft.IsInstalled)
        {
            GameVersion = "0.0.0";
            GameVersionColor = Brushes.Gray;
            return;
        }

        GameVersion = VersionRegistry.InstalledVersion;
        GameVersionColor = _model.VersionRegistry.IsSupported ? Brushes.DarkGreen : Brushes.DarkRed;
    }
}