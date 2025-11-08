using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fusion.Bot;
using Fusion.Persistence;
using Fusion.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Fusion Discord bot...");
    MongoConfiguration.Configure();

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            config.AddUserSecrets<Program>(optional: true);
        })
        .UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console();
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<DiscordOptions>(context.Configuration.GetSection("Discord"));
            services.Configure<MongoOptions>(context.Configuration.GetSection(MongoOptions.SectionName));

            services.AddSingleton(provider =>
            {
                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
                };

                return new DiscordSocketClient(config);
            });

            services.AddSingleton(provider =>
            {
                var socketClient = provider.GetRequiredService<DiscordSocketClient>();
                var config = new InteractionServiceConfig
                {
                    LogLevel = LogSeverity.Info,
                    UseCompiledLambda = true
                };

                return new InteractionService(socketClient, config);
            });

            services.AddSingleton<IMongoClient>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "Set the Mongo connection string via 'Mongo:ConnectionString' configuration.");
                }

                return new MongoClient(options.ConnectionString);
            });

            services.AddSingleton<IQuoteRepository, MongoQuoteRepository>();
            services.AddHostedService<MongoIndexInitializer>();

            services.AddSingleton<SlashCommandService>();
            services.AddHostedService<DiscordBotHostedService>();
        })
        .Build();

    await host.RunAsync().ConfigureAwait(false);
}
catch (Exception exception)
{
    Log.Fatal(exception, "Fusion Discord bot terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

internal partial class Program;
