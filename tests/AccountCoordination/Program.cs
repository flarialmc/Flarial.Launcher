using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flarial.Runtime.Core;
using Flarial.Runtime.Identity;
using Flarial.Runtime.Services;
using AuthenticationManager = Flarial.Runtime.Identity.AuthenticationManager;

static class Program
{
    static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    static async Task Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "hold")
        {
            await new CredentialLock(args[1]).RunAsync(async () =>
            {
                await File.WriteAllTextAsync(args[2], "ready");
                while (!File.Exists(args[3])) await Task.Delay(10);
            });
            return;
        }

        // Failed refresh must finish revocation before returning/releasing its
        // credential transaction, so cleanup cannot delete a later login.
        RefreshTokenManager._.Value = "fixture-refresh";
        FlarialClientBeta._.AccessToken = "fixture-access";
        var revoking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRevoke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpService.Post = async (uri, _) =>
        {
            if (uri.EndsWith("/token")) return new(HttpStatusCode.Unauthorized);
            Require(uri.EndsWith("/revoke"), "Unexpected provider request.");
            revoking.SetResult();
            await finishRevoke.Task;
            return new(HttpStatusCode.OK);
        };
        var failed = AuthenticationManager.AuthenticateSilentlyAsync();
        await revoking.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!failed.IsCompleted, "Failed refresh returned before its logout finished.");
        Require(RefreshTokenManager._.Value is null && FlarialClientBeta._.AccessToken is null,
            "Logout did not remove the active local account.");
        finishRevoke.SetResult();
        Require(await failed is null, "Failed refresh supplied an access token.");
        FlarialClientBeta._.AccessToken = "stale-access";
        await AuthenticationManager.RevokeAsync();
        Require(FlarialClientBeta._.AccessToken is null, "Missing vault entry left beta authorization cached.");

        var name = @"Local\Flarial.CredentialFixture." + Guid.NewGuid();
        var credentials = new CredentialLock(name);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0;
        var first = credentials.RunAsync(async () =>
        {
            Interlocked.Increment(ref active);
            entered.SetResult();
            await release.Task;
            Interlocked.Decrement(ref active);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = new CredentialLock(name).RunAsync(async () =>
        {
            Require(Volatile.Read(ref active) == 0, "Async credential transactions overlapped.");
            await Task.Yield();
        });
        await Task.Delay(100);
        Require(!second.IsCompleted, "Second owner bypassed a held lease.");
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        try { await credentials.RunAsync(() => Task.FromException(new IOException("fixture failure"))); }
        catch (IOException) { }
        await credentials.RunAsync(async () => await Task.Yield()).WaitAsync(TimeSpan.FromSeconds(5));

        // The optional native executable comes from dll-css's lease test target.
        // Holding its mutex must also block this .NET owner.
        await CheckProcess(name, null);
        if (args.Length == 1) await CheckProcess(name, args[0]);
        Console.WriteLine("Account cleanup, async lease, process exclusion and abandoned-owner recovery passed.");
    }

    static async Task CheckProcess(string name, string? native)
    {
        var folder = Path.Combine(Path.GetTempPath(), "flarial-credential-fixture-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var ready = Path.Combine(folder, "ready");
        var release = Path.Combine(folder, "release");
        var info = new ProcessStartInfo(native ?? Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "hold", name, ready, release }) info.ArgumentList.Add(arg);
        using var child = Process.Start(info)!;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!File.Exists(ready))
            {
                Require(!child.HasExited, "Fixture owner failed to acquire its lease.");
                await Task.Delay(10, deadline.Token);
            }
            int entered = 0;
            var waiter = new CredentialLock(name).RunAsync(() => { Interlocked.Exchange(ref entered, 1); return Task.CompletedTask; });
            await Task.Delay(100);
            Require(!waiter.IsCompleted && Volatile.Read(ref entered) == 0, "Process boundary did not exclude a second credential owner.");
            // Simulate a process crash. The surviving owner must recover and
            // complete without ReleaseMutex running on the wrong managed thread.
            child.Kill();
            await child.WaitForExitAsync(deadline.Token);
            await waiter.WaitAsync(deadline.Token);
            Require(Volatile.Read(ref entered) == 1, "Abandoned credential lease was not recovered.");
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); }
            Directory.Delete(folder, true);
        }
    }
}
