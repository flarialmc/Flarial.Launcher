using System;
using Windows.Win32.Security.Credentials;
using static Windows.Win32.PInvoke;
using static Windows.Win32.Security.Credentials.CRED_FLAGS;
using static Windows.Win32.Security.Credentials.CRED_PERSIST;
using static Windows.Win32.Security.Credentials.CRED_TYPE;

namespace Flarial.Runtime.Services;

abstract class CredentialService<T> : CredentialService where T : CredentialService<T>, new()
{
    private protected CredentialService()
    {
        if (_ is null) return;
        throw new InvalidOperationException();
    }
    internal static readonly T _ = new();
}

unsafe abstract class CredentialService
{
    private protected CredentialService() { }
    private protected abstract string UserName { get; }
    private protected abstract string TargetName { get; }

    private protected void Write(string value)
    {
        fixed (char* blob = value)
        fixed (char* user = UserName)
        fixed (char* target = TargetName)
        {
            var size = sizeof(char) * value.Length;

            CREDENTIALW credential = new()
            {
                UserName = user,
                TargetName = target,
                Type = CRED_TYPE_GENERIC,
                CredentialBlob = (byte*)blob,
                CredentialBlobSize = (uint)size,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
            };

            CredWrite(&credential, 0);
        }
    }

    private protected string? Read()
    {
        fixed (char* target = TargetName)
        {
            CREDENTIALW* credential = null;
            try
            {
                if (!CredRead(target, CRED_TYPE_GENERIC, 0, &credential))
                    return null;

                var ptr = (char*)credential->CredentialBlob;
                var length = (int)credential->CredentialBlobSize / sizeof(char);

                string value = new(ptr, 0, length);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            finally { CredFree(credential); }
        }
    }

    private protected void Delete()
    {
        fixed (char* target = TargetName)
            CredDelete(target, CRED_TYPE_GENERIC, 0);
    }
}