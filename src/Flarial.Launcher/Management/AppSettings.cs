using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using Flarial.Runtime.Identity;
using Flarial.Runtime.Services;

namespace Flarial.Launcher.Management;

[JsonConverter(typeof(JsonStringEnumConverter<BuildType>))]
public enum BuildType { Beta, Custom, Release, PreproductionCanary }

public sealed class AppSettings
{
    public bool AutomaticUpdates { get; set; } = true;

    public bool PerformanceMode { get; set; } = false;

    public BuildType BuildType { get; set; } = AccountAuthorityProfile.IsPreproduction
        ? BuildType.PreproductionCanary : BuildType.Release;

    public string? CustomDllPath
    {
        get;
        set
        {
            if (value is null) field = value;
            else field = Path.GetFullPath(value);
        }
    }

    internal static AppSettings Get()
    {
        try
        {
            using var stream = File.OpenRead("Flarial.Launcher.json");
            return JsonService.Default.Read<AppSettings>(stream);
        }
        catch { return new(); }
    }

    internal void Set()
    {
        using var stream = File.Create("Flarial.Launcher.json");
        JsonService.Default.Write(stream, this);
    }
}
