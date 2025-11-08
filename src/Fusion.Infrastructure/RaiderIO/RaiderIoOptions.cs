using Fusion.Infrastructure.Warcraft;

namespace Fusion.Infrastructure.RaiderIO;

public sealed class RaiderIoOptions
{
    public const string SectionName = "RaiderIO";

    /// <summary>
    /// Default region used for character lookups (us, eu, tw, kr, cn).
    /// </summary>
    public string Region { get; set; } = BlizzardRegions.Us;

    /// <summary>
    /// Base API URL. Defaults to https://raider.io/api/v1
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://raider.io/api/v1");

    /// <summary>
    /// Comma-delimited fields appended to character requests (e.g., mythic_plus_scores_by_season:current).
    /// </summary>
    public string? DefaultFields { get; set; }

    /// <summary>
    /// Optional API key for authenticated Raider.IO requests.
    /// </summary>
    public string? ApiKey { get; set; }
}
