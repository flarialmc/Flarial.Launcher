using System;
using Windows.Security.Credentials;

namespace Flarial.Runtime.Services;

abstract class PasswordVaultService<T> : PasswordVaultService where T : PasswordVaultService<T>, new()
{
    private protected PasswordVaultService()
    {
        if (_ is null) return;
        throw new InvalidOperationException();
    }
    internal static readonly T _ = new();
}

abstract class PasswordVaultService
{
    private protected PasswordVaultService() { }
    private protected abstract string Resource { get; }
    private protected abstract string UserName { get; }

    static readonly PasswordVault s_vault = new();

    PasswordCredential? Retrieve()
    {
        try { return s_vault.Retrieve(Resource, UserName); }
        catch { return null; }
    }

    internal void Remove()
    {
        if (Retrieve() is { } credential)
            s_vault.Remove(credential);
    }

    [Obsolete("Use 'CredentialService' instead.", true)]
    internal string? Get()
    {
        if (Retrieve() is { } credential)
        {
            credential.RetrievePassword();
            return credential.Password;
        }
        return null;
    }

    [Obsolete("Use 'CredentialService' instead.", true)]
    internal void Set(string value)
    {
        s_vault.Add(new()
        {
            Password = value,
            Resource = Resource,
            UserName = UserName
        });
    }
}