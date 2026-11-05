using System.Runtime.InteropServices;

namespace Flarial.Runtime.Unmanaged;

unsafe static class NativeMemoryExtensions
{
    extension<T>(NativeMemory) where T : unmanaged
    {
        internal static T* Alloc(nuint byteCount)
        {
            return (T*)NativeMemory.Alloc(byteCount);
        }
    }
}