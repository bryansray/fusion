using System.Collections.Immutable;

namespace Fusion.Infrastructure.Warcraft;

public static class BlizzardRegions
{
    public const string Us = "us";
    public const string Eu = "eu";
    public const string Kr = "kr";
    public const string Tw = "tw";
    public const string Cn = "cn";

    private static readonly ImmutableHashSet<string> SupportedRegions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        Us,
        Eu,
        Kr,
        Tw,
        Cn);

    public static IReadOnlyCollection<string> All => SupportedRegions;

    public static bool IsSupported(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return false;
        }

        var normalized = NormalizeInternal(region, throwIfInvalid: false);
        return normalized is not null && SupportedRegions.Contains(normalized);
    }

    public static string Normalize(string? region)
    {
        var normalized = NormalizeInternal(region, throwIfInvalid: true);
        return normalized ?? throw new InvalidOperationException("Region normalization failed.");
    }

    private static string? NormalizeInternal(string? region, bool throwIfInvalid)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            if (throwIfInvalid)
            {
                throw new ArgumentException("Region is required.", nameof(region));
            }

            return null;
        }

#pragma warning disable CA1308 // Normalize strings to uppercase
        var normalized = region.Trim().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        if (!SupportedRegions.Contains(normalized))
        {
            if (throwIfInvalid)
            {
                throw new ArgumentOutOfRangeException(nameof(region), region, "Unsupported Blizzard API region.");
            }

            return null;
        }

        return normalized;
    }
}
