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

Production account lookup retains `https://api.flarial.xyz/api/v2/account`
and its Boolean `entitlements.beta.active` contract. The preproduction website
exposes `/api/v1/launcher/account` instead; that response uses
`entitlements.tester.active`, with Boolean or null values. Null means the tester
decision is unavailable: retain the account/name/Flarial+ result but deny beta
access. Missing or malformed selected fields are rejected, and neither profile
falls back to the other profile's entitlement key. This mapping does not enable
release/beta/custom builds in the private preproduction launcher.

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

The same check exercises the account owner's actual bearer GET request and
response decoder. Independent fixtures cover both selected route contracts,
true/false entitlements, unavailable preproduction tester decisions, malformed
values and the wrong profile's key. These checks use no vault, token exchange,
browser, account endpoint or avatar download; they do not prove live login.

Preproduction OAuth client registration, account linking, and issuer assets
remain separate deployment requirements. Production refresh tokens and account
requests are never reused for preproduction. Source review and a local build
do not establish live login, issuer readiness, DLL loading, or game acceptance.

## Account-resource repair verification (2026-10-11)

Owned Windows builds passed for actual Runtime/Launcher and its headless owner
in Production/Preproduction Debug/Release. Consumer fixtures checked the bearer
GET, selected route, asymmetric entitlement decisions, unavailable/null tester,
malformed values and wrong-profile keys. Restoring the old preprod v2 route,
beta-only decoder and non-nullable tester behavior each failed for its intended
reason. Exact source restoration, rebuild and fixed owner passed after each.

The companion DLL built in Production Debug/Release and Preproduction Release;
its actual account owner passed in each phase and after restoring Production.
Its old-route negative failed at the real request assertion; fixed owner passed.
The retained fixed preprod DLL is 24,071,168 bytes, SHA-256
`741840d6764587d04c1b172a85d7522c0357be85e4cc93b7a5cf439577f1a702`.
A final private Preproduction Release build embedded this artifact's actual
path/hash. The owner verified metadata and identical launcher/owner Runtime
copies, SHA-256
`c4cbb74ecb6c585c2b9971c2d44a455ef02b49715a155fbba12f1a428c2d6d9a`.

The bounded continuation completed all inner checks with exit 0 in 492.121 s,
peak job commit 2,012,504,064 bytes, no timeout or disk/log-bound violation.
Limits were 8 GiB job commit, 512 active processes, 1,800 s plus 5 s cleanup,
4 GiB initial/1 GiB runtime disk headroom and 128 MiB aggregate logs. The strict
outer wrapper returned failure because it observed one child before closing
its kill-on-close Job. The child identity and natural exit were not captured;
do not describe this as a clean zero-child receipt. Independent post-close
inspection found no remaining build/owner processes, and source/SDK/protected
artifact postflight passed. Original admission and assertion-line observer
failures are preserved. Completed DLL builds were not repeated.

All 2,707 pinned source/configuration/document files and the three protected
staged/backup DLL identities matched before this documentation update. No live
login, vault mutation, game launch or deployment occurred. The new retained DLL
and private launcher have not replaced the game mod. Live preprod readiness,
account login, canary loading and two-client audio remain pending.
