using System.Globalization;
using System.Text;
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

        var message = "Character lookup logged.";

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var profile = await _client
                .GetCharacterAsync(trimmedServer, trimmedCharacter, CancellationToken.None)
                .ConfigureAwait(false);

            if (profile is null)
            {
                message = $"Could not find `{trimmedCharacter}` on `{trimmedServer}` in Raider.IO.";
                _logger.LogInformation(
                    "Raider.IO character {Character} on {Server} not found.",
                    trimmedCharacter,
                    trimmedServer);
            }
            else
            {
                message = BuildCharacterSummary(profile);
                _logger.LogInformation(
                    "Fetched Raider.IO character {Character} on {Server}.",
                    profile.Name,
                    profile.Realm);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Raider.IO character lookup failed for {Character} on {Server}.",
                trimmedCharacter,
                trimmedServer);
            message = "Something went wrong while calling Raider.IO. The error was logged.";
        }
#pragma warning restore CA1031 // Do not catch general exception types

        await RespondAsync(message, ephemeral: true).ConfigureAwait(false);
    }

    private static string BuildCharacterSummary(RaiderIoCharacterProfile profile)
    {
        var builder = new StringBuilder();
        var spec = string.IsNullOrWhiteSpace(profile.ActiveSpecName) ? string.Empty : $" ({profile.ActiveSpecName})";
        builder.AppendLine($"**{profile.Name}** – {profile.Class}{spec}");
        builder.AppendLine($"Realm: {profile.Realm} | Region: {profile.Region.ToUpperInvariant()}");

        if (profile.Gear is not null)
        {
            builder.AppendLine($"Equipped iLvl: {profile.Gear.ItemLevelEquipped:0.#} (Total: {profile.Gear.ItemLevelTotal:0.#})");
        }

        if (profile.MythicPlusRanks?.Overall is MythicPlusRank rank)
        {
            builder.AppendLine($"Mythic+ Ranks – World: {FormatRank(rank.World)}, Region: {FormatRank(rank.Region)}, Realm: {FormatRank(rank.Realm)}");
        }

        if (profile.LastCrawledAt is DateTimeOffset crawledAt)
        {
            builder.AppendLine($"Last Synced: {crawledAt:yyyy-MM-dd HH:mm} UTC");
        }

        return builder.ToString();
    }

    private static string FormatRank(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    }
}
