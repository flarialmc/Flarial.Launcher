using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Flarial.Runtime.Linux.Engine;

/// <summary>Hidden check, called by reflection or a `--selftest-engine` hook: EngineSelfTest.RunAsync().</summary>
static class EngineSelfTest
{
    public static async Task<int> RunAsync()
    {
        Paths.Ensure();
        IEngine e = new EngineManager();
        Console.WriteLine($"proton ready={e.IsProtonReady} umu ready={e.IsUmuReady} rev={e.Revision}");
        Console.WriteLine($"wine={e.Wine}\nwineserver={e.Wineserver} exists={File.Exists(e.Wineserver)}\numu={e.UmuRun}");
        var fail = !e.IsProtonReady || !e.IsUmuReady;

        if (File.Exists(e.Wine))
        {
            var p = System.Diagnostics.Process.Start(Proc.Info(e.Wine, ["--version"]))!;
            Console.WriteLine("wine --version: " + (await p.StandardOutput.ReadToEndAsync()).Trim());
            await p.WaitForExitAsync(); fail |= p.ExitCode != 0;
        }

        foreach (var url in new[] { EngineManager.EngineUrl, EngineManager.UmuUrl })
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url) { Headers = { Range = new(0, 15) } };
            using var res = await Download.Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            Console.WriteLine($"{(int)res.StatusCode} {res.Content.Headers.ContentRange} {url}");
            fail |= !res.IsSuccessStatusCode;
        }

        var f = Path.Combine(Path.GetTempPath(), "flarial-engine-selftest.bin");
        await File.WriteAllTextAsync(f, "abc");
        var ok = await Download.Sha256Async(f) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        Console.WriteLine("sha256 known-answer: " + ok);
        File.Delete(f);
        return fail || !ok ? 1 : 0;
    }
}
