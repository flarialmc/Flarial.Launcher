using System;
using System.Runtime.InteropServices;
using Flarial.Runtime.Unmanaged;
using Windows.UI.Text;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using static Windows.Win32.PInvoke;
using static Windows.Win32.Security.TOKEN_ACCESS_MASK;
using static Windows.Win32.Security.TOKEN_INFORMATION_CLASS;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;


namespace Flarial.Runtime.Unmanaged;


static class NativeProcessTokenExtensions
{
    extension(TOKEN_ACCESS_MASK access)
    {
        internal NativeProcessToken? Open(in NativeProcess process)
        {
            return NativeProcessToken.Open(access, process);
        }
    }
}


unsafe readonly struct NativeProcessToken : IDisposable
{
    readonly HANDLE _handle;

    NativeProcessToken(HANDLE handle) => _handle = handle;

    internal static NativeProcessToken? Open(TOKEN_ACCESS_MASK access, in NativeProcess process)
    {
        HANDLE handle = new();

        if (!OpenProcessToken(process, access, &handle))
            return null;

        return new(handle);
    }

    public void Dispose() => CloseHandle(_handle);

    public static implicit operator HANDLE(in NativeProcessToken token) => token._handle;
}