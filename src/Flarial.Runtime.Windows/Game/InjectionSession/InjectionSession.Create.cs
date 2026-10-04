using System.Collections.Generic;
using System.IO;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.SystemServices;
using static System.Environment;
using static Windows.Win32.System.Diagnostics.Debug.IMAGE_DIRECTORY_ENTRY;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Game;

partial class InjectionSession
{
    static readonly string s_system = GetFolderPath(SpecialFolder.System);

    readonly IReadOnlyList<string> _paths;

    InjectionSession(IReadOnlyList<string> paths) => _paths = paths;

    unsafe static InjectionSession? Create(ModificationLibrary library)
    {
        var path = library.EnsureLoadable();

        if (DONT_RESOLVE_DLL_REFERENCES.Open(path) is not { } module)
            return null;

        using (module)
        {
            var image = (nint)(void*)(HMODULE)module;

            var dos = (IMAGE_DOS_HEADER*)image;
            var nt = (IMAGE_NT_HEADERS64*)(image + dos->e_lfanew);

            var directory = nt->OptionalHeader.DataDirectory.AsReadOnlySpan();
            var entry = directory[(int)IMAGE_DIRECTORY_ENTRY_IMPORT];

            if (entry.VirtualAddress > 0)
            {
                List<string> paths = [];

                for (var import = (IMAGE_IMPORT_DESCRIPTOR*)(image + entry.VirtualAddress); import->Name > 0; import++)
                {
                    var ptr = (sbyte*)(image + import->Name);
                    paths.Add(Path.Combine(s_system, new(ptr)));
                }

                paths.Add(path);
                return new(paths);
            }
        }

        return null;
    }
}