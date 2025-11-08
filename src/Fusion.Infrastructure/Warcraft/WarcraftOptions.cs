namespace Fusion.Infrastructure.Warcraft;

public sealed class WarcraftOptions
{
    public const string SectionName = "Warcraft";

    /// <summary>
    /// Default API region (us, eu, kr, tw). Defaults to us.
    /// </summary>
    public string Region { get; set; } = BlizzardRegions.Us;

    /// <summary>
    /// Locale used when querying Blizzard APIs (e.g., en_US).
    /// </summary>
    public string Locale { get; set; } = "en_US";

    /// <summary>
    /// OAuth client id from https://develop.battle.net.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret from https://develop.battle.net.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
