namespace Flarial.Runtime.Game;

/// <summary>Injects a custom (user supplied) DLL on its own, without loading its dependencies first (as upstream).</summary>
public static class Injector
{
    public static bool Launch(ModificationLibrary library)
    {
        var path = library.EnsureLoadable();

        if (Platform.Platform.Game.Launch() is not { } processId)
            return false;

        return Platform.Platform.Injector.Inject([path], processId);
    }
}
