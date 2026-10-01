using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Game;

public static partial class Injector
{
    static unsafe readonly delegate* unmanaged[Stdcall]<void*, uint> s_address;

    unsafe static Injector()
    {
        fixed (char* moduleNamePtr = "Kernel32")
        fixed (byte* procedureNamePtr = "LoadLibraryW"u8)
        {
            var module = GetModuleHandle(moduleNamePtr);
            var address = GetProcAddress(module, new(procedureNamePtr));
            s_address = (delegate* unmanaged[Stdcall]<void*, uint>)(nint)address;
        }
    }
}