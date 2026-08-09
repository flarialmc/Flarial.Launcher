using System.Text.Json.Serialization;

namespace Flarial.Runtime.Discord;

sealed class AccountEntitlements
{
    [JsonConstructor]
    internal AccountEntitlements(string? avatar, string username, string discordId, bool hasTesterRole, bool hasFlarialPlus)
    {
        Avatar = avatar;
        Username = username;
        DiscordId = discordId;
        HasTesterRole = hasTesterRole;
        HasFlarialPlus = hasFlarialPlus;
    }

    [JsonPropertyName("avatar")] public string? Avatar { get; }
    [JsonPropertyName("username")] public string Username { get; }
    [JsonPropertyName("discordId")] public string DiscordId { get; }
    [JsonPropertyName("hasTesterRole")] public bool HasTesterRole { get; }
    [JsonPropertyName("hasFlarialPlus")] public bool HasFlarialPlus { get; }
}