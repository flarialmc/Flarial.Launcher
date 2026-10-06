using System.Collections.Generic;
using Flarial.Runtime.Game;
using Flarial.Runtime.Services;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.Memory.PAGE_PROTECTION_FLAGS;
using static Windows.Win32.System.Memory.VIRTUAL_ALLOCATION_TYPE;
using static Windows.Win32.System.Memory.VIRTUAL_FREE_TYPE;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;
using static Windows.Win32.System.Threading.PROCESS_CREATION_FLAGS;

namespace Flarial.Runtime.Core;

partial class FlarialClient<T>
{
    static class Loader
    {
        static unsafe readonly delegate* unmanaged[Stdcall]<nuint, void> s_apc;
        static unsafe readonly delegate* unmanaged[Stdcall]<void*, uint> s_start;

        unsafe static Loader()
        {
            fixed (char* moduleNamePtr = "Kernel32")
            fixed (byte* procedureNamePtr = "LoadLibraryW"u8)
            {
                var module = GetModuleHandle(moduleNamePtr);
                var address = GetProcAddress(module, new(procedureNamePtr));

                s_apc = (delegate* unmanaged[Stdcall]<nuint, void>)(nint)address;
                s_start = (delegate* unmanaged[Stdcall]<void*, uint>)(nint)address;
            }
        }

        internal unsafe static bool Launch()
        {
            var imports = new ModificationLibrary(_.FileName).AsImports();

            if (Minecraft.Launch() is not { } processId)
                return false;

            if (PROCESS_ALL_ACCESS.Open(processId) is not { } process)
                return false;

            using (process)
            {
                HANDLE thread = new();
                List<nint> items = [];
                try
                {
                    thread = CreateRemoteThread(process, null, 0, s_start, null, (uint)CREATE_SUSPENDED, null);

                    foreach (var import in imports)
                    {
                        var size = (nuint)(import.Length + 1) * sizeof(char);
                        var item = VirtualAllocEx(process, null, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                        fixed (char* path = import)
                        {
                            items.Add((nint)path);
                            WriteProcessMemory(process, item, path, size, null);
                        }

                        QueueUserAPC(s_apc, thread, (nuint)item);
                    }

                    Dictionary<string, string?> description = new() { ["access_token"] = AccessToken };
                    fixed (char* ptr = JsonService.Default.Write(description)) SetThreadDescription(thread, ptr);

                    ResumeThread(thread);
                    WaitForSingleObject(thread, INFINITE);

                    return true;
                }
                finally
                {
                    TerminateThread(thread, 0); CloseHandle(thread);
                    foreach (var item in items) VirtualFreeEx(process, (void*)item, 0, MEM_RELEASE);
                }
            }
        }
    }
}