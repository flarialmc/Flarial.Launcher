using System;
using System.Collections.Generic;
using Flarial.Runtime.Unmanaged;
using Microsoft.VisualBasic;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.Memory.PAGE_PROTECTION_FLAGS;
using static Windows.Win32.System.Memory.VIRTUAL_ALLOCATION_TYPE;
using static Windows.Win32.System.Memory.VIRTUAL_FREE_TYPE;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;
using static Windows.Win32.System.Threading.PROCESS_CREATION_FLAGS;

namespace Flarial.Runtime.Game;

partial class InjectionSession
{
    unsafe bool Inject(uint processId)
    {
        if (PROCESS_ALL_ACCESS.Open(processId) is not { } process)
            return false;

        using (process)
        {
            HANDLE thread = new();
            List<nint> items = [];
            try
            {
                thread = CreateRemoteThread(process, null, 0, s_thread, null, (uint)CREATE_SUSPENDED, null);

                foreach (var path in _paths)
                {
                    var size = (nuint)(path.Length + 1) * sizeof(char);
                    var item = VirtualAllocEx(process, null, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                    fixed (char* ptr = path)
                    {
                        items.Add((nint)item);
                        WriteProcessMemory(process, item, ptr, size, null);
                    }

                    QueueUserAPC(s_apc, thread, (nuint)item);
                }

                ResumeThread(thread);
                WaitForSingleObject(thread, INFINITE);
            }
            finally
            {
                TerminateThread(thread, 0); CloseHandle(thread);
                foreach (var item in items) VirtualFreeEx(process, (void*)item, 0, MEM_RELEASE);
            }
        }

        return true;
    }
}