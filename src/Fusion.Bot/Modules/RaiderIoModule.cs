using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Fusion.Infrastructure.RaiderIO;
using Fusion.Infrastructure.RaiderIO.Models;
using Microsoft.Extensions.Logging;

namespace Fusion.Bot.Modules;

[Group("raiderio", "Raider.IO utilities.")]
public sealed class RaiderIoModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly IReadOnlyDictionary<string, Color> ClassColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
    {
        ["Death Knight"] = new Color(0xC4, 0x1F, 0x3B),
        ["Demon Hunter"] = new Color(0xA3, 0x30, 0xC9),
        ["Druid"] = new Color(0xFF, 0x7D, 0x0A),
        ["Evoker"] = new Color(0x33, 0x93, 0x7F),
        ["Hunter"] = new Color(0xAB, 0xD4, 0x73),
        ["Mage"] = new Color(0x3F, 0xC7, 0xEB),
        ["Monk"] = new Color(0x00, 0xFF, 0x96),
        ["Paladin"] = new Color(0xF4, 0x8C, 0xBA),
        ["Priest"] = new Color(0xFF, 0xFF, 0xFF),
        ["Rogue"] = new Color(0xFF, 0xF5, 0x69),
        ["Shaman"] = new Color(0x00, 0x70, 0xDE),
        ["Warlock"] = new Color(0x87, 0x87, 0xED),
        ["Warrior"] = new Color(0xC7, 0x9C, 0x6E)
    };

    private readonly ILogger<RaiderIoModule> _logger;
    private readonly IRaiderIoClient _client;

    public RaiderIoModule(ILogger<RaiderIoModule> logger, IRaiderIoClient client)
    {
        _logger = logger;
        _client = client;
    }

    [SlashCommand("character", "Fetch Raider.IO stats for a character (defaults to US region).")]
    public async Task GetCharacterAsync(
        [Summary("server", "Realm/server name (e.g., Area 52).")] string server,
        [Summary("character", "Character name.")] string characterName)
    {
        var trimmedServer = server?.Trim();
        var trimmedCharacter = characterName?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedServer) || string.IsNullOrWhiteSpace(trimmedCharacter))
        {
            await RespondAsync("Please supply both a server and character name.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Raider.IO character lookup requested by {UserId} ({Username}) -> {Character} on {Server}.",
            Context.User.Id,
            Context.User.Username,
            trimmedCharacter,
            trimmedServer);

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var profile = await _client
                .GetCharacterAsync(trimmedServer, trimmedCharacter, CancellationToken.None)
                .ConfigureAwait(false);

            if (profile is null)
            {
                _logger.LogInformation(
                    "Raider.IO character {Character} on {Server} not found.",
                    trimmedCharacter,
                    trimmedServer);
                await RespondAsync($"Could not find `{trimmedCharacter}` on `{trimmedServer}` in Raider.IO.", ephemeral: true)
                    .ConfigureAwait(false);
                return;
            }
            else
            {
                _logger.LogInformation(
                    "Fetched Raider.IO character {Character} on {Server}.",
                    profile.Name,
                    profile.Realm);
                var embed = BuildCharacterEmbed(profile);
                await RespondAsync(embed: embed, ephemeral: true).ConfigureAwait(false);
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Raider.IO character lookup failed for {Character} on {Server}.",
                trimmedCharacter,
                trimmedServer);
            await RespondAsync("Something went wrong while calling Raider.IO. The error was logged.", ephemeral: true)
                .ConfigureAwait(false);
            return;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    [SlashCommand("guild", "Fetch Raider.IO info for a guild (defaults to US region).")]
    public async Task GetGuildAsync(
        [Summary("server", "Realm/server name (e.g., Area 52).")] string server,
        [Summary("guild", "Guild name.")] string guildName)
    {
        var trimmedServer = server?.Trim();
        var trimmedGuild = guildName?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedServer) || string.IsNullOrWhiteSpace(trimmedGuild))
        {
            await RespondAsync("Please supply both a server and guild name.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Raider.IO guild lookup requested by {UserId} ({Username}) -> {Guild} on {Server}.",
            Context.User.Id,
            Context.User.Username,
            trimmedGuild,
            trimmedServer);

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var profile = await _client
                .GetGuildAsync(trimmedServer, trimmedGuild, fields: "raid_progression,members", CancellationToken.None)
                .ConfigureAwait(false);

            if (profile is null)
            {
                await RespondAsync($"Could not find guild `{trimmedGuild}` on `{trimmedServer}` in Raider.IO.", ephemeral: true)
                    .ConfigureAwait(false);
                return;
            }

            var embed = BuildGuildEmbed(profile);
            await RespondAsync(embed: embed, ephemeral: true).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Raider.IO guild lookup failed for {Guild} on {Server}.",
                trimmedGuild,
                trimmedServer);
            await RespondAsync("Something went wrong while calling Raider.IO. The error was logged.", ephemeral: true)
                .ConfigureAwait(false);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static Embed BuildCharacterEmbed(RaiderIoCharacterProfile profile)
    {
        var builder = new StringBuilder();
        var spec = string.IsNullOrWhiteSpace(profile.ActiveSpecName) ? string.Empty : $" ({profile.ActiveSpecName})";
        builder.AppendLine($"{profile.Class}{spec}");

        var embedBuilder = new EmbedBuilder()
            .WithTitle(profile.Name)
            .WithDescription(builder.ToString())
            .WithColor(GetClassColor(profile.Class))
            .AddField("Realm", $"{profile.Realm} ({profile.Region.ToUpperInvariant()})", inline: true);

        if (BuildProfileUrl(profile) is string characterUrl)
        {
            embedBuilder.WithUrl(characterUrl);
        }

        if (profile.MythicPlusRanks?.Overall is MythicPlusRank rank)
        {
            embedBuilder.AddField(
                "Mythic+ Ranks",
                $"World: {FormatRank(rank.World)}\nRegion: {FormatRank(rank.Region)}\nRealm: {FormatRank(rank.Realm)}",
                inline: true);
        }

        if (profile.MythicPlusScoresBySeason.FirstOrDefault() is MythicPlusScoresBySeason scoresBySeason)
        {
            var scores = scoresBySeason.Scores;
            if (scores.Count > 0)
            {
                var currentScore = FormatScore(scores.GetValueOrDefault("all", 0));
                embedBuilder.AddField(
                    "Mythic+ Scores",
                    $"Current: {currentScore}",
                    inline: true);
            }
        }

        if (profile.Gear is not null)
        {
            embedBuilder.AddField(
                "Item Level",
                $"Equipped: {profile.Gear.ItemLevelEquipped:0.#}\nTotal: {profile.Gear.ItemLevelTotal:0.#}",
                inline: true);
        }

        if (profile.LastCrawledAt is DateTimeOffset crawledAt)
        {
            embedBuilder.WithFooter($"Last synced {crawledAt:yyyy-MM-dd HH:mm} UTC");
        }

        return embedBuilder.Build();
    }

    private Embed BuildGuildEmbed(RaiderIoGuildProfile profile)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle(profile.Name)
            .WithDescription($"Realm: {profile.Realm} ({profile.Region.ToUpperInvariant()}) | Faction: {ToTitleCase(profile.Faction)}")
            .WithColor(profile.Faction.Equals("alliance", StringComparison.OrdinalIgnoreCase) ? Color.Blue : Color.DarkRed);

        var guildUrl = profile.ProfileUrl?.AbsoluteUri.ToString() ?? BuildGuildProfileUrl(profile);
        _logger.LogInformation("Guild URL: {GuildUrl}", guildUrl);
        if (guildUrl is not null)
        {
            embedBuilder.WithUrl(guildUrl);
        }

        if (profile.RaidProgression.Count > 0)
        {
            foreach (var progression in profile.RaidProgression.Take(3))
            {
                embedBuilder.AddField(progression.Key, progression.Value.Summary, inline: true);
            }
        }

        return embedBuilder.Build();
    }

    private static string FormatRank(int? value) =>
        value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "-";

    private static string FormatScore(float value) =>
        value.ToString("N1", CultureInfo.InvariantCulture);

    private static string? BuildProfileUrl(RaiderIoCharacterProfile profile)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase
        var region = profile.Region?.Trim().ToLowerInvariant() ?? "us";
#pragma warning restore CA1308 // Normalize strings to uppercase
        var realmSlug = Slugify(profile.Realm);
        var characterSlug = Slugify(profile.Name);
        var url = $"https://raider.io/characters/{region}/{realmSlug}/{characterSlug}";
        return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : null;
    }

    private static string? BuildGuildProfileUrl(RaiderIoGuildProfile profile)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase
        var region = profile.Region?.Trim().ToLowerInvariant() ?? "us";
#pragma warning restore CA1308 // Normalize strings to uppercase
        var realmSlug = Slugify(profile.Realm);
        var guildSlug = Slugify(profile.Name);
        var url = $"https://raider.io/guilds/{region}/{realmSlug}/{guildSlug}";
        return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : null;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

#pragma warning disable CA1308 // Normalize strings to uppercase
        var normalized = value.Trim().ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308 // Normalize strings to uppercase
        normalized = Regex.Replace(normalized, "[^a-z0-9-]", string.Empty);
        return normalized;
    }

    private static Color GetClassColor(string className)
    {
        if (ClassColors.TryGetValue(className, out var color))
        {
            return color;
        }

        return Color.DarkBlue;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var textInfo = CultureInfo.InvariantCulture.TextInfo;
#pragma warning disable CA1308 // Normalize strings to uppercase
        var normalized = value.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
        return textInfo.ToTitleCase(normalized);
    }
}
