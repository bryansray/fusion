using System.Net;
using System.Net.Http;
using System.Text;
using Fusion.Infrastructure.Warcraft;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fusion.Infrastructure.Tests;

public sealed class WarcraftClientTests
{
    private static WarcraftClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(new WarcraftOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Region = "us",
            Locale = "en_US"
        });

        return new WarcraftClient(httpClient, options, NullLogger<WarcraftClient>.Instance);
    }

    [Fact]
    public async Task GetCharacterAsyncReturnsProfileWhenFound()
    {
        using var handler = new TestHttpMessageHandler()
            .EnqueueJsonResponse(HttpStatusCode.OK, """{"access_token":"token","expires_in":60}""")
            .EnqueueJsonResponse(HttpStatusCode.OK, """
            {
              "id": 8675309,
              "name": "Thrall",
              "level": 70,
              "character_class": { "id": 1, "name": "Shaman" },
              "realm": { "id": 1, "name": "Area 52", "slug": "area-52" }
            }
            """);

        using var httpClient = handler.CreateClient();
        using var client = CreateClient(httpClient);

        var profile = await client.GetCharacterAsync("Area 52", "Thrall");

        Assert.NotNull(profile);
        Assert.Equal("Thrall", profile!.Name);
        Assert.Equal(70, profile.Level);
        Assert.Equal("area-52", profile.Realm?.Slug);
        Assert.Equal("Shaman", profile.CharacterClass?.Name);
    }

    [Fact]
    public async Task GetCharacterAsyncReturnsNullWhenCharacterMissing()
    {
        using var handler = new TestHttpMessageHandler()
            .EnqueueJsonResponse(HttpStatusCode.OK, """{"access_token":"token","expires_in":60}""")
            .Enqueue(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = handler.CreateClient();
        using var client = CreateClient(httpClient);

        var profile = await client.GetCharacterAsync("eu", "Blackrock", "Unknown");

        Assert.Null(profile);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public TestHttpMessageHandler Enqueue(Func<HttpResponseMessage> factory)
        {
            _responses.Enqueue(factory);
            return this;
        }

        public TestHttpMessageHandler EnqueueJsonResponse(HttpStatusCode statusCode, string json)
        {
            return Enqueue(() =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                return response;
            });
        }

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException($"No response configured for {request.Method} {request.RequestUri}.");
            }

            var factory = _responses.Dequeue();
            return Task.FromResult(factory());
        }
    }
}
