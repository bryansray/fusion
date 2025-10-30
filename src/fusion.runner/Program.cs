using Discord;
using Discord.WebSocket;
using Fusion.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        config.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<DiscordOptions>(context.Configuration.GetSection("Discord"));

        services.AddSingleton(provider =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            return new DiscordSocketClient(config);
        });

        services.AddHostedService<DiscordBotHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
    })
    .Build();

await host.RunAsync();

public partial class Program;
