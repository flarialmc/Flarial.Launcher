using System;

namespace Flarial.Runtime.Exceptions;

sealed class MinecraftElevatedException : Exception
{
    internal MinecraftElevatedException() : base("Minecraft is running with elevated privileges.") {}
}