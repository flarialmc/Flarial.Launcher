using System.Collections.Generic;

namespace Flarial.Runtime.Game;

/// <summary>Loads a DLL's imported libraries (from the system directory) into the game before the DLL itself.</summary>
sealed class InjectionSession
{
    readonly IReadOnlyList<string> _paths;

    InjectionSession(IReadOnlyList<string> paths) => _paths = paths;

    static InjectionSession? Create(ModificationLibrary library)
    {
        var path = library.EnsureLoadable();

        if (library.Open() is not { } image)
            return null;

        // Same as upstream: a DLL without an import table is not injected.
        if (image.Imports.Count is 0)
            return null;

        var system = Platform.Platform.Injector.SystemDirectory.TrimEnd('\\');

        List<string> paths = [];
        foreach (var name in image.Imports)
            paths.Add($"{system}\\{name}");

        paths.Add(path);
        return new(paths);
    }

    internal static bool Launch(ModificationLibrary library)
    {
        if (Create(library) is not { } session)
            return false;

        if (Platform.Platform.Game.Launch() is not { } processId)
            return false;

        return Platform.Platform.Injector.Inject(session._paths, processId);
    }
}
