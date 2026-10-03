using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Game;

sealed partial class InjectionSession
{
    static unsafe readonly delegate* unmanaged[Stdcall]<nuint, void> s_apc;
    static unsafe readonly delegate* unmanaged[Stdcall]<void*, uint> s_thread;

    unsafe static InjectionSession()
    {
        fixed (char* moduleNamePtr = "Kernel32")
        fixed (byte* procedureNamePtr = "LoadLibraryW"u8)
        {
            var module = GetModuleHandle(moduleNamePtr);
            var address = GetProcAddress(module, new(procedureNamePtr));

            s_apc = (delegate* unmanaged[Stdcall]<nuint, void>)(nint)address;
            s_thread = (delegate* unmanaged[Stdcall]<void*, uint>)(nint)address;
        }
    }

    internal static bool Launch(ModificationLibrary library, string? betaAccessToken = null)
    {
        if (Create(library) is not { } session)
            return false;

        if (Minecraft.Launch() is not { } processId)
            return false;

        using var handoff = betaAccessToken is null ? null : new BetaTokenHandoff(processId, betaAccessToken);
        return session.Inject(processId) && (handoff?.WaitForDelivery() ?? true);
    }
}