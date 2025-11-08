using System.Net;
using System.Text.Json;
using Fusion.Infrastructure.RaiderIO.Models;
using Fusion.Infrastructure.Warcraft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fusion.Infrastructure.RaiderIO;

public interface IRaiderIoClient
{
    Task<RaiderIoCharacterProfile?> GetCharacterAsync(
        string realm,
        string character,
        CancellationToken cancellationToken = default);

    Task<RaiderIoCharacterProfile?> GetCharacterAsync(
        string region,
        string realm,
        string character,
        string? fields,
        CancellationToken cancellationToken = default);
}

public sealed class RaiderIoClient : IRaiderIoClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly RaiderIoOptions _options;
    private readonly ILogger<RaiderIoClient> _logger;

    public RaiderIoClient(HttpClient httpClient, IOptions<RaiderIoOptions> options, ILogger<RaiderIoClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<RaiderIoCharacterProfile?> GetCharacterAsync(
        string realm,
        string character,
        CancellationToken cancellationToken = default) =>
        GetCharacterAsync(_options.Region, realm, character, _options.DefaultFields, cancellationToken);

    public async Task<RaiderIoCharacterProfile?> GetCharacterAsync(
        string region,
        string realm,
        string character,
        string? fields,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl?.ToString().TrimEnd('/') ?? "https://raider.io/api/v1";
        var normalizedRegion = BlizzardRegions.Normalize(region);

        var endpoint = BuildCharacterUri(baseUrl, normalizedRegion, realm, character, fields);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        }
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Raider.IO character {Character} on {Realm} ({Region}) not found.",
                character,
                realm,
                normalizedRegion);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await JsonSerializer.DeserializeAsync<RaiderIoCharacterProfile>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static Uri BuildCharacterUri(string baseUrl, string region, string realm, string character, string? fields)
    {
        if (string.IsNullOrWhiteSpace(realm))
        {
            throw new ArgumentException("Realm is required.", nameof(realm));
        }

        if (string.IsNullOrWhiteSpace(character))
        {
            throw new ArgumentException("Character is required.", nameof(character));
        }

        static string Encode(string value) => Uri.EscapeDataString(value.Trim());

        var builder = new UriBuilder($"{baseUrl}/characters/profile")
        {
            Query = $"region={Encode(region)}&realm={Encode(realm)}&name={Encode(character)}"
        };

        if (!string.IsNullOrWhiteSpace(fields))
        {
            builder.Query += $"&fields={fields}";
        }

        return builder.Uri;
    }
}
