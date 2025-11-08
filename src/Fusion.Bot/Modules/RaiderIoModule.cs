using System.Globalization;
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

    private static Embed BuildCharacterEmbed(RaiderIoCharacterProfile profile)
    {
        var builder = new StringBuilder();
        var spec = string.IsNullOrWhiteSpace(profile.ActiveSpecName) ? string.Empty : $" ({profile.ActiveSpecName})";
        builder.AppendLine($"{profile.Class}{spec}");

        var embed = new EmbedBuilder()
            .WithTitle(profile.Name)
            .WithDescription(builder.ToString())
            .WithColor(Color.DarkBlue)
            .WithUrl(BuildProfileUrl(profile))
            .AddField("Realm", $"{profile.Realm} ({profile.Region.ToUpperInvariant()})", inline: true);

        if (profile.MythicPlusRanks?.Overall is MythicPlusRank rank)
        {
            embed.AddField(
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
                embed.AddField(
                    "Mythic+ Scores",
                    $"Current: {currentScore}",
                    inline: true);
            }
        }

        if (profile.Gear is not null)
        {
            embed.AddField(
                "Item Level",
                $"Equipped: {profile.Gear.ItemLevelEquipped:0.#}\nTotal: {profile.Gear.ItemLevelTotal:0.#}",
                inline: true);
        }

        if (profile.LastCrawledAt is DateTimeOffset crawledAt)
        {
            embed.WithFooter($"Last synced {crawledAt:yyyy-MM-dd HH:mm} UTC");
        }

        return embed.Build();
    }

    private static string FormatRank(int? value) =>
        value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "-";

    private static string FormatScore(float value) =>
        value.ToString("N1", CultureInfo.InvariantCulture);

    private static string BuildProfileUrl(RaiderIoCharacterProfile profile)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase
        var region = profile.Region?.Trim().ToLowerInvariant() ?? "us";
#pragma warning restore CA1308 // Normalize strings to uppercase
        var realmSlug = Slugify(profile.Realm);
        var characterSlug = Slugify(profile.Name);
        return $"https://raider.io/characters/{region}/{realmSlug}/{characterSlug}";
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
}
