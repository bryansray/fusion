using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Fusion.Persistence.Tests.Containers;

public sealed class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    public IMongoClient Client { get; private set; } = default!;
    public string DatabaseName => "fusion-tests";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Client = new MongoClient(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }
}
