using System;
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

    /// <summary>Fatal error dialog: zenity/kdialog when present, always echoed to stderr.</summary>
    public static void TaskDialog(nint handle, string title, string? instruction, string content, string? information)
    {
        var text = $"{instruction}\n\n{content}\n\n{information}";
        Console.Error.WriteLine($"{title}\n{text}");

        foreach (var (tool, args) in new[] { ("zenity", new[] { "--error", "--title", title, "--text", text }), ("kdialog", new[] { "--title", title, "--error", text }) })
        {
            try
            {
                ProcessStartInfo info = new(tool) { UseShellExecute = false };
                foreach (var arg in args) info.ArgumentList.Add(arg);
                using var process = Process.Start(info);
                process?.WaitForExit();
                return;
            }
            catch { }
        }
    }
}
