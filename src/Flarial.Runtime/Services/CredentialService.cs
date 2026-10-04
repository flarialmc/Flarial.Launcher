using System;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Services;

abstract class CredentialService<T> : CredentialService where T : CredentialService<T>, new()
{
    private protected CredentialService()
    {
        if (_ is null) return;
        throw new InvalidOperationException();
    }

    internal static readonly T _  = new();
}

abstract class CredentialService
{
    private protected CredentialService() { }

    private protected abstract string Resource { get; }
    private protected abstract string Username { get; }

    internal void Remove() => Platform.Platform.Credentials.Remove(Resource, Username);
    internal string? Get() => Platform.Platform.Credentials.Get(Resource, Username);
    internal void Set(string value) => Platform.Platform.Credentials.Set(Resource, Username, value);
}
