using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;
using Flarial.Runtime.Game;

const string token = "fake.header.signature";
if (args.Length != 0)
{
    var pipeName = Console.ReadLine()!;
    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
    await client.ConnectAsync(5000);
    byte[] prefix = new byte[4];
    if (args[0] == "reject")
    {
        try { return await client.ReadAsync(prefix) == 0 ? 0 : 1; }
        catch (IOException) { return 0; }
    }
    await client.ReadExactlyAsync(prefix);
    var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
    if (length != token.Length) return 1;
    byte[] bytes = new byte[length];
    await client.ReadExactlyAsync(bytes);
    return System.Text.Encoding.UTF8.GetString(bytes) == token ? 0 : 1;
}
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Run on Windows.");

foreach (bool wrongProcess in new[] { false, true })
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, UseShellExecute = false };
    info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    info.ArgumentList.Add(wrongProcess ? "reject" : "receive");
    using var child = Process.Start(info)!;
    uint target = (uint)(wrongProcess ? Environment.ProcessId : child.Id);
    using var handoff = new BetaTokenHandoff(target, token);
    await child.StandardInput.WriteLineAsync($"Flarial.Beta.Auth.{target}");
    await child.StandardInput.FlushAsync();
    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    if (child.ExitCode != 0) throw new Exception("Unexpected client result.");
    if (!wrongProcess && !handoff.WaitForDelivery()) throw new Exception("Delivery failed.");
}
foreach (var invalid in new[] { "", new string('x', 4097) })
{
    try { using var handoff = new BetaTokenHandoff((uint)Environment.ProcessId, invalid); }
    catch (ArgumentException) { continue; }
    throw new Exception("Invalid token accepted.");
}
Console.WriteLine("PASS: target process receives token; other process rejected; cancellation and length limits.");
return 0;
