using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion.Persistence.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Fusion.Persistence;

public sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoCollection<QuoteDocument> _collection;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(
        IMongoClient client,
        IOptions<MongoOptions> options,
        ILogger<MongoIndexInitializer> logger)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        var mongoOptions = options.Value;

        if (string.IsNullOrWhiteSpace(mongoOptions.DatabaseName))
        {
            throw new InvalidOperationException("Mongo database name is required to initialize indexes.");
        }

        var collectionName = string.IsNullOrWhiteSpace(mongoOptions.QuotesCollectionName)
            ? "quotes"
            : mongoOptions.QuotesCollectionName;

        var database = client.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<QuoteDocument>(collectionName);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<QuoteDocument>>
        {
            new(Builders<QuoteDocument>.IndexKeys.Ascending(q => q.ShortId), new CreateIndexOptions
            {
                Name = "quotes_shortId_unique",
                Unique = true
            }),
            new(Builders<QuoteDocument>.IndexKeys.Ascending(q => q.PersonKey), new CreateIndexOptions
            {
                Name = "quotes_personKey",
            }),
            new(Builders<QuoteDocument>.IndexKeys.Ascending(q => q.DeletedAt), new CreateIndexOptions
            {
                Name = "quotes_deletedAt"
            }),
            new(Builders<QuoteDocument>.IndexKeys.Ascending(q => q.Tags), new CreateIndexOptions
            {
                Name = "quotes_tags",
                Sparse = true
            })
        };

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var result = await _collection.Indexes.CreateManyAsync(models, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Ensured Mongo indexes: {IndexNames}", string.Join(", ", result));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to ensure Mongo indexes for quotes collection.");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
