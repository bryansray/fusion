using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;
using Fusion.Infrastructure.RaiderIO;
using Fusion.Infrastructure.Warcraft;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fusion.Infrastructure.Tests;

public sealed class RaiderIoClientTests
{
    private static RaiderIoClient CreateClient(HttpClient httpClient, RaiderIoOptions? overrideOptions = null)
    {
        var options = overrideOptions ?? new RaiderIoOptions
        {
            Region = BlizzardRegions.Us,
            BaseUrl = new Uri("https://raider.io/api/v1"),
            DefaultFields = "gear"
        };

        return new RaiderIoClient(httpClient, Options.Create(options), NullLogger<RaiderIoClient>.Instance);
    }

    [Fact]
    public async Task GetCharacterAsyncReturnsProfile()
    {
        using var handler = new TestHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """
            {
              "name": "Thrall",
              "class": "Shaman",
              "race": "Orc",
              "realm": "Area 52",
              "region": "us",
              "gear": { "item_level_equipped": 470, "item_level_total": 475 }
            }
            """);

        using var httpClient = handler.CreateClient();
        var client = CreateClient(httpClient);

        var profile = await client.GetCharacterAsync("Area 52", "Thrall");

        Assert.NotNull(profile);
        Assert.Equal("Shaman", profile!.Class);
        Assert.Equal(470, profile.Gear?.ItemLevelEquipped);
    }

    [Fact]
    public async Task GetCharacterAsyncReturnsNullWhenNotFound()
    {
        using var handler = new TestHttpMessageHandler()
            .Enqueue(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = handler.CreateClient();
        var client = CreateClient(httpClient);

        var profile = await client.GetCharacterAsync("eu", "draenor", "unknown", null);

        Assert.Null(profile);
    }

    [Fact]
    public async Task GetGuildAsyncReturnsProfile()
    {
        using var handler = new TestHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """
            {
              "name": "Echo",
              "realm": "Tarren Mill",
              "region": "eu",
              "faction": "horde"
            }
            """);

        using var httpClient = handler.CreateClient();
        var client = CreateClient(httpClient);

        var guild = await client.GetGuildAsync("Tarren Mill", "Echo");

        Assert.NotNull(guild);
        Assert.Equal("Echo", guild!.Name);
        Assert.Equal("horde", guild.Faction);
    }

    [Fact]
    public async Task GetGuildAsyncReturnsNullWhenNotFound()
    {
        using var handler = new TestHttpMessageHandler()
            .Enqueue(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = handler.CreateClient();
        var client = CreateClient(httpClient);

        var guild = await client.GetGuildAsync("us", "illidan", "unknown", null);

        Assert.Null(guild);
    }

    [Fact]
    public async Task GetCharacterAsyncAddsApiKeyHeaderWhenConfigured()
    {
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var options = new RaiderIoOptions
        {
            Region = BlizzardRegions.Eu,
            BaseUrl = new Uri("https://raider.io/api/v1"),
            ApiKey = "test-key"
        };

        using var httpClient = handler.CreateClient();
        var client = CreateClient(httpClient, options);

        await client.GetCharacterAsync("eu", "twisting-nether", "thrall", null);

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("x-api-key", out var values));
        Assert.Contains("test-key", values);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public TestHttpMessageHandler Enqueue(Func<HttpResponseMessage> factory)
        {
            _responses.Enqueue(factory);
            return this;
        }

        public TestHttpMessageHandler EnqueueJson(HttpStatusCode statusCode, string json)
        {
            return Enqueue(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException($"No response configured for {request.Method} {request.RequestUri}.");
            }

            var response = _responses.Dequeue();
            return Task.FromResult(response());
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
