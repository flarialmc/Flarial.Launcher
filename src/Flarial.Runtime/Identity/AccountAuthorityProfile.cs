namespace Flarial.Runtime.Identity;

// This is a trusted build choice. Player settings and custom voice destinations
// cannot select an OAuth issuer or change where launcher credentials are sent.
public static class AccountAuthorityProfile
{
#if FLARIAL_ACCOUNT_PREPRODUCTION
    public static readonly bool IsPreproduction = true;
    public const string Name = "preproduction";
    public const string OAuthResource = "https://flarial-api-site-preprod.ashanksundaram2.workers.dev/api";
    public const string OAuthBase = "https://flarial-api-site-preprod.ashanksundaram2.workers.dev/api/auth/oauth2";
    public const string AccountUrl = "https://flarial-api-site-preprod.ashanksundaram2.workers.dev/api/v1/launcher/account";
    public const string CredentialResource = "Flarial Launcher Preproduction";
#else
    public static readonly bool IsPreproduction = false;
    public const string Name = "production";
    public const string OAuthResource = "https://flarial.xyz/api";
    public const string OAuthBase = "https://flarial.xyz/api/auth/oauth2";
    public const string AccountUrl = "https://api.flarial.xyz/api/v2/account";
    public const string CredentialResource = "Flarial Launcher";
#endif
}
