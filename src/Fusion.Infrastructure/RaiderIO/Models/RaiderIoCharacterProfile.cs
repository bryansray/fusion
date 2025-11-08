using System.Text.Json.Serialization;

namespace Fusion.Infrastructure.RaiderIO.Models;

public sealed record RaiderIoCharacterProfile
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("class")]
    public string Class { get; init; } = string.Empty;

    [JsonPropertyName("race")]
    public string Race { get; init; } = string.Empty;

    [JsonPropertyName("active_spec_name")]
    public string? ActiveSpecName { get; init; }

    [JsonPropertyName("realm")]
    public string Realm { get; init; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("gear")]
    public CharacterGear? Gear { get; init; }

    [JsonPropertyName("mythic_plus_ranks")]
    public MythicPlusRanks? MythicPlusRanks { get; init; }

    [JsonPropertyName("mythic_plus_scores_by_season")]
    public IReadOnlyCollection<MythicPlusScoresBySeason> MythicPlusScoresBySeason { get; init; } = [];

    [JsonPropertyName("last_crawled_at")]
    public DateTimeOffset? LastCrawledAt { get; init; }
}

public sealed record MythicPlusScoresBySeason
{
    [JsonPropertyName("season")]
    public string Season { get; init; } = string.Empty;

    [JsonPropertyName("scores")]
    public IReadOnlyDictionary<string, float> Scores { get; init; } = new Dictionary<string, float>();
}

public sealed record CharacterGear
{
    [JsonPropertyName("item_level_equipped")]
    public double ItemLevelEquipped { get; init; }

    [JsonPropertyName("item_level_total")]
    public double ItemLevelTotal { get; init; }
}

public sealed record MythicPlusRanks
{
    [JsonPropertyName("overall")]
    public MythicPlusRank? Overall { get; init; }
}

public sealed record MythicPlusRank
{
    [JsonPropertyName("world")]
    public int? World { get; init; }

    [JsonPropertyName("region")]
    public int? Region { get; init; }

    [JsonPropertyName("realm")]
    public int? Realm { get; init; }
}
