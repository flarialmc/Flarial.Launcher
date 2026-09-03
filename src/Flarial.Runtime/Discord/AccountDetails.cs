using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Flarial.Runtime.Services;
using Windows.ApplicationModel.Store.Preview.InstallControl;

namespace Flarial.Runtime.Discord;

public sealed class AccountDetails
{
    const string AvatarUri = "https://cdn.discordapp.com/avatars/{0}/{1}";

    public string Username { get; }
    readonly Task<byte[]?> _avatarTask;

    public bool HasBetaAccess { get; }
    public bool HasFlarialPlus { get; }

    internal AccountDetails(AccountMetadata metadata)
    {
        Username = metadata.Username;
        HasFlarialPlus = metadata.HasFlarialPlus;
        HasBetaAccess = metadata.HasFlarialPlus || metadata.HasTesterRole;

        var avatarUri = string.Format(AvatarUri, metadata.DiscordId, metadata.Avatar);
        _avatarTask = HttpService.TryGetBytesAsync(avatarUri);
    }

    public Task<byte[]?> GetAvatarAsync() => _avatarTask;
}