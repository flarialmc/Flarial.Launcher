namespace Flarial.Runtime.Game;

public static class Injector
{
    public static bool Launch(Library library)
    {
        var path = library.EnsureLoadable();

        if (Platform.Platform.Game.Launch() is not { } processId)
            return false;

        return Platform.Platform.Injector.Inject(path, processId);
    }
}
