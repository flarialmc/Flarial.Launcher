using System;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using static Windows.Win32.Foundation.WAIT_EVENT;
using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Unmanaged;

static class NativeProcessExtensions
{
    extension(PROCESS_ACCESS_RIGHTS rights)
    {
        internal NativeProcess? Open(uint processId)
        {
            return NativeProcess.Open(rights, processId);
        }
    }
}

readonly struct NativeProcess : IDisposable
{
    readonly HANDLE _handle;

    NativeProcess(HANDLE handle) => _handle = handle;

    public void Dispose() => CloseHandle(_handle);

    internal static NativeProcess? Open(PROCESS_ACCESS_RIGHTS rights, uint processId)
    {
        var handle = OpenProcess(rights, false, processId);
        return !handle.IsNull ? new(handle) : null;
    }

    internal bool Wait(uint timeout) => WaitForSingleObject(_handle, timeout) is WAIT_TIMEOUT;

    public static implicit operator HANDLE(in NativeProcess process) => process._handle;
}
