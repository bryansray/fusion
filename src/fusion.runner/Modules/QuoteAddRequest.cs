namespace Fusion.Runner.Modules;

public sealed record class QuoteAddRequest
{
    public string Author { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Tags { get; init; }

    public bool Nsfw { get; init; }

    public IReadOnlyList<string> GetTags() =>
        string.IsNullOrWhiteSpace(Tags)
            ? Array.Empty<string>()
            : Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
