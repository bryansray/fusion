using Fusion.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fusion.Persistence.Models;

public sealed class QuoteDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string ShortId { get; private init; } = ShortIdentifier.New();

    public required string Person { get; init; }

    public ulong? PersonUserId { get; init; }

    public required string Message { get; init; }

    public string PersonKey { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public IReadOnlyList<MentionedUser> MentionedUsers { get; init; } = Array.Empty<MentionedUser>();

    public bool Nsfw { get; init; }

    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }

    public ulong AddedBy { get; init; }

    public DateTimeOffset AddedAt { get; init; }

    public int Likes { get; init; }

    public int Uses { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public ulong? DeletedBy { get; init; }
}
