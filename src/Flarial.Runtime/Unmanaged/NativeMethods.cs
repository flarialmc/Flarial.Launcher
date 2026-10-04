using System.Diagnostics;

namespace Flarial.Runtime.Unmanaged;

public static class NativeMethods
{
    /// <summary>Opens a URL or file with the desktop's default handler (xdg-open).</summary>
    public static void ShellExecute(string file)
    {
        try
        {
            using (Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { file }, UseShellExecute = false })) { }
        }
        catch { }
    }
}
