using System.IO;
using Flarial.Runtime.Exceptions;

namespace Flarial.Runtime.Game;

public sealed class ModificationLibrary(string? path)
{
    // Upstream: LoadLibraryEx(DONT_RESOLVE_DLL_REFERENCES) succeeds and the image has IMAGE_FILE_DLL; checked on the PE headers instead.
    public bool IsLoadable => _path is { } && PortableImage.Open(_path) is { };

    internal PortableImage? Open() => _path is { } ? PortableImage.Open(_path) : null;

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
