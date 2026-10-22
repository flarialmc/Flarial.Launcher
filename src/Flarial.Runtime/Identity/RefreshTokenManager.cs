using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

sealed class RefreshTokenManager : PasswordVaultService<RefreshTokenManager>
{
    private protected override string UserName => "Discord";
    private protected override string Resource => "Flarial Launcher";
}