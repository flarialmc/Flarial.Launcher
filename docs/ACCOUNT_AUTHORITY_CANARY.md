# Trusted account authority canary

The default launcher build remains Production. `FlarialAccountAuthority=Preproduction`
selects the fixed preproduction OAuth, token, revoke, resource, and account
endpoints in `AccountAuthorityProfile`. The choice is compiled into the runtime;
`BuildType`, a custom DLL path, and player voice-server preferences cannot change
it. The preproduction build has a separate Windows credential-vault resource,
settings directory, process mutex, and build output directory. It is an
unpackaged private build, not a replacement for the production MSIX.
The CLR assembly name remains `Flarial.Launcher` so existing Avalonia resource
URIs resolve in both builds.

The trusted client handoff now includes `account_authority` next to the access
token. Production sends `"production"`; preproduction sends `"preproduction"`.
The receiving DLL must enforce the matching profile. Older DLLs ignore this
marker, so a preproduction launcher never downloads or launches the release,
beta, or user-selected custom DLL. The legacy command-line injector is also
disabled in that build.

A trusted preproduction build may set `FlarialPreproductionDllPath` to an
absolute local path and `FlarialPreproductionDllSha256` to the complete SHA-256
of a separately reviewed preproduction canary DLL. Both values are embedded in
the runtime assembly at build time. The launch owner opens the local file with
writers denied, verifies its hash and DLL shape, and keeps that handle open
through injection. Missing or mismatched values fail closed. No accepted path
or hash is committed here; review and pin an actual artifact before attempting
a canary launch.

`tests/Flarial.Runtime.AuthorityChecks` is the headless owner check. Given a
reviewed local PE DLL fixture, it exercises the loader's actual handoff JSON,
correct and incorrect artifact hashes, a modified DLL, and the read handle that
denies writers until launch completion. It also checks the compiled assembly
metadata for an unset or trusted build pin. It does not start Minecraft or
authenticate a player.

Preproduction OAuth client registration, account linking, and issuer assets
remain separate deployment requirements. Production refresh tokens and account
requests are never reused for preproduction. Source review and a local build
do not establish live login, issuer readiness, DLL loading, or game acceptance.
