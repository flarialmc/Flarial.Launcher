using System;
using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

[Obsolete("Use 'CredentialService' instead.", true)]
sealed class RefreshTokenManager : PasswordVaultService<RefreshTokenManager>
{
    private protected override string UserName => "Discord";
    private protected override string Resource => "Flarial Launcher";
}