Windows-only runtime sources, synced with upstream flarialmc/Flarial.Launcher main d7a4264 ("Use Flarial OAuth2.").
Covers: GDK/Minecraft lookup + bootstrap, the dependency-loading InjectionSession and its Injector, native
ModificationLibrary (LoadLibraryEx + exported-symbol checks), PackageService/TaskService, PasswordVault credentials,
CsWin32 NativeMethods, NativeDialog (TaskDialog), the MSIX self-updater, the SystemIdentification analytics id and
the native beta commit-hash check (Core/FlarialClientBeta.Windows.cs). Package/ holds the MSIX manifest and assets.
Not built on Linux. The cross-platform equivalents (managed PE parsing for the same checks) live in Flarial.Runtime.
A Windows backend implementing the interfaces in Flarial.Runtime/Platform can be ported from here.
