> [!CAUTION]
> ### Please submit pull requests when contributing!

# Flarial Launcher

The official launcher for Flarial Client.

## Notes
- This repository only contains the source code for the launcher.
  
- If you want to install the launcher, please download the installer:

  - Download: https://cdn.flarial.xyz/launcher/Flarial.Launcher.Installer.exe
  
  - Repository: https://github.com/flarialmc/Flarial.Launcher.Installer

## Layout
- `windows/` - the Windows launcher (Avalonia UI over `Flarial.Runtime`, packaged as MSIX).
  - Build (Windows): `dotnet publish windows\src\Flarial.Launcher\Flarial.Launcher.csproj`
  - Output: `windows\src\Flarial.Launcher\AppPackages\Flarial.Launcher_Test\Flarial.Launcher.msix`
- `linux/` - the native Linux launcher (see `linux/README.md`).
  - Build: `dotnet build linux/src/Flarial.Launcher -c Debug`
  - Publish: `dotnet publish linux/src/Flarial.Launcher -c Release -r linux-x64 --self-contained`
