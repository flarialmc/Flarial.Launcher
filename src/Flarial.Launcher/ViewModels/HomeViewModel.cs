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
    [Reactive] string _launcherVersion = FlarialLauncher.Version;
    [Reactive] string _launcherStatus = "Connecting to services...";

    [Reactive] string _gameVersion = "0.0.0";
    [Reactive] IImmutableSolidColorBrush _gameVersionColor = Brushes.Gray;

    UnsupportedVersionDialog UnsupportedVersionDialog => field ??= new(_mainWindowViewModel.VersionRegistry);

    public AccountModel Account => _mainWindowViewModel._account;

    readonly MainWindowViewModel _mainWindowViewModel;
    readonly AppSettings _settings = ((App)Application.Current!).Settings;

    public ReactiveCommand<RxVoid, RxVoid> Launch { get; }
    public ReactiveCommand<RxVoid, RxVoid> CloseWindow { get; }
    public ReactiveCommand<RxVoid, RxVoid> MinimizeWindow { get; }

    public HomeViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;

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
            var betaV2 = _settings.BuildType is BuildType.BetaV2;
            var alphaV3 = _settings.BuildType is BuildType.AlphaV3;
            var release = _settings.BuildType is BuildType.Release;

            FlarialClient? client = null;
            if (betaV2) client = FlarialClientBetaV2._;
            if (alphaV3) client = FlarialClientAlphaV3._;
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

                if (release && !_mainWindowViewModel.VersionRegistry.IsSupported)
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
                ModificationLibrary library = new(path);

                if (!library.IsLoadable)
                {
                    await InvalidCustomDllDialog._.ShowAsync();
                    return;
                }

                LauncherStatus = "Launching the game...";
                if (!await Task.Run(() => Injector.Launch(library)))
                {
                    await LaunchFailureDialog._.ShowAsync();
                    return;
                }

                return;
            }

            if (alphaV3 && !await ClientAlphaV3ActiveDialog._.ShowAsync())
                return;

            if (betaV2 && !await ClientBetaV2ActiveDialog._.ShowAsync())
                return;

            LauncherStatus = "Checking client update...";
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

            LauncherStatus = "Launching the client...";
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

    public void Report(int value) => LauncherStatus = $"Downloading client update... {value}%";

    public void OnPackageStatusChanged()
    {
        if (!Minecraft.IsInstalled)
        {
            GameVersion = "0.0.0";
            GameVersionColor = Brushes.Gray;
            return;
        }

        GameVersion = VersionRegistry.InstalledVersion;
        GameVersionColor = _mainWindowViewModel.VersionRegistry.IsSupported ? Brushes.DarkGreen : Brushes.DarkRed;
    }
}