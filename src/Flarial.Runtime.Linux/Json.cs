using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flarial.Runtime.Linux;

static class Json
{
    public static string? Field(string path, string name)
    {
        try { return JsonNode.Parse(File.ReadAllText(path))?[name]?.GetValue<string>(); }
        catch { return null; }
    }

    public static JsonObject ReadObject(string path)
    {
        try { return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new(); }
        catch { return new(); }
    }

    /// <summary>Atomic 0600 write.</summary>
    public static void WriteSecret(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, path, true);
    }
}
