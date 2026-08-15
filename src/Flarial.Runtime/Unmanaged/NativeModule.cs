using System;
using System.IO;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;
using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Unmanaged;

static class NativeModuleExtensions
{
    extension(LOAD_LIBRARY_FLAGS flags)
    {
        internal NativeModule? Open(string path)
        {
            return NativeModule.Open(flags, path);
        }
    }
}

unsafe readonly struct NativeModule : IDisposable
{
    readonly HMODULE _handle;

    NativeModule(HMODULE handle) => _handle = handle;

    public void Dispose() => FreeLibrary(_handle);

    internal static NativeModule? Open(LOAD_LIBRARY_FLAGS flags, string path)
    {
        fixed (char* ptr = path)
        {
            var handle = LoadLibraryEx(ptr, dwFlags: flags);
            return !handle.IsNull ? new(handle) : null;
        }
    }

    public static implicit operator HMODULE(in NativeModule module) => module._handle;
}