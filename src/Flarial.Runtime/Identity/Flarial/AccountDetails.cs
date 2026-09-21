using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity.Flarial;

[Experimental("Flarial_Runtime_Identity_Flarial")]
sealed class AccountMetadata
{
    internal required string? AvatarUrl { get; init; }
    internal required string DisplayName { get; init; }
    internal required bool HasBetaAccess { get; init; }
    internal required bool HasFlarialPlus { get; init; }
}

[Experimental("Flarial_Runtime_Identity_Flarial")]
public sealed class AccountDetails
{
    static readonly Task<byte[]?> s_avatarTask = Task.FromResult<byte[]?>(null);

    public string Username { get; }
    readonly Task<byte[]?> _avatarTask = s_avatarTask;

    public bool HasBetaAccess { get; }
    public bool HasFlarialPlus { get; }

    internal AccountDetails(AccountMetadata metadata)
    {
        Username = metadata.DisplayName;
        HasBetaAccess = metadata.HasBetaAccess;
        HasFlarialPlus = metadata.HasFlarialPlus;

        if (metadata.AvatarUrl is { } avatarUrl)
            _avatarTask = HttpService.TryGetBytesAsync(avatarUrl);
    }

    public Task<byte[]?> GetAvatarAsync() => _avatarTask;
}