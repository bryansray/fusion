using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace Fusion.Runner.Modules;

[Group("quote", "Quote management commands.")]
public sealed class QuoteModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<QuoteModule> _logger;

    public QuoteModule(ILogger<QuoteModule> logger)
    {
        _logger = logger;
    }

    [SlashCommand("add", "Add a new quote.")]
    public async Task AddQuoteAsync([ComplexParameter] QuoteAddRequest request)
    {
        var tagList = request.GetTags();

        _logger.LogInformation(
            "Quote add requested by {User} ({UserId}): {Author} - {Message} | Tags: {Tags} | NSFW: {Nsfw}",
            Context.User.Username,
            Context.User.Id,
            request.Author,
            request.Message,
            tagList,
            request.Nsfw);

        var tagsSummary = tagList.Count > 0 ? string.Join(", ", tagList) : "No tags";

        await RespondAsync(
            $"Quote from {request.Author} received! Tags: {tagsSummary} NSFW: {request.Nsfw}",
            ephemeral: true);
    }
}
