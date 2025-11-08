using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fusion.Infrastructure.Warcraft.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Fusion.Infrastructure.Warcraft;

public interface IWarcraftClient
{
    Task<CharacterProfile?> GetCharacterAsync(
        string realm,
        string character,
        CancellationToken cancellationToken = default);

    Task<CharacterProfile?> GetCharacterAsync(
        string region,
        string realm,
        string character,
        CancellationToken cancellationToken = default);
}

public sealed class WarcraftClient : IWarcraftClient, IDisposable
{
    private static readonly Regex SlugPattern = new("[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly WarcraftOptions _options;
    private readonly ILogger<WarcraftClient> _logger;
    private readonly ConcurrentDictionary<string, AccessToken> _tokenCache = new();
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

    public WarcraftClient(HttpClient httpClient, IOptions<WarcraftOptions> options, ILogger<WarcraftClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<CharacterProfile?> GetCharacterAsync(
        string realm,
        string character,
        CancellationToken cancellationToken = default) =>
        GetCharacterAsync(_options.Region, realm, character, cancellationToken);

    public async Task<CharacterProfile?> GetCharacterAsync(
        string region,
        string realm,
        string character,
        CancellationToken cancellationToken = default)
    {
        var regionCode = BlizzardRegions.Normalize(region);
        var token = await GetAccessTokenAsync(regionCode, cancellationToken).ConfigureAwait(false);
        var locale = string.IsNullOrWhiteSpace(_options.Locale) ? "en_US" : _options.Locale.Trim();
        var namespaceValue = $"profile-{regionCode}";
        var realmSlug = Slugify(realm);
        var characterSlug = Slugify(character);
        var endpoint =
            $"https://{regionCode}.api.blizzard.com/profile/wow/character/{realmSlug}/{characterSlug}?namespace={namespaceValue}&locale={locale}";

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Character {Character} on {Realm} ({Region}) was not found.",
                character,
                realm,
                regionCode);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await JsonSerializer.DeserializeAsync<CharacterProfile>(contentStream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await contentStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<string> GetAccessTokenAsync(string region, CancellationToken cancellationToken)
    {
        if (_tokenCache.TryGetValue(region, out var cachedToken) && !cachedToken.IsExpired())
        {
            return cachedToken.Token;
        }

        await _tokenSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tokenCache.TryGetValue(region, out cachedToken) && !cachedToken.IsExpired())
            {
                return cachedToken.Token;
            }

            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Warcraft options require ClientId and ClientSecret to request an access token.");
            }

            var tokenEndpoint = $"https://{region}.battle.net/oauth/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                })
            };

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var tokenResponse = await JsonSerializer
                    .DeserializeAsync<TokenResponse>(contentStream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);

                if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                {
                    throw new InvalidOperationException("Blizzard token response was empty.");
                }

                cachedToken = new AccessToken(tokenResponse.AccessToken, region, tokenResponse.ExpiresIn);
                _tokenCache[region] = cachedToken;

                _logger.LogInformation(
                    "Fetched Blizzard access token for region {Region}. Expires in {ExpiresIn} seconds.",
                    region,
                    tokenResponse.ExpiresIn);

                return cachedToken.Token;
            }
            finally
            {
                await contentStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));
        }

#pragma warning disable CA1308 // Normalize strings to uppercase
        var normalized = value.Trim().ToLowerInvariant().Replace(' ', '-');
#pragma warning restore CA1308 // Normalize strings to uppercase
        normalized = SlugPattern.Replace(normalized, string.Empty);
        return normalized;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record AccessToken(string Token, string Region, int ExpiresInSeconds)
    {
        private readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

        public bool IsExpired() => DateTimeOffset.UtcNow >= _createdAt.AddSeconds(Math.Max(1, ExpiresInSeconds - 30));
    }

    public void Dispose()
    {
        _tokenSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
