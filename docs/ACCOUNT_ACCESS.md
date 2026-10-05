# Launcher-owned account tokens

The launcher is the only writer and refresh owner for `Flarial Launcher` /
`Discord`. It publishes the resulting short-lived access token at the separate
`Flarial Launcher` / `Flarial Access v1` entry for the DLL to read:

```json
{"version":1,"access_token":"<OAuth access token>","expires_at":1791228000}
```

Expiry is Unix time in seconds, calculated from request start and the provider's
`expires_in`. The launcher clears old access before replacing login credentials
or logging out, including when the refresh entry is already missing. It saves
successful refresh rotation before publishing the new access record. Invalid
or nearly expired access is not published, but the rotated refresh is preserved.

A 30-second timer renews access within two minutes of expiry while the launcher
is open. It uses the same local gate as login and logout, skips busy operations,
and bounds the token exchange to ten seconds. Transient HTTP/network failures
retain login and retry on the next tick. Invalid refresh responses complete
cleanup before releasing the gate, rather than queuing a later logout.

The DLL must use the matching read-only implementation. It never reads the
refresh entry and never calls OAuth refresh. Old launchers do not publish access;
old DLLs can still rotate the login credential. Closing the launcher stops renewal,
so expired access requires opening it again. No IPC or OAuth provider changes are
introduced. This replaces the shared-writer approach in PR #6.

Run `dotnet run --project tests/AccountAccess/AccountAccess.csproj` for synthetic
handoff, renewal, rotation, exclusion, logout and failure tests. The PR workflow
also builds the full launcher in Debug and Release. Packaged-launcher/injected-game
vault visibility and long-running beta access still need a Windows smoke test.
