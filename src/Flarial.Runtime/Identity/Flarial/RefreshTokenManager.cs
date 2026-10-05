using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity.Flarial;

sealed class RefreshTokenManager : CredentialService<RefreshTokenManager>
{
    private protected override string Username => "Flarial";
    private protected override string Resource => "Flarial Launcher";
}