using System.Text.Json.Nodes;

namespace Flarial.Runtime.Linux;

/// <summary>Optional advanced settings from Root/settings.json (no UI): {"inject_delay":8,"custom_env":"K=V K=V","ray_tracing":false,"diagnostics":false}.</summary>
static class Settings
{
    static JsonObject Load() => Json.ReadObject(Paths.Settings);

    public static int InjectDelaySeconds => Load()["inject_delay"]?.GetValue<int>() ?? 8;
    public static string CustomEnv => Load()["custom_env"]?.GetValue<string>() ?? "";
    public static bool RayTracing => Load()["ray_tracing"]?.GetValue<bool>() ?? false;
    public static bool Diagnostics => Load()["diagnostics"]?.GetValue<bool>() ?? false;
}
