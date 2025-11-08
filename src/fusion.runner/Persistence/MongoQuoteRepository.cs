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
        var filter = Builders<QuoteDocument>.Filter.Eq(q => q.PersonKey, author);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<QuoteDocument?> GetByShortIdAsync(string shortId, CancellationToken cancellationToken = default)
    {
        var normalized = shortId.Trim().ToUpperInvariant();
        var filter = Builders<QuoteDocument>.Filter.Eq(q => q.ShortId, normalized);
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
        var filter = Builders<QuoteDocument>.Filter.Regex(q => q.ShortId, new BsonRegularExpression(pattern));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }
}
