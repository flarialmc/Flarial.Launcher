# Flarial Launcher for Linux

Avalonia UI of the official Flarial launcher (v3, flarialmc/launcher) running natively on linux-x64. XAML, styles, animations and assets are the upstream files unchanged (`docs/ui-deviations.md` lists every changed line).

## Build / run
```
# .NET 10 SDK (here: /run/media/bariman/url/Other Programs/dotnet)
export DOTNET_ROOT="/run/media/bariman/url/Other Programs/dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project src/Flarial.Launcher -maxcpucount:8
```
Data lives in `~/.local/share/Flarial/Launcher` (override with `XDG_DATA_HOME`). Needs a desktop session (X11/XWayland), `xdg-open`; optional `secret-tool` (libsecret), `zenity`/`kdialog`.

## Architecture
* `src/Flarial.Launcher` - Avalonia UI/ViewModels (upstream).
* `src/Flarial.Runtime` - cross-platform backend: HTTP/JSON, Flarial/Discord OAuth (system browser + localhost redirect), client DLL download (release/beta), VersionRegistry, promotions, PE check (`Library`), settings. Talks to the OS only through `Platform/Platform.cs`:
  * `IGameService` - `IsInstalled`, `InstalledVersion`, `InstalledVersions`, `IsRunning`, `IsSideloaded`, `IsGamingServicesInstalled`, `RequiresInstalledGame`, `StatusChanged`, `InstallAsync(VersionItem, uri, progress)`, `Launch()`
  * `IInjector` - `Inject(dllPath, pid)`, `IsClientRunning`
  * `IMicrosoftAccount` - `IsSignedIn`, `SignInAsync()`, `SignOutAsync()`
  * `ICredentialStore` - `Get/Set/Remove` (refresh tokens)
  * Version list for the UI comes from `VersionRegistry` (`VersionItem.Version/DownloadUris/GameLaunchHelper`).
* `src/Flarial.Runtime.Linux` - Linux backend. **Stubbed (seams for the Wine/Proton work):** `LinuxGameService` (reports "not installed", Install/Launch throw `NotImplementedException`), `LinuxInjector`, `LinuxMicrosoftAccount`. **Real:** `LinuxCredentialStore` (libsecret via `secret-tool`, falls back to a 0600 file).
* `src/Flarial.Runtime.Windows` - original Windows-only sources (GDK lookup, Injector, PackageService, PasswordVault, CsWin32, updater), moved out unchanged, not built.
* Self-update/MSIX migration is removed.

## Verification tools (`tools/`)
`capture-linux.sh` (state walk via `FLARIAL_SHOT`), `interact.py` (XTEST hover/press/click + frame bursts), `compare-bursts.py`, `frame-strip.py`. References in `docs/reference`, Linux frames in `docs/linux`.
