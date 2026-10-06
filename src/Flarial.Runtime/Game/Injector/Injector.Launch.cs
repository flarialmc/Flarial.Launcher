using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.Memory.PAGE_PROTECTION_FLAGS;
using static Windows.Win32.System.Memory.VIRTUAL_ALLOCATION_TYPE;
using static Windows.Win32.System.Memory.VIRTUAL_FREE_TYPE;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;

namespace Flarial.Runtime.Game;

partial class Injector
{
    public unsafe static bool Launch(ModificationLibrary library)
    {
        var path = library.AsPath();

        if (Minecraft.Launch() is not { } processId)
            return false;

        if (PROCESS_ALL_ACCESS.Open(processId) is not { } process)
            return false;

        using (process)
        {
            HANDLE thread = new();
            void* parameter = null;
            try
            {
                var size = (nuint)(path.Length + 1) * sizeof(char);

                parameter = VirtualAllocEx(process, null, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                fixed (char* buffer = path) WriteProcessMemory(process, parameter, buffer, size, null);

                thread = CreateRemoteThread(process, null, 0, s_address, parameter, 0, null);
                WaitForSingleObject(thread, INFINITE);

                return true;
            }
            finally
            {
                CloseHandle(thread);
                VirtualFreeEx(process, parameter, 0, MEM_RELEASE);
            }
        }
    }
}