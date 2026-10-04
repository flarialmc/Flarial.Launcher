using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;

namespace Flarial.Runtime.Unmanaged;

unsafe readonly struct NativeWindow
{
    readonly HWND _handle;
    internal readonly uint _processId;

    internal bool IsVisible => IsWindowVisible(_handle);
    internal void SetActive() => SwitchToThisWindow(_handle, true);

    NativeWindow(HWND handle, uint processId)
    {
        _handle = handle;
        _processId = processId;
    }

    internal static NativeWindow? Open(HWND handle)
    {
        uint processId = 0;

        if (GetWindowThreadProcessId(handle, &processId) < 0)
            return null;

        return new(handle, processId);
    }

    public static implicit operator HWND(in NativeWindow window) => window._handle;
}