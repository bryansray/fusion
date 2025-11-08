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
