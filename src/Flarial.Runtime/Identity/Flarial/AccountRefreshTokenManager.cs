using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity.Flarial;

sealed class AccountRefreshTokenManager : CredentialService<AccountRefreshTokenManager>
{
    private protected override string Username => "Flarial Account";
    private protected override string Resource => "Flarial Launcher";
}
