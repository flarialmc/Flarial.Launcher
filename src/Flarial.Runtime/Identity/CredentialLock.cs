using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Flarial.Runtime.Identity;

// Shared with the native DLL. A transaction owns the mutex through credential
// read, HTTP exchange, and credential commit (or removal).
sealed class CredentialLock(string name)
{
    internal static string CurrentUserName
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value ?? throw new InvalidOperationException("Account coordination unavailable.");
            return @"Global\Flarial.AccountCredential.v1." + sid;
        }
    }

    internal Task<T> RunAsync<T>(Func<Task<T>> operation) => Task.Run(() =>
    {
        using var mutex = new Mutex(false, name);
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("Account operation already in progress.");
        }
        catch (AbandonedMutexException)
        {
            // PasswordVault entries are atomic. The operation re-reads the
            // credential; no cached refresh token is redeemed after recovery.
        }

        try
        {
            // A Windows mutex is thread-affine. Keep acquisition and release
            // on this worker even if the operation's awaits change threads.
            return operation().GetAwaiter().GetResult();
        }
        finally { mutex.ReleaseMutex(); }
    });

    internal Task RunAsync(Func<Task> operation) => RunAsync(async () =>
    {
        await operation();
        return true;
    });
}
