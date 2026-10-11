using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Flarial.Runtime.Core;
using Flarial.Runtime.Identity;

if (args.Length is not (2 or 4) || !File.Exists(args[0]) ||
    args[1] is not ("production" or "preproduction") ||
    (args.Length == 4 && args[1] != "preproduction"))
    throw new ArgumentException("Expected a local PE DLL fixture, trusted authority, and optional canary pins.");

var preproduction = args[1] == "preproduction";
using (var request = AccountManager.CreateAccountRequest("local-account-sentinel"))
{
    var expectedUrl = preproduction
        ? "https://flarial-api-site-preprod.ashanksundaram2.workers.dev/api/v1/launcher/account"
        : "https://api.flarial.xyz/api/v2/account";
    Require(request.RequestUri?.AbsoluteUri == expectedUrl,
        "Launcher account request used the wrong trusted route.");
    Require(request.Method == HttpMethod.Get && request.Content is null &&
            request.Headers.Authorization is { Scheme: "Bearer", Parameter: "local-account-sentinel" },
        "Launcher account request lost its bearer GET contract.");
}

var accessKey = preproduction ? "tester" : "beta";
foreach (var (active, plus, expectedAccess) in new[]
         { ("true", false, true), ("false", true, false) })
{
    var account = ReadAccountSnapshot(accessKey, active, plus);
    Require(account.Username == "Account contract" && account.HasBetaAccess == expectedAccess &&
            account.HasFlarialPlus == plus && await account.GetAvatarAsync() is null,
        "Launcher account response lost its name or independent entitlement decisions.");
}
if (preproduction)
{
    AccountDetails account;
    try { account = ReadAccountSnapshot("tester", "null", true); }
    catch (Exception exception)
    {
        throw new InvalidOperationException(
            "Unavailable preproduction tester decision rejected account login.", exception);
    }
    Require(account.Username == "Account contract" && !account.HasBetaAccess && account.HasFlarialPlus,
        "Unavailable preproduction tester decision granted access or lost account data.");
}
else RequireAccountRejected("beta", "null");
foreach (var malformed in new[] { "\"true\"", "1", "{}" })
    RequireAccountRejected(accessKey, malformed);
RequireAccountRejected(preproduction ? "beta" : "tester", "true");
Console.WriteLine("Launcher account request and response contracts PASS.");

var description = FlarialClient.CreateLaunchDescription("local-sentinel");
using (var json = JsonDocument.Parse(description))
{
    var root = json.RootElement;
    Require(root.GetProperty("access_token").GetString() == "local-sentinel",
        "Handoff lost its token value.");
    Require(root.GetProperty("account_authority").GetString() == args[1],
        "Handoff has the wrong build authority.");
}

var canaryMetadata = typeof(PreproductionCanary).Assembly
    .GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
var pathPins = canaryMetadata.Where(item => item.Key == "Flarial.PreproductionDllPath").ToArray();
var hashPins = canaryMetadata.Where(item => item.Key == "Flarial.PreproductionDllSha256").ToArray();
if (args[1] == "preproduction")
{
    var expectedPath = args.Length == 4 ? args[2] : string.Empty;
    var expectedHash = args.Length == 4 ? args[3] : string.Empty;
    Require(pathPins.Length == 1 && pathPins[0].Value == expectedPath &&
            hashPins.Length == 1 && hashPins[0].Value == expectedHash,
        "Preproduction build artifact metadata differs from the trusted build pins.");
}
else
{
    Require(pathPins.Length == 0 && hashPins.Length == 0,
        "Production build contains preproduction artifact metadata.");
}

var directory = Path.Combine(Path.GetTempPath(), "flarial-authority-owner-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var filePath = Path.Combine(directory, "canary.dll");
try
{
    File.Copy(args[0], filePath);
    string hash;
    using (var source = File.OpenRead(filePath))
        hash = Convert.ToHexString(SHA256.HashData(source));

    using (var held = PreproductionCanary.OpenVerifiedArtifact(filePath, hash))
    {
        Require(held is not null, "Correct local DLL/hash was rejected.");
        var writeDenied = false;
        try
        {
            using var writer = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            writeDenied = true;
        }
        Require(writeDenied, "Verified DLL was writable while the launch owner held it.");
    }

    using (var wrong = PreproductionCanary.OpenVerifiedArtifact(filePath, new string('0', 64)))
        Require(wrong is null, "Wrong DLL hash was accepted.");
    using (var missing = PreproductionCanary.OpenVerifiedArtifact(Path.Combine(directory, "missing.dll"), hash))
        Require(missing is null, "Missing DLL was accepted.");
    using (var unset = PreproductionCanary.OpenVerifiedArtifact(filePath, null))
        Require(unset is null, "Unset build hash was accepted.");

    using (var writer = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
        writer.WriteByte(0xA5);
    using (var modified = PreproductionCanary.OpenVerifiedArtifact(filePath, hash))
        Require(modified is null, "Modified but loadable DLL was accepted under the old hash.");

    Console.WriteLine("Authority handoff and local artifact owner PASS.");
}
finally
{
    File.Delete(filePath);
    Directory.Delete(directory);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static AccountDetails ReadAccountSnapshot(string accessKey, string active, bool plus)
{
    var plusJson = plus ? "true" : "false";
    using var document = JsonDocument.Parse($$"""
        {
          "user": { "display_name": "Account contract", "avatar_url": null },
          "entitlements": {
            "{{accessKey}}": { "active": {{active}} },
            "flarial_plus": { "active": {{plusJson}} }
          }
        }
        """);
    return AccountManager.ReadAccountResponse(document.RootElement);
}

static void RequireAccountRejected(string accessKey, string active)
{
    try { _ = ReadAccountSnapshot(accessKey, active, true); }
    catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or JsonException)
    {
        return;
    }
    throw new InvalidOperationException("A missing or malformed selected account entitlement was accepted.");
}
