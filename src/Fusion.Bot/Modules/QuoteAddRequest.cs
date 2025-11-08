using Discord.Interactions;

namespace Fusion.Bot.Modules;

[method: ComplexParameterCtor]
public sealed class QuoteAddRequest([Summary("person", "The person who said the quote.")] string person,
                       [Summary("quote", "The quote message.")] string message,
                       [Summary("tags", "Comma-separated tags for the quote.")] string? tags = null,
                       [Summary("nsfw", "Whether the quote is NSFW.")] bool nsfw = false)
{
    public string Person { get; init; } = person;

    public string Message { get; init; } = message;

    public string? Tags { get; init; } = tags;

    public bool Nsfw { get; init; } = nsfw;

    public IReadOnlyList<string> GetTags() =>
          string.IsNullOrWhiteSpace(Tags)
              ? Array.Empty<string>()
              : Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
