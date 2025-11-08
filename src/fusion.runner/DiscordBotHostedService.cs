using Discord;
using Discord.WebSocket;
using Fusion.Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fusion.Runner;

internal sealed class DiscordBotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private readonly DiscordOptions _options;
    private readonly ulong? _guildId;
    private readonly SlashCommandService _slashCommandService;
    private bool _isStarted;

    public DiscordBotHostedService(
        DiscordSocketClient client,
        IOptions<DiscordOptions> options,
        SlashCommandService slashCommandService,
        ILogger<DiscordBotHostedService> logger)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _slashCommandService = slashCommandService;

        if (!string.IsNullOrWhiteSpace(_options.GuildId))
        {
            if (ulong.TryParse(_options.GuildId, out var guildId))
            {
                _guildId = guildId;
            }
            else
            {
                _logger.LogWarning(
                    "Configured guild id '{GuildId}' is not valid. Update appsettings.json or user secrets with the numeric guild id.",
                    _options.GuildId);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = _options.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError(
                "Missing Discord bot token. Set it with: dotnet user-secrets set \"Discord:Token\" \"<token>\"");
            throw new InvalidOperationException("Discord bot token was not configured.");
        }

        _client.Log += HandleLogAsync;
        _client.Ready += HandleReadyAsync;

        await _slashCommandService.InitializeAsync().ConfigureAwait(false);

        _logger.LogInformation("Starting Discord client...");

        await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);

        _isStarted = true;

        _logger.LogInformation("Discord client started. Waiting for ready event...");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isStarted)
        {
            return;
        }

        _logger.LogInformation("Stopping Discord client...");

        if (_client.ConnectionState == ConnectionState.Connected)
        {
            await _client.StopAsync().ConfigureAwait(false);
        }

        if (_client.LoginState == LoginState.LoggedIn)
        {
            await _client.LogoutAsync().ConfigureAwait(false);
        }

        _client.Log -= HandleLogAsync;
        _client.Ready -= HandleReadyAsync;

        _isStarted = false;
    }

    private Task HandleLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        if (message.Exception is not null)
        {
            _logger.Log(level, message.Exception, "{Source}: {Message}", message.Source, message.Message);
        }
        else
        {
            _logger.Log(level, "{Source}: {Message}", message.Source, message.Message);
        }

        return Task.CompletedTask;
    }

    private Task HandleReadyAsync()
    {
        var currentUser = _client.CurrentUser;
        _logger.LogInformation(
            "Connected as {Username}#{Discriminator} ({UserId})",
            currentUser.Username,
            currentUser.Discriminator,
            currentUser.Id);

        if (_guildId is ulong guildId)
        {
            var guild = _client.GetGuild(guildId);
            if (guild is not null)
            {
                _logger.LogInformation("Watching guild {GuildName} ({GuildId})", guild.Name, guild.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Bot does not have visibility of guild id {GuildId}. Invite it to the server with that id.",
                    guildId);
            }
        }
        else if (!string.IsNullOrWhiteSpace(_options.GuildId))
        {
            _logger.LogWarning("Guild id '{GuildId}' is not numeric and was ignored.", _options.GuildId);
        }

        if (!string.IsNullOrWhiteSpace(_options.Status))
        {
            _logger.LogInformation("Setting presence to '{Status}'", _options.Status);
            return _client.SetGameAsync(_options.Status, type: ActivityType.Playing);
        }

        return Task.CompletedTask;
    }
}
