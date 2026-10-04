using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Flarial.Runtime.Core;

static class Program
{
    static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Beta token handoff assertion failed");
    }

    static unsafe void Main(string[] args)
    {
        var path = Path.GetFullPath(args[0]);
        var module = NativeLibrary.Load(path);
        try
        {
            var method = typeof(FlarialClient).Assembly.GetType("Flarial.Runtime.Game.BetaTokenHandoff")!
                .GetMethod("Deliver", BindingFlags.Static | BindingFlags.NonPublic)!;
            bool Deliver(string token) => (bool)method.Invoke(null, [(uint)Environment.ProcessId, path, token])!;
            var handoff = (byte*)NativeLibrary.GetExport(module, "FlarialBetaAccessTokenHandoff");
            foreach (var token in new[] { "synthetic-account-token", new string('x', 4096) })
            {
                Check(Deliver(token));
                Check(*(uint*)handoff == 1);
                Check(*(uint*)(handoff + 4) == token.Length);
                Check(Encoding.UTF8.GetString(new ReadOnlySpan<byte>(handoff + 8, token.Length)) == token);
                new Span<byte>(handoff, 4104).Clear();
            }
            foreach (var token in new[] { "", "bad\r\nheader", "bad\0token", new string('x', 4097) })
            {
                Check(!Deliver(token));
                Check(*(uint*)handoff == 0);
                Check(*(uint*)(handoff + 4) == 0);
            }
            Check(!(bool)method.Invoke(null, [0u, path, "synthetic-token"])!);
            Console.WriteLine("Beta token handoff tests passed");
        }
        finally { NativeLibrary.Free(module); }
    }
}
