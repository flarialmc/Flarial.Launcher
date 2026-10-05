using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity.Discord;

sealed class RefreshTokenManager : CredentialService<RefreshTokenManager>
{
    private protected override string Username => "Flarial";
    private protected override string Resource => "Flarial Launcher";
}