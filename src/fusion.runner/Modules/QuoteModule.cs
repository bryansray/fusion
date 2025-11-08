using System;
using System.Linq;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
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
        var document = new QuoteDocument
        {
            Person = request.Author,
            PersonKey = NormalizeAuthorKey(request.Author),
            PersonUserId = ResolveAuthorId(request.Author),
            Message = request.Message,
            Tags = tagList.ToArray(),
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
            request.Author,
            document.PersonUserId,
            request.Message,
            tagList,
            request.Nsfw,
            document.ShortId);

        await _repository.InsertAsync(document);

        var tagsSummary = tagList.Count > 0 ? string.Join(", ", tagList) : "No tags";

        await RespondAsync(
            $"Quote {document.ShortId} from {request.Author} received! Tags: {tagsSummary} NSFW: {request.Nsfw}",
            ephemeral: true);
    }

    private static string NormalizeAuthorKey(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return "unknown";
        }

        var normalized = author.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9]+", "-");
        normalized = normalized.Trim('-');

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private ulong? ResolveAuthorId(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return null;
        }

        if (MentionUtils.TryParseUser(author, out var mentionUserId))
        {
            return mentionUserId;
        }

        var guild = Context.Guild;
        if (guild is null)
        {
            return null;
        }

        var trimmed = author.Trim();
        var user = guild.Users.FirstOrDefault(u =>
            string.Equals(u.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Username, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"{u.Username}#{u.Discriminator}", trimmed, StringComparison.OrdinalIgnoreCase));

        return user?.Id;
    }
}
