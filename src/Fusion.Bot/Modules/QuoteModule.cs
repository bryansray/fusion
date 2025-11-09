using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fusion.Persistence;
using Fusion.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace Fusion.Bot.Modules;

[Group("quote", "Quote management commands.")]
public sealed class QuoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<QuoteModule> _logger;
    private readonly IQuoteRepository _repository;

    public QuoteModule(ILogger<QuoteModule> logger, IQuoteRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [SlashCommand("add", "Add a new quote.")]
    public async Task AddQuoteAsync([ComplexParameter] QuoteAddRequest request)
    {
        var tagList = request.GetTags();
        var (personName, personUserId) = ResolvePerson(request.Person);
        var mentionedUsers = ResolveMentionedUsers(request.Message);
        var document = new QuoteDocument
        {
            Person = personName,
            PersonKey = NormalizePersonKey(personName),
            PersonUserId = personUserId,
            Message = request.Message,
            Tags = tagList.ToArray(),
            MentionedUsers = mentionedUsers,
            Nsfw = request.Nsfw,
            GuildId = Context.Guild?.Id ?? 0,
            ChannelId = Context.Channel.Id,
            AddedBy = Context.User.Id,
            AddedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Quote add requested by {User} ({UserId}) in Guild {GuildId} Channel {ChannelId}: {Author} (Id: {AuthorId}) - {Message} | Tags: {Tags} | NSFW: {Nsfw} | ShortId: {ShortId}",
            Context.User.Username,
            Context.User.Id,
            document.GuildId,
            document.ChannelId,
            document.Person,
            document.PersonUserId,
            document.Message,
            tagList,
            request.Nsfw,
            document.ShortId);

        await _repository.InsertAsync(document).ConfigureAwait(false);

        var tagsSummary = tagList.Count > 0 ? string.Join(", ", tagList) : "No tags";

        await RespondAsync(
            $"Quote {document.ShortId} from {document.Person} received! Tags: {tagsSummary} NSFW: {request.Nsfw}",
            ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("find", "Find a quote by its short id.")]
    public async Task FindQuoteAsync([Summary("id", "The short id generated when the quote was added.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to look up.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var quote = await _repository.GetByShortIdAsync(normalized).ConfigureAwait(false);

        if (quote is not null)
        {
            await _repository.IncrementUsesAsync(quote.ShortId).ConfigureAwait(false);
            await RespondWithQuoteAsync(quote, null, ephemeral: false).ConfigureAwait(false);
            return;
        }

        var fuzzyMatches = await _repository.GetFuzzyShortIdAsync(normalized).ConfigureAwait(false);
        if (fuzzyMatches.Count == 0)
        {
            await RespondAsync($"No quotes found matching id prefix `{normalized}`.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var fallback = fuzzyMatches[0];
        await _repository.IncrementUsesAsync(fallback.ShortId).ConfigureAwait(false);
        await RespondWithQuoteAsync(
            fallback,
            $"Quote `{normalized}` not found. Showing closest match:",
            ephemeral: false).ConfigureAwait(false);
    }

    [SlashCommand("search", "Search quote text and tags.")]
    public async Task SearchQuotesAsync(
        [Summary("query", "Text to search for inside stored quotes.")] string query,
        [Summary("limit", "Maximum number of results (1-10)."), MinValue(1), MaxValue(10)] int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await RespondAsync("Please provide text to search for.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var clampedLimit = Math.Clamp(limit, 1, 10);
        var results = await _repository.SearchAsync(query, clampedLimit).ConfigureAwait(false);

        if (results.Count == 0)
        {
            await RespondAsync("No quotes matched that search.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Showing {results.Count} result(s) for `" + query.Trim() + "`:");
        builder.AppendLine();

        foreach (var result in results)
        {
            builder.AppendLine($"`{result.ShortId}` **{result.Person}**: {Truncate(result.Message, 120)}");
        }

        foreach (var result in results)
        {
            await _repository.IncrementUsesAsync(result.ShortId).ConfigureAwait(false);
        }

        await RespondAsync(builder.ToString(), ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("delete", "Soft delete a quote by its short id.")]
    public async Task DeleteQuoteAsync([Summary("id", "The quote short id to delete.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to delete.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (!HasQuoteModerationPermission())
        {
            await RespondAsync("You do not have permission to delete quotes.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var deleted = await _repository.SoftDeleteAsync(normalized, Context.User.Id).ConfigureAwait(false);

        var message = deleted
            ? $"Quote `{normalized}` has been soft deleted."
            : $"Quote `{normalized}` does not exist or was already deleted.";

        await RespondAsync(message, ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("restore", "Restore a previously deleted quote.")]
    public async Task RestoreQuoteAsync([Summary("id", "The quote short id to restore.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to restore.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (!HasQuoteModerationPermission())
        {
            await RespondAsync("You do not have permission to restore quotes.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var restored = await _repository.RestoreAsync(normalized, Context.User.Id).ConfigureAwait(false);

        var message = restored
            ? $"Quote `{normalized}` has been restored."
            : $"Quote `{normalized}` does not exist or is not deleted.";

        await RespondAsync(message, ephemeral: true).ConfigureAwait(false);
    }

    private IReadOnlyList<MentionedUser> ResolveMentionedUsers(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Array.Empty<MentionedUser>();
        }

        var matches = Regex.Matches(message, "<@!?([0-9]+)>");
        if (matches.Count == 0)
        {
            return Array.Empty<MentionedUser>();
        }

        var guild = Context.Guild;
        var result = new List<MentionedUser>(matches.Count);

        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            if (!ulong.TryParse(match.Groups[1].Value, out var userId))
            {
                continue;
            }

            var user = guild?.GetUser(userId) ?? Context.Client.GetUser(userId);
            if (user is null)
            {
                continue;
            }

            result.Add(new MentionedUser(user.Id, GetUserDisplayName(user)));
        }

        return result
            .GroupBy(u => u.UserId)
            .Select(g => g.First())
            .ToArray();
    }

    private async Task RespondWithQuoteAsync(QuoteDocument quote, string? message, bool ephemeral)
    {
        var embed = BuildQuoteEmbed(quote);
        var components = BuildQuoteComponents(quote).Build();

        if (string.IsNullOrWhiteSpace(message))
        {
            await RespondAsync(embed: embed, components: components, ephemeral: ephemeral).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(message, embed: embed, components: components, ephemeral: ephemeral).ConfigureAwait(false);
        }
    }

    private static Embed BuildQuoteEmbed(QuoteDocument quote)
    {
        var tags = quote.Tags.Count > 0 ? string.Join(", ", quote.Tags) : "None";
        var mentions = quote.MentionedUsers.Count > 0
            ? string.Join(", ", quote.MentionedUsers.Select(m => $"<@{m.UserId}> ({m.DisplayName})"))
            : "None";

        var embed = new EmbedBuilder()
            .WithTitle(quote.Person)
            .WithDescription($"> {quote.Message}")
            .WithColor(quote.Nsfw ? Color.DarkRed : Color.DarkGrey)
            .AddField("Short Id", $"`{quote.ShortId}`", inline: true)
            .AddField("NSFW", quote.Nsfw ? "Yes" : "No", inline: true)
            .AddField("Tags", tags, inline: false)
            .AddField("Mentions", mentions, inline: false)
            .AddField("Stats", $"Uses: {quote.Uses}\nLikes: {quote.Likes}", inline: true)
            .WithFooter($"Added by <@{quote.AddedBy}> on {quote.AddedAt:yyyy-MM-dd HH:mm} UTC");

        if (quote.PersonUserId is not null)
        {
            embed.WithAuthor(quote.Person, iconUrl: null, url: null);
        }

        if (quote.DeletedAt is not null)
        {
            embed.AddField("Deleted", $"{quote.DeletedAt:yyyy-MM-dd HH:mm} UTC by <@{quote.DeletedBy}>", inline: false);
        }

        return embed.Build();
    }

    private static ComponentBuilder BuildQuoteComponents(QuoteDocument quote)
    {
        return new ComponentBuilder()
            .WithButton("Share Quote", $"quote-share:{quote.ShortId}", ButtonStyle.Primary)
            .WithButton("Copy ID", $"quote-copy:{quote.ShortId}", ButtonStyle.Secondary);
    }

    internal static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "…";
    }

    private bool HasQuoteModerationPermission()
    {
        if (Context.User is SocketGuildUser guildUser)
        {
            var permissions = guildUser.GuildPermissions;
            return permissions.ManageMessages || permissions.ManageGuild || permissions.Administrator;
        }

        return false;
    }

    internal static string NormalizePersonKey(string person)
    {
        if (string.IsNullOrWhiteSpace(person))
        {
            return "unknown";
        }

#pragma warning disable CA1308 // Normalize strings to uppercase
        var normalized = person.Trim().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
        normalized = Regex.Replace(normalized, "[^a-z0-9]+", "-");
        normalized = normalized.Trim('-');

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private (string Name, ulong? Id) ResolvePerson(string person)
    {
        if (string.IsNullOrWhiteSpace(person))
        {
            return ("Unknown", null);
        }

        var trimmedInput = person.Trim();

        if (MentionUtils.TryParseUser(person, out var mentionUserId))
        {
            var user = GetGuildUser(mentionUserId) ?? Context.Client.GetUser(mentionUserId);
            if (user is not null)
            {
                return (GetUserDisplayName(user), user.Id);
            }

            return (trimmedInput, mentionUserId);
        }

        var guild = Context.Guild;
        if (guild is null)
        {
            return (trimmedInput, null);
        }

        var guildUser = guild.Users.FirstOrDefault(u =>
            string.Equals(u.DisplayName, trimmedInput, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Username, trimmedInput, StringComparison.OrdinalIgnoreCase) ||
            (u.GlobalName is not null &&
             string.Equals(u.GlobalName, trimmedInput, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals($"{u.Username}#{u.Discriminator}", trimmedInput, StringComparison.OrdinalIgnoreCase));

        if (guildUser is not null)
        {
            return (GetUserDisplayName(guildUser), guildUser.Id);
        }

        return (trimmedInput, null);
    }

    private SocketGuildUser? GetGuildUser(ulong userId) => Context.Guild?.GetUser(userId);

    private static string GetUserDisplayName(IUser user) =>
        user switch
        {
            SocketGuildUser guildUser => guildUser.DisplayName,
            _ => user.GlobalName ?? user.Username
        };

    [ComponentInteraction("quote-share:*")]
    public async Task HandleQuoteShareAsync(string shortId)
    {
        var normalized = shortId.Trim().ToUpperInvariant();
        var quote = await _repository.GetByShortIdAsync(normalized).ConfigureAwait(false);
        if (quote is null)
        {
            await RespondAsync($"Quote `{normalized}` could not be found.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await _repository.IncrementUsesAsync(quote.ShortId).ConfigureAwait(false);
        var embed = BuildQuoteEmbed(quote);
        var components = BuildQuoteComponents(quote).Build();
        await RespondAsync(embed: embed, components: components, ephemeral: false).ConfigureAwait(false);
    }

    [ComponentInteraction("quote-copy:*")]
    public async Task HandleQuoteCopyAsync(string shortId)
    {
        var normalized = shortId.Trim().ToUpperInvariant();
        await RespondAsync($"Short Id: `{normalized}`", ephemeral: true).ConfigureAwait(false);
    }
}
