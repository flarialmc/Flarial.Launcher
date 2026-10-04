# UI deviations from upstream (flarialmc/Flarial.Launcher main d7a4264 "Use Flarial OAuth2.")

No `.axaml`, style, animation, converter, control, font or image file was modified (compare with upstream: `git diff d7a4264 HEAD -- '*.axaml'` only shows the files listed below). Only the following launcher lines changed.

| File | Change | Reason |
|---|---|---|
| `Flarial.Launcher.csproj` | TFM `net10.0-windows10.0.19041.0` -> `net10.0`; `Avalonia.Win32` -> `Avalonia.X11`; added `SkiaSharp.NativeAssets.Linux 4.153.1` (matches managed SkiaSharp); dropped the MSIX tooling (`Microsoft.Windows.SDK.BuildTools*`, Release MSIX properties, `Package.appxmanifest`/`Assets`, kept in `Flarial.Runtime.Windows/Package`); ProjectReference to `Flarial.Runtime.Linux`; dropped `ApplicationManifest` and its `AvaloniaResource Remove` | Windows-only |
| `Resources/app.manifest` | deleted | Windows manifest |
| `Program.cs` | `UseWin32()` -> `UseX11()`; `LinuxPlatform.Use()` registers backend; data dir `Flarial\Launcher` -> `Path.Combine("Flarial","Launcher")` (=`~/.local/share/Flarial/Launcher`) | Windows-only / path separator |
| `AssemblyInfo.cs` | `SupportedOSPlatform("windows...")` -> `linux`; crash dialog (`TaskDialog`) now zenity/kdialog + stderr (same title/text) | Windows-only API |
| `Views/MainWindow.axaml.cs` | removed unused `using Windows.Win32;`; added `ScreenshotDriver.Start(this)` (no-op unless `FLARIAL_SHOT` is set) | Windows-only using / dev capture tool |
| `ViewModels/MainWindowViewModel.cs` | removed `LauncherMigrationDialog` (MSIX migration) and `FlarialLauncher.DownloadAsync` (self-update that downloads an installer and exits) from `OnLoaded` | self-update disabled entirely, MSIX is Windows-only |
| `ViewModels/SettingsGeneralViewModel.cs` | `@"..\Client"` -> `Path.Combine("..","Client")` | path separator |
| `ViewModels/VersionItemViewModel.cs` | "Minecraft not installed" dialog only when `Platform.Game.RequiresInstalledGame` (true on Windows, false on Linux) | on Linux installing a version creates the install |
| `Management/StorePage.cs` | `ms-windows-store://pdp/?ProductId=X` -> `https://apps.microsoft.com/detail/X` | no Store protocol on Linux |
| `Dialogs/Metadata/GameNotFoundDialog.cs` | button "Back" -> "Install" (closes dialog, opens Settings > Versions via the existing `PageTransitions` messages); body text no longer mentions Microsoft Store/Xbox App | intentional user-requested change (Linux installs through the Versions page) |
| `ViewModels/VersionItemViewModel.cs` (install) | a failed install is caught and shown as a notification instead of crashing the launcher (no "installed" dialog) | backend errors (disk space, sign-in, network) are expected on Linux |
| `ViewModels/MainWindowViewModel.cs` / `Program.cs` | register `LinuxPlatform.Notify` (-> `NotificationArea.Add`) and `AccountsService.Current` (real xodus/Xbox backend) | backend hooks |
| `Views/VersionItemView.axaml`, `ViewModels/VersionItemViewModel.cs`, `Dialogs/Metadata/DeleteVersionDialog.cs` (installed state) | per item: not on disk = Download; downloaded = red "Select" (the former `Button.installed` look) + the upstream trash button, now wired to a confirm dialog and delete; active = new greyed disabled `Button.selected` "Selected" + trash. `Button:disabled` dims to 0.6; the template's icon Viewbox hides when `Tag` is null so icon-less labels are centred. Select/Delete are disabled while the game runs or an install is active; `IGameService` gained `SelectVersion`/`DeleteVersion` | user-requested version management (upstream only had the Download state wired) |
| `Dialogs/Metadata/MicrosoftSignInRequiredDialog.cs` + `VersionItemViewModel.InstallAsync` | new existing-style dialog shown instead of starting a download when no Microsoft account is signed in; "Sign In" opens Settings > Accounts | fail fast instead of hanging at 0% |
| `Views/SettingsView.axaml.cs` | `PageTransition` also checks the matching sidebar RadioButton | pages opened from dialogs (Game Not Found, Sign In Required) left the previous sidebar button highlighted |
| `Views/SettingsGeneralView.axaml` | removed the Account section (upstream renamed Discord -> Account) (header, avatar/username/role row, Login/Logout); Folders now starts the page, remaining spacing unchanged | user request: the Flarial OAuth2 account lives only in Settings > Accounts, which binds the same `SettingsGeneralViewModel` login/logout/account state |
| `ViewModels/SettingsGeneralViewModel.cs` (Open Client Folder) | opens `LinuxPlatform.ClientDirectory` (`%LOCALAPPDATA%\Flarial\Client` inside the Wine prefix, where the injected client writes) instead of `..\Client` next to the launcher data | the client runs inside Wine |
| `AssemblyInfo.cs` / `Flarial.Runtime/Unmanaged/NativeDialog.cs` | crash dialog goes through `NativeDialog` (zenity/kdialog + stderr) instead of `TaskDialogIndirect` | Windows-only API |
| `ViewModels/MainWindowViewModel.cs` | launcher self-update (`CheckForUpdatesAsync`/`DownloadAsync`, `IProgress<int>`) removed from `OnLoaded` | self-update disabled entirely |
| `ScreenshotDriver.cs` | new dev tool | verification |

Dead on Linux but kept as-is: `LauncherMigrationDialog`, `LauncherUpdateAvailableDialog`, `AutomaticUpdates` setting (UI toggle still present, no effect).

## Windows-only visuals
* `AcrylicBlur`, `SpotlightDecorator` (SKRuntimeEffect), custom tooltip: pure Skia/Avalonia, run unchanged on Linux.
* Window: original already uses `WindowDecorations.None` + transparent window + `Border CornerRadius=25` (no HWND region/DWM code exists in this commit), so nothing needed replacing. Rounded corners need a compositing WM (GNOME/KDE fine).

## Remaining known visual differences (measured vs wine reference, see `pixel-diff.txt`)
* Glyph anti-aliasing: text pixels differ by 0.1-0.5% of the image (fuzz 3%). Layout/position identical. Tried `TextHintingMode` None/Light/Strong and `TextRenderingMode` Alias/Antialias/Subpixel; the default is closest. Cause: wine's Win32 backend vs native X11 FreeType/HarfBuzz rasterisation.
* Emoji in dialog titles (`dialog.png`, ~1.1%): wine prefix has no emoji font (renders a boxed glyph); Linux renders Noto Color Emoji (same colour emoji as real Windows Segoe UI Emoji). Cannot be made identical to the wine reference; a real Windows capture would not match either way.
* `notification.png` (~7%): captured at different points of the notification slide-in animation (timing), not a style difference.
* Settings > Versions list is empty in both (versions load from the network after startup).
* Transitions: frame strips (`transition-*.png`) show the same sequence/timing; mid-transition frame diffs are sampling jitter (capture is ~25 fps screen grabs, not frame-locked).
* Not verifiable on Linux/wine: real Windows DWM acrylic/mica behind the window (the original does not use it), Minecraft-dependent states (installed version colour, supported/unsupported).

## Intentional additions (user-requested)
* Settings > **Accounts** page (sidebar entry below Versions, same slide/zoom transition at Y=1500): Flarial (OAuth2) account reusing `SettingsGeneralViewModel`/`AccountModel`, Microsoft account (xodus) and Xbox Live device-code sign-in (code shown in the existing `MessageBoxView`). Built only from existing styles/brushes/controls. Files: `Views/SettingsAccountsView.axaml(.cs)`, `ViewModels/SettingsAccountsViewModel.cs`, `Management/AccountsService.cs` (`IAccountsService`, fake default); minimal edits to `SettingsView.axaml(.cs)` (extra row/radio/page, Return button moved to row 4), `SettingsViewModel`, `App.axaml`, `PageTransitions`, `ScreenshotDriver`. Captures: `docs/linux/accounts-*.png`.
