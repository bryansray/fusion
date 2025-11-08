using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fusion.Runner.Persistence;
using Fusion.Runner.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace Fusion.Runner.Modules;

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

        await _repository.InsertAsync(document);

        var tagsSummary = tagList.Count > 0 ? string.Join(", ", tagList) : "No tags";

        await RespondAsync(
            $"Quote {document.ShortId} from {document.Person} received! Tags: {tagsSummary} NSFW: {request.Nsfw}",
            ephemeral: true);
    }

    [SlashCommand("find", "Find a quote by its short id.")]
    public async Task FindQuoteAsync([Summary("id", "The short id generated when the quote was added.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to look up.", ephemeral: true);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var quote = await _repository.GetByShortIdAsync(normalized);
        string response;

        if (quote is not null)
        {
            await _repository.IncrementUsesAsync(quote.ShortId);
            response = FormatQuoteResponse(quote);
        }
        else
        {
            var fuzzyMatches = await _repository.GetFuzzyShortIdAsync(normalized);
            if (fuzzyMatches.Count == 0)
            {
                response = $"No quotes found matching id prefix `{normalized}`.";
            }
            else
            {
                var fallback = fuzzyMatches.First();
                await _repository.IncrementUsesAsync(fallback.ShortId);
                response = $"Quote `{normalized}` not found. Showing closest match:`\n`" + FormatQuoteResponse(fallback);
            }
        }

        await RespondAsync(response, ephemeral: false);
    }

    [SlashCommand("search", "Search quote text and tags.")]
    public async Task SearchQuotesAsync(
        [Summary("query", "Text to search for inside stored quotes.")] string query,
        [Summary("limit", "Maximum number of results (1-10)."), MinValue(1), MaxValue(10)] int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await RespondAsync("Please provide text to search for.", ephemeral: true);
            return;
        }

        var clampedLimit = Math.Clamp(limit, 1, 10);
        var results = await _repository.SearchAsync(query, clampedLimit);

        if (results.Count == 0)
        {
            await RespondAsync("No quotes matched that search.", ephemeral: true);
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
            await _repository.IncrementUsesAsync(result.ShortId);
        }

        await RespondAsync(builder.ToString(), ephemeral: true);
    }

    [SlashCommand("delete", "Soft delete a quote by its short id.")]
    public async Task DeleteQuoteAsync([Summary("id", "The quote short id to delete.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to delete.", ephemeral: true);
            return;
        }

        if (!HasQuoteModerationPermission())
        {
            await RespondAsync("You do not have permission to delete quotes.", ephemeral: true);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var deleted = await _repository.SoftDeleteAsync(normalized, Context.User.Id);

        var message = deleted
            ? $"Quote `{normalized}` has been soft deleted."
            : $"Quote `{normalized}` does not exist or was already deleted.";

        await RespondAsync(message, ephemeral: true);
    }

    [SlashCommand("restore", "Restore a previously deleted quote.")]
    public async Task RestoreQuoteAsync([Summary("id", "The quote short id to restore.")] string shortId)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            await RespondAsync("Please provide the quote id you want to restore.", ephemeral: true);
            return;
        }

        if (!HasQuoteModerationPermission())
        {
            await RespondAsync("You do not have permission to restore quotes.", ephemeral: true);
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var restored = await _repository.RestoreAsync(normalized, Context.User.Id);

        var message = restored
            ? $"Quote `{normalized}` has been restored."
            : $"Quote `{normalized}` does not exist or is not deleted.";

        await RespondAsync(message, ephemeral: true);
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

    private static string FormatQuoteResponse(QuoteDocument quote)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**{quote.Person}** said:");
        builder.AppendLine($"> {quote.Message}");
        builder.AppendLine();

        builder.AppendLine($"Short Id: `{quote.ShortId}` | NSFW: {(quote.Nsfw ? "Yes" : "No")}");
        builder.AppendLine($"Added by: <@{quote.AddedBy}> on {quote.AddedAt:yyyy-MM-dd HH:mm} UTC");

        var tags = quote.Tags.Count > 0 ? string.Join(", ", quote.Tags) : "None";
        builder.AppendLine($"Tags: {tags}");

        var mentions = quote.MentionedUsers.Count > 0
            ? string.Join(", ", quote.MentionedUsers.Select(m => $"<@{m.UserId}> ({m.DisplayName})"))
            : "None";
        builder.AppendLine($"Mentions: {mentions}");

        builder.AppendLine($"Uses: {quote.Uses} | Likes: {quote.Likes}");

        if (quote.DeletedAt is not null)
        {
            builder.AppendLine($"Deleted: {quote.DeletedAt:yyyy-MM-dd HH:mm} UTC by <@{quote.DeletedBy}>");
        }

        return builder.ToString();
    }

    private static string Truncate(string value, int maxLength)
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

    private static string NormalizePersonKey(string person)
    {
        if (string.IsNullOrWhiteSpace(person))
        {
            return "unknown";
        }

        var normalized = person.Trim().ToLowerInvariant();
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
}
