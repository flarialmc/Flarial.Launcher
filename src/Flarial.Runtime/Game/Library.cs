using System.IO;
using Flarial.Runtime.Exceptions;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.SystemServices;
using static Windows.Win32.System.Diagnostics.Debug.IMAGE_FILE_CHARACTERISTICS;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;

namespace Flarial.Runtime.Game;

public unsafe sealed class Library(string? path)
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
                var dos = (IMAGE_DOS_HEADER*)(void*)(HMODULE)module;
                var nt = (IMAGE_NT_HEADERS64*)((nint)dos + dos->e_lfanew);
                return nt->FileHeader.Characteristics.HasFlag(IMAGE_FILE_DLL);
            }
        }
    }

    internal string EnsureLoadable()
    {
        if (_path is null)
            throw new LibraryLoadFailureException();

        if (!IsLoadable)
            throw new LibraryLoadFailureException();

        return _path;
    }

    readonly string? _path = Path.HasExtension(path) ? Path.GetFullPath(path) : null;
}