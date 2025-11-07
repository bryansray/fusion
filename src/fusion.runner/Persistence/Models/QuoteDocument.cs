using System;
using System.Collections.Generic;
using Fusion.Runner;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Fusion.Runner.Persistence.Models;

public sealed class QuoteDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string ShortId { get; private init; } = ShortIdentifier.New();

    public required string Author { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool Nsfw { get; init; }

    public ulong GuildId { get; init; }

    public ulong ChannelId { get; init; }

    public ulong AddedBy { get; init; }

    [BsonElement("createdAt")]
    public DateTimeOffset AddedAt { get; init; }
}
