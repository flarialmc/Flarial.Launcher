using System;
using System.Threading.Tasks;
using Flarial.Runtime.Platform;

namespace Flarial.Runtime.Linux;

/// <summary>SEAM: Microsoft account sign-in (device code / browser flow) used to download the game from the Store.</summary>
public sealed class LinuxMicrosoftAccount : IMicrosoftAccount
{
    public bool IsSignedIn => false;

    public Task<bool> SignInAsync() => throw new NotImplementedException("Microsoft account sign-in is not implemented yet.");

    public Task SignOutAsync() => throw new NotImplementedException("Microsoft account sign-in is not implemented yet.");
}
