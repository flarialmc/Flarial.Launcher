using System;
using System.Diagnostics;

namespace Flarial.Runtime.Unmanaged;

/// <summary>Fatal error dialog: zenity/kdialog when present, always echoed to stderr (Windows: TaskDialogIndirect, see Flarial.Runtime.Windows).</summary>
public readonly struct NativeDialog
{
    public required nint Handle { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string? Instruction { get; init; }
    public required string? Information { get; init; }

    public void Show()
    {
        var text = $"{Instruction}\n\n{Content}\n\n{Information}";
        Console.Error.WriteLine($"{Title}\n{text}");

        foreach (var (tool, args) in new[] { ("zenity", new[] { "--error", "--title", Title, "--text", text }), ("kdialog", new[] { "--title", Title, "--error", text }) })
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
