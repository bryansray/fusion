using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fusion.Bot;

public sealed class SlashCommandService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlashCommandService> _logger;
    private readonly ulong? _guildId;
    private bool _commandsRegistered;
    private bool _isInitialized;

    public SlashCommandService(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<DiscordOptions> options,
        ILogger<SlashCommandService> logger)
    {
        _client = client;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interactionService.Log += HandleLogAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _client.Ready += HandleReadyAsync;

        var guildId = options.Value.GuildId;
        if (!string.IsNullOrWhiteSpace(guildId) && ulong.TryParse(guildId, out var parsedGuildId))
        {
            _guildId = parsedGuildId;
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _logger.LogInformation("Loading interaction modules...");
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider).ConfigureAwait(false);
        _isInitialized = true;
        _logger.LogInformation("Loaded {ModuleCount} interaction modules.", _interactionService.Modules.Count);
    }

    private async Task HandleReadyAsync()
    {
        if (_commandsRegistered || !_isInitialized)
        {
            return;
        }

        if (_guildId is ulong guildId)
        {
            await _interactionService.RegisterCommandsToGuildAsync(guildId, deleteMissing: true).ConfigureAwait(false);
            _logger.LogInformation(
                "Registered {CommandCount} slash commands to guild {GuildId}.",
                _interactionService.SlashCommands.Count,
                guildId);
        }
        else
        {
            await _interactionService.RegisterCommandsGloballyAsync(deleteMissing: true).ConfigureAwait(false);
            _logger.LogInformation(
                "Registered {CommandCount} slash commands globally.",
                _interactionService.SlashCommands.Count);
        }

        _commandsRegistered = true;
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider).ConfigureAwait(false);

            if (!result.IsSuccess && result.Error != InteractionCommandError.UnmetPrecondition)
            {
                _logger.LogWarning(
                    "Interaction execution failed for {InteractionId}: {Error} - {Reason}",
                    interaction.Id,
                    result.Error,
                    result.ErrorReason);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while executing interaction {InteractionId}", interaction.Id);

            if (interaction is IDiscordInteraction discordInteraction && !discordInteraction.HasResponded)
            {
                await discordInteraction.RespondAsync(
                    "Sorry, something went wrong while processing that command.",
                    ephemeral: true).ConfigureAwait(false);
            }
        }
#pragma warning restore CA1031 // Do not catch general exception types
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
}
