using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Discord;

public sealed class AccountDetails
{
    public string Username { get; }
    readonly Task<byte[]?> _avatarTask;

    public bool HasBetaAccess { get; }
    public bool HasFlarialPlus { get; }

    internal AccountDetails(string username, string avatarUri, bool hasFlarialPlus, bool hasTesterRole)
    {
        Username = username;
        _avatarTask = HttpService.TryGetBytesAsync(avatarUri);

        HasFlarialPlus = hasFlarialPlus;
        HasBetaAccess = hasTesterRole || hasFlarialPlus;
    }

    public Task<byte[]?> GetAvatarAsync() => _avatarTask;
}