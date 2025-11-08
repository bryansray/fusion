using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Fusion.Runner.Persistence.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Fusion.Runner.Persistence;

public sealed class MongoQuoteRepository : IQuoteRepository
{
    private readonly IMongoCollection<QuoteDocument> _collection;
    private readonly ILogger<MongoQuoteRepository> _logger;

    public MongoQuoteRepository(
        IMongoClient client,
        IOptions<MongoOptions> options,
        ILogger<MongoQuoteRepository> logger)
    {
        _logger = logger;

        var mongoOptions = options.Value;

        if (string.IsNullOrWhiteSpace(mongoOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                "Mongo connection string is missing. Set 'Mongo:ConnectionString' in configuration.");
        }

        if (string.IsNullOrWhiteSpace(mongoOptions.DatabaseName))
        {
            throw new InvalidOperationException("Mongo database name is not configured.");
        }

        var database = client.GetDatabase(mongoOptions.DatabaseName);
        var collectionName = string.IsNullOrWhiteSpace(mongoOptions.QuotesCollectionName)
            ? "quotes"
            : mongoOptions.QuotesCollectionName;

        _collection = database.GetCollection<QuoteDocument>(collectionName);
    }

    public async Task InsertAsync(QuoteDocument quote, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(quote, cancellationToken: cancellationToken);
        _logger.LogInformation("Quote from {Author} persisted to MongoDB.", quote.Person);
    }

    public async Task<IReadOnlyList<QuoteDocument>> FindByAuthorAsync(
        string author,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Eq(q => q.PersonKey, author),
            Builders<QuoteDocument>.Filter.Eq(q => q.DeletedAt, null));
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<QuoteDocument?> GetByShortIdAsync(string shortId, CancellationToken cancellationToken = default)
    {
        var normalized = shortId.Trim().ToUpperInvariant();
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Eq(q => q.ShortId, normalized),
            Builders<QuoteDocument>.Filter.Eq(q => q.DeletedAt, null));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuoteDocument>> GetFuzzyShortIdAsync(
        string shortIdPrefix,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortIdPrefix))
        {
            return Array.Empty<QuoteDocument>();
        }

        var normalized = shortIdPrefix.Trim().ToUpperInvariant();
        var pattern = $"^{Regex.Escape(normalized)}";
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Regex(q => q.ShortId, new BsonRegularExpression(pattern)),
            Builders<QuoteDocument>.Filter.Eq(q => q.DeletedAt, null));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuoteDocument>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<QuoteDocument>();
        }

        var normalized = Regex.Escape(query.Trim());
        var regex = new BsonRegularExpression(normalized, "i");

        var messageFilter = Builders<QuoteDocument>.Filter.Regex(q => q.Message, regex);
        var tagsFilter = Builders<QuoteDocument>.Filter.Regex("Tags", regex);
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Or(messageFilter, tagsFilter),
            Builders<QuoteDocument>.Filter.Eq(q => q.DeletedAt, null));

        var limited = Math.Clamp(limit, 1, 25);
        return await _collection.Find(filter).Limit(limited).ToListAsync(cancellationToken);
    }

    public async Task IncrementUsesAsync(string shortId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            return;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var filter = Builders<QuoteDocument>.Filter.Eq(q => q.ShortId, normalized);
        var update = Builders<QuoteDocument>.Update.Inc(q => q.Uses, 1);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> SoftDeleteAsync(string shortId, ulong deletedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            return false;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Eq(q => q.ShortId, normalized),
            Builders<QuoteDocument>.Filter.Eq(q => q.DeletedAt, null));

        var update = Builders<QuoteDocument>.Update
            .Set(q => q.DeletedAt, DateTimeOffset.UtcNow)
            .Set(q => q.DeletedBy, deletedBy);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> RestoreAsync(string shortId, ulong restoredBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortId))
        {
            return false;
        }

        var normalized = shortId.Trim().ToUpperInvariant();
        var filter = Builders<QuoteDocument>.Filter.And(
            Builders<QuoteDocument>.Filter.Eq(q => q.ShortId, normalized),
            Builders<QuoteDocument>.Filter.Ne(q => q.DeletedAt, null));

        var update = Builders<QuoteDocument>.Update
            .Set(q => q.DeletedAt, null)
            .Set(q => q.DeletedBy, null);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }
}
