# UI deviations from upstream (flarialmc/launcher origin/main aa6b411)

No `.axaml`, style, animation, converter, control, font or image file was modified (verify:
`git diff 9073089 HEAD --stat -- '*.axaml' '*.ttf' '*.webp'` is empty). Only the following launcher lines changed.

| File | Change | Reason |
|---|---|---|
| `Flarial.Launcher.csproj` | TFM `net10.0-windows10.0.19041.0` -> `net10.0`; `Avalonia.Win32` -> `Avalonia.X11`; added `SkiaSharp.NativeAssets.Linux 4.150.1` (managed SkiaSharp 4.150.1 needs matching native lib; transitive one was 3.119); ProjectReference to `Flarial.Runtime.Linux`; dropped `ApplicationManifest` and its `AvaloniaResource Remove` | Windows-only |
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
* Settings > **Accounts** page (sidebar entry below Versions, same slide/zoom transition at Y=1500): Flarial (Discord) account reusing `SettingsGeneralViewModel`/`DiscordAccountModel` (the Discord section on General is kept), Microsoft account (xodus) and Xbox Live device-code sign-in (code shown in the existing `MessageBoxView`). Built only from existing styles/brushes/controls. Files: `Views/SettingsAccountsView.axaml(.cs)`, `ViewModels/SettingsAccountsViewModel.cs`, `Management/AccountsService.cs` (`IAccountsService`, fake default); minimal edits to `SettingsView.axaml(.cs)` (extra row/radio/page, Return button moved to row 4), `SettingsViewModel`, `App.axaml`, `PageTransitions`, `ScreenshotDriver`. Captures: `docs/linux/accounts-*.png`.
