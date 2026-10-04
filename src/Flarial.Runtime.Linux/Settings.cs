using System.Text.Json.Nodes;

namespace Flarial.Runtime.Linux;

/// <summary>Optional advanced settings from Root/settings.json (no UI): {"inject_delay":8,"custom_env":"K=V K=V","ray_tracing":false,"diagnostics":false}.</summary>
static class Settings
{
    static JsonObject Load() => Json.ReadObject(Paths.Settings);

    public static int InjectDelaySeconds => Load()["inject_delay"]?.GetValue<int>() ?? 15; // the most we wait for the game to be ready; injection happens as soon as it is (inject_settle_ms after its window exists)
    public static int InjectSettleMs => Load()["inject_settle_ms"]?.GetValue<int>() ?? 2000;
    public static string CustomEnv => Load()["custom_env"]?.GetValue<string>() ?? "";
    public static bool RayTracing => Load()["ray_tracing"]?.GetValue<bool>() ?? false;
    public static bool Diagnostics => Load()["diagnostics"]?.GetValue<bool>() ?? false;
}
