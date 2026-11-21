using System;

namespace Flarial.Runtime.Exceptions;

[Obsolete(" ", true)]
sealed class MinecraftSideloadedException : Exception
{
    internal MinecraftSideloadedException() : base("Minecraft is sideloaded, please reinstall it.") { }
}