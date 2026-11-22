using System;
using System.Collections.Generic;
using System.IO;
using Flarial.Runtime.Exceptions;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.SystemServices;
using static System.Environment;
using static Windows.Win32.System.Diagnostics.Debug.IMAGE_DIRECTORY_ENTRY;
using static Windows.Win32.System.Diagnostics.Debug.IMAGE_FILE_CHARACTERISTICS;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Game;

public unsafe sealed class ModificationLibrary(string? path)
{
    public bool IsLoadable
    {
        get
        {
            if (_path is null)
                return false;

            if (DONT_RESOLVE_DLL_REFERENCES.Open(_path) is not { } module)
                return false;

            using (module)
            {
                var dos = (IMAGE_DOS_HEADER*)(void*)module;
                var nt = (IMAGE_NT_HEADERS64*)(module + dos->e_lfanew);
                return nt->FileHeader.Characteristics.HasFlag(IMAGE_FILE_DLL);
            }
        }
    }

    internal string AsPath()
    {
        if (_path is null)
            throw new LibraryLoadFailureException();

        if (!IsLoadable)
            throw new LibraryLoadFailureException();

        return _path;
    }

    [Obsolete("", true)]
    internal IReadOnlyList<string> AsImports()
    {
        var path = AsPath();
        var s_system = GetFolderPath(SpecialFolder.System);

        if (DONT_RESOLVE_DLL_REFERENCES.Open(path) is not { } module)
            throw new LibraryLoadFailureException();

        using (module)
        {
            var dos = (IMAGE_DOS_HEADER*)(void*)module;
            var nt = (IMAGE_NT_HEADERS64*)(module + dos->e_lfanew);

            var directory = nt->OptionalHeader.DataDirectory.AsReadOnlySpan();
            var entry = directory[(int)IMAGE_DIRECTORY_ENTRY_IMPORT];

            if (entry.VirtualAddress <= 0)
                throw new LibraryLoadFailureException();

            List<string> items = [];

            for (var import = (IMAGE_IMPORT_DESCRIPTOR*)(module + entry.VirtualAddress); import->Name > 0; import++)
            {
                var item = (sbyte*)(module + import->Name);
                items.Add(Path.Combine(s_system, new(item)));
            }

            items.Add(path);
            return items;
        }
    }

    readonly string? _path = Path.HasExtension(path) ? Path.GetFullPath(path) : null;
}