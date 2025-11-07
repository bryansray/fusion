using Discord.Interactions;

namespace Fusion.Runner.Modules;

public sealed class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with a Pong! message.")]
    public async Task HandlePingAsync()
    {
        await RespondAsync("Pong!");
    }
}
