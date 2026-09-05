using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Flarial.Runtime.Identity;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.Models;

public sealed partial class AccountModel : ReactiveObject
{
    [Reactive] Bitmap _avatar;
    [Reactive] string _username;
    [Reactive] EntitlementModel _entitlement;

    const string DefaultUserName = "Guest";
    const string ProfileImageUri = "avares://Flarial.Launcher/Resources/avatar.webp";

    readonly Bitmap _defaultAvatar;

    internal AccountModel()
    {
        Uri uri = new(ProfileImageUri);

        using var stream = AssetLoader.Open(uri);
        _defaultAvatar = new(stream);

        _entitlement = new();
        Avatar = _defaultAvatar;
        Username = DefaultUserName;
    }

    public void Login(AccountDetails account) => Dispatcher.UIThread.Post(async () =>
    {
        var hasBetaAccess = account.HasBetaAccess;
        var hasFlarialPlus = account.HasFlarialPlus;

        Username = account.Username;

        if (hasBetaAccess && !hasFlarialPlus)
        {
            Entitlement.Name = "Tester";
            Entitlement.Border = Brushes.DarkGray;
            Entitlement.Background = Brushes.DimGray;
        }
        else if (hasBetaAccess)
        {
            Entitlement.Name = "Flarial+";
            Entitlement.Border = Brushes.IndianRed;
            Entitlement.Background = Brushes.DarkRed;
        }

        if (await account.GetAvatarAsync() is { } avatar)
        {
            using MemoryStream stream = new(avatar, false);
            Avatar = new(stream);
        }
        else Avatar = _defaultAvatar;
    }, DispatcherPriority.Background);

    public void Logout() => Dispatcher.UIThread.Post(() =>
    {
        Entitlement.Logout();
        Avatar = _defaultAvatar;
        Username = DefaultUserName;
    }, DispatcherPriority.Background);
}