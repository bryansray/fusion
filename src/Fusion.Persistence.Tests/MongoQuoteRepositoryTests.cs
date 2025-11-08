using Bogus;
using Fusion.Persistence;
using Fusion.Persistence.Models;
using Fusion.Persistence.Tests.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Fusion.Persistence.Tests;

public sealed class MongoQuoteRepositoryTests : IClassFixture<MongoDbFixture>
{
    private static readonly Faker Faker = new();
    private readonly MongoDbFixture _fixture;

    public MongoQuoteRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private MongoQuoteRepository CreateRepository()
    {
        var options = Options.Create(new MongoOptions
        {
            ConnectionString = _fixture.Client.Settings.Server.ToString(),
            DatabaseName = _fixture.DatabaseName,
            QuotesCollectionName = "quotes"
        });

        return new MongoQuoteRepository(_fixture.Client, options, new NullLogger<MongoQuoteRepository>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_and_find_quote_roundtrip()
    {
        var repository = CreateRepository();
        var document = new QuoteDocument
        {
            Person = Faker.Name.FullName(),
            Message = Faker.Lorem.Sentence(),
            GuildId = 123,
            ChannelId = 456,
            AddedBy = 789
        };

        await repository.InsertAsync(document);
        var fetched = await repository.GetByShortIdAsync(document.ShortId);

        Assert.NotNull(fetched);
        Assert.Equal(document.Person, fetched!.Person);
    }
}
