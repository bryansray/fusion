namespace Fusion.Runner;

public sealed class DiscordOptions
{
    public string? Token { get; set; }

    public int GuildId { get; init; }

    public int ClientId { get; init; }

    public string? Status { get; set; }
}
