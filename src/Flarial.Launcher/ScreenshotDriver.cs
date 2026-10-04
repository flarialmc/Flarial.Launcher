using ReactiveUI.Primitives;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Flarial.Launcher.Types;
using Flarial.Launcher.ViewModels;
using Flarial.Launcher.Views;
using ReactiveUI;

namespace Flarial.Launcher;

// Dev tool: set FLARIAL_SHOT=<dir> to walk every screen and save PNGs, then exit.
static class ScreenshotDriver
{
    public static async void Start(MainWindow w)
    {
        if (Environment.GetEnvironmentVariable("FLARIAL_SHOT") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "state"), "boot");
        var vm = (MainWindowViewModel)w.DataContext!;

        async Task Shot(string name, int wait = 1500)
        {
            await Task.Delay(wait);
            File.WriteAllText(Path.Combine(dir, "state"), name);
            await Task.Delay(700);
        }
        void Go(PageTransitions p) => MessageBus.Current.SendMessage(p);

        await Task.Delay(12000);
        await Shot("home");

        var acc = vm._account;
        acc.Username = "Player";
        acc.Entitlement.Name = "Flarial+";
        acc.Entitlement.Border = Brushes.IndianRed;
        acc.Entitlement.Background = Brushes.DarkRed;
        await Shot("home-logged-in");

        File.WriteAllText(Path.Combine(dir, "state"), "t-to-settings"); Go(PageTransitions.SettingsPage); await Task.Delay(1500);
        await Shot("settings-general", 2500);

        var g = vm.SettingsViewModel.SettingsGeneralViewModel;
        g.LoginAvailable = false; g.AccountAvailable = true;
        await Shot("settings-general-logged-in");
        g.LoginAvailable = true; g.AccountAvailable = false;

        File.WriteAllText(Path.Combine(dir, "state"), "t-to-versions"); Go(PageTransitions.SettingsVersionsPage); await Task.Delay(1500);
        await Shot("settings-versions", 2500);
        Go(PageTransitions.SettingsConfigsPage);
        await Shot("settings-configs", 2500);
        var a = vm.SettingsViewModel.SettingsAccountsViewModel;
        Go(PageTransitions.SettingsAccountsPage);
        await Shot("accounts-signed-out", 2500);
        g.LoginAvailable = false; g.AccountAvailable = true;
        a.MicrosoftSignedIn = true; a.MicrosoftText = "FlarialPlayer";
        a.XboxSignedIn = true; a.XboxText = "FlarialPlayer";
        await Shot("accounts-signed-in");
        a.XboxSignedIn = false; a.XboxText = "Not signed in";
        a.XboxSignIn.Execute().Subscribe(_ => { });
        await Shot("accounts-device-code", 2000);
        vm.CurrentDialog?.SelectButtonCommand.Execute("Cancel"); await Task.Delay(1000);
        g.LoginAvailable = true; g.AccountAvailable = false;
        Go(PageTransitions.SettingsGeneralPage);
        await Task.Delay(1500);
        File.WriteAllText(Path.Combine(dir, "state"), "t-to-home"); Go(PageTransitions.HomePage); await Task.Delay(1500);
        await Task.Delay(2000);

        vm.NotificationArea.Add("Flarial Client has been launched successfully.");
        await Shot("notification", 1200);

        _ = vm.ShowMessageBoxAsync("💡 Install Version", "Minecraft 1.21.100 will be now installed.\n\n• Once the installation starts, you won't able to cancel it.\n• Free up disk space before proceeding with the installation.\n• A high speed internet connection is recommended for this.\n\nIf you need help, join our Discord.", ["Install", "Cancel"]);
        await Shot("dialog", 2000);
        File.WriteAllText(Path.Combine(dir, "state"), "done");
        await Task.Delay(500);
        Environment.Exit(0);
    }
}
