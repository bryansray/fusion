using System.Text.Json.Serialization;

namespace Fusion.Infrastructure.Warcraft.Models;

public sealed record CharacterProfile
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("faction")]
    public KeyedSummary? Faction { get; init; }

    [JsonPropertyName("race")]
    public KeyedSummary? Race { get; init; }

    [JsonPropertyName("character_class")]
    public KeyedSummary? CharacterClass { get; init; }

    [JsonPropertyName("realm")]
    public RealmSummary? Realm { get; init; }

    [JsonPropertyName("last_login_timestamp")]
    public long? LastLoginTimestamp { get; init; }

    [JsonPropertyName("item_level")]
    public int? ItemLevel { get; init; }
}

public record KeyedSummary
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed record RealmSummary : KeyedSummary
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}
