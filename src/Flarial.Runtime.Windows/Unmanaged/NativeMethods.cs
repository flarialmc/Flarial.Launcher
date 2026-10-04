using System;
using Windows.Win32;
using static Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD;

namespace Flarial.Runtime.Unmanaged;

public unsafe static class NativeMethods
{
    public static void ShellExecute(string file)
    {
        fixed (char* ptr = file)
            PInvoke.ShellExecute(lpFile: ptr, nShowCmd: SW_NORMAL);
    }
}