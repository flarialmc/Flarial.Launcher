using Flarial.Runtime.Services;

namespace Flarial.Runtime.Identity;

sealed class CredentialManager : CredentialService<CredentialManager>
{
    private protected override string UserName => "Flarial";
    private protected override string TargetName => "Flarial Launcher";
}