# Account credential transactions

The launcher and DLL share a Flarial OAuth2 refresh credential in PasswordVault
under `Flarial Launcher` / `Discord`. Refresh, logout/revocation, and credential
replacement acquire `Global\Flarial.AccountCredential.v1.<Windows user SID>`.
Both repositories must use that exact protocol name and transaction boundary.
The global namespace spans sessions of the same Windows user; default Windows
object security applies. No OAuth identities or secrets belong in mutex names
or diagnostics.

AccountManager takes its local async gate before the Windows mutex. CredentialLock
uses a Task.Run worker to acquire, wait for the async transaction, and release
on one thread: Windows mutex ownership cannot move with an await continuation.
Acquisition fails after 20 seconds. Access/creation failure also fails closed.
All exits, including provider exceptions, release the lease. An abandoned lease
is recovered and the credential re-read. A crash between issuer rotation and
vault persistence may still require signing in again.

Refresh reads, redeems, and saves under one lease. Logout clears beta access and
removes the current credential before revocation, while retaining the lease.
Failure cleanup finishes before the transaction returns; it must never queue
a later AccountManager.LogoutAsync that could remove a new login. Even without
a vault entry, logout clears cached beta access. Browser sign-in runs outside
the Windows lease and acquires it only to replace the credential and clear the
previous beta access, so waiting for a person does not block DLL refresh.

The DLL re-reads the vault before reusing access and binds its cache to the saved
refresh credential's fingerprint. This invalidates cached authorization after
logout/switch on the next lookup; already-issued access tokens are not remotely
revoked by a local mutex.

## Tests and release checks

```powershell
dotnet run --project tests/AccountCoordination/AccountCoordination.csproj -c Debug --no-launch-profile
dotnet run --project tests/AccountCoordination/AccountCoordination.csproj -c Release --no-launch-profile
```

These source-link the actual credential lease and refresh/revocation owner, with
synthetic credentials and an in-memory HTTP provider. They never use PasswordVault,
open a browser, or call production endpoints. Tests cover awaited cleanup, beta
cache clearing, async exclusion, exception release, process exclusion, and recovery
after a process crash. Optionally pass the absolute path to dll-css's
`FlarialCredentialLeaseTests.exe` after `--` to exercise native/.NET exclusion
and abandonment. Fixtures use unique `Local\Flarial.CredentialFixture.*` names.

Deploy alongside the DLL coordination change. Older writers ignore this mutex;
mixed versions do not provide full serialization. Packaged-launcher/injected-game
testing of mutex permissions, vault visibility, simultaneous refresh, and
logout/account switching remains required before release. This PR does not
deploy, change OAuth policy, or exercise live accounts.
