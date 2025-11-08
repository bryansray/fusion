using System;
using System.Text;
using Discord.Interactions;
using Fusion.Infrastructure.Warcraft;
using Fusion.Infrastructure.Warcraft.Models;
using Microsoft.Extensions.Logging;

namespace Fusion.Bot.Modules;

[Group("warcraft", "World of Warcraft utilities.")]
public sealed class WarcraftModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<WarcraftModule> _logger;
    private readonly IWarcraftClient _warcraftClient;

    public WarcraftModule(ILogger<WarcraftModule> logger, IWarcraftClient warcraftClient)
    {
        _logger = logger;
        _warcraftClient = warcraftClient;
    }

    [SlashCommand("character", "Fetch a character profile by realm and name.")]
    public async Task GetCharacterAsync(
        [Summary("realm", "Realm name or slug (e.g., Area 52).")] string realm,
        [Summary("character", "Character name.")] string characterName)
    {
        var trimmedRealm = realm?.Trim();
        var trimmedCharacter = characterName?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedRealm) || string.IsNullOrWhiteSpace(trimmedCharacter))
        {
            _logger.LogWarning(
                "Warcraft character lookup aborted: invalid input. User {UserId}",
                Context.User.Id);
            await RespondAsync("Please provide both a realm and character name.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Warcraft character lookup requested by {UserId} ({Username}) -> {Character} on {Realm}.",
            Context.User.Id,
            Context.User.Username,
            trimmedCharacter,
            trimmedRealm);

        var responseMessage = "Character lookup logged.";

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var profile = await _warcraftClient
                .GetCharacterAsync(BlizzardRegions.Us, trimmedRealm, trimmedCharacter, CancellationToken.None);

            if (profile is null)
            {
                _logger.LogInformation(
                    "Warcraft character {Character} on {Realm} (US) not found.",
                    trimmedCharacter,
                    trimmedRealm);
                responseMessage = $"Could not find `{trimmedCharacter}` on `{trimmedRealm}`. Logged for follow-up.";
            }
            else
            {
                _logger.LogInformation(
                    "Fetched Warcraft character {Character} ({Level}) on {Realm} (US).",
                    profile.Name,
                    profile.Level,
                    profile.Realm?.Name ?? trimmedRealm);
                responseMessage = BuildCharacterSummary(profile);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Warcraft character lookup failed for {Character} on {Realm}.",
                trimmedCharacter,
                trimmedRealm);
            responseMessage = "Something went wrong looking up that character. The error was logged.";
        }
#pragma warning restore CA1031 // Do not catch general exception types

        await RespondAsync(responseMessage, ephemeral: true).ConfigureAwait(false);
    }

    private static string BuildCharacterSummary(CharacterProfile profile)
    {
        var builder = new StringBuilder();
        var className = profile.CharacterClass?.Name ?? "Adventurer";
        var realmName = profile.Realm?.Name ?? "Unknown realm";
        var realmSlug = profile.Realm?.Slug ?? "n/a";

        builder.AppendLine($"**{profile.Name}** – Level {profile.Level} {className}");
        builder.AppendLine($"Realm: {realmName} ({realmSlug})");

        if (profile.ItemLevel is int itemLevel && itemLevel > 0)
        {
            builder.AppendLine($"Item Level: {itemLevel}");
        }

        if (profile.LastLoginTimestamp is long timestamp && timestamp > 0)
        {
            var lastLogin = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
            builder.AppendLine($"Last Login: {lastLogin:yyyy-MM-dd HH:mm} UTC");
        }

        builder.Append("Region: US (default)");

        return builder.ToString();
    }
}
