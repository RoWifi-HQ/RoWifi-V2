using Coravel;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoWifi_Alpha.Commands;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace RoWifi_Alpha
{
    public class Program
    {
        public static void Main(string[] _)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging(x =>
                {
                    x.ClearProviders();
                    x.AddConsole();
                    x.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureServices(services => 
                {
                    var config = new DiscordConfiguration
                    {
                        Token = Environment.GetEnvironmentVariable("DiscToken"),
                        TokenType = TokenType.Bot,
                        AutoReconnect = true,
                        ShardId = int.Parse(Environment.GetEnvironmentVariable("SHARD").Split("-").LastOrDefault() ?? "0"),
                        ShardCount = int.Parse(Environment.GetEnvironmentVariable("TOTAL_SHARDS")),
                        LogLevel = DSharpPlus.LogLevel.Debug,
                        UseInternalLogHandler = true
                    };
                    services.AddSingleton(config);

                    var Client = new DiscordClient(config);
                    var deps = new ServiceCollection()
                        .AddSingleton<DatabaseService>()
                        .AddSingleton<LoggerService>()
                        .AddHttpClient()
                        .AddSingleton(Client)
                        .AddMemoryCache();

                    deps.AddHttpClient<RobloxService>();
                    deps.AddHttpClient<PatreonService>();

                    Client.UseCommandsNext(new CommandsNextConfiguration
                    {
                        CaseSensitive = false,
                        DmHelp = false,
                        EnableDefaultHelp = true,
                        EnableDms = false,
                        EnableMentionPrefix = true,
                        UseDefaultCommandHandler = false,
                        Services = deps.BuildServiceProvider()
                    });
                    Client.UseInteractivity(new InteractivityConfiguration
                    {
                        Timeout = TimeSpan.FromMinutes(1),
                        PaginationDeletion = PaginationDeletion.DeleteMessage
                    });
                    var Commands = Client.GetCommandsNext();
                    Commands.RegisterCommands(typeof(UserAdmin).Assembly);

                    services.AddSingleton(Client)
                        .AddSingleton(Commands)
                        .AddSingleton<LoggerService>()
                        .AddSingleton<ActivityService>()
                        .AddSingleton<AutoDetection>()
                        .AddHostedService<Services.EventHandler>()
                        .AddHostedService<DiscordBot>()
                        .AddHostedService<CommandHandler>()
                        .AddScheduler();
                })
                .UseConsoleLifetime();

            var host = builder.Build();

            host.Services.UseScheduler(scheduler =>
            {
                scheduler.OnWorker("CPU Intensive");
                scheduler.Schedule<AutoDetection>()
                    .Cron("00 */3 * * *");
                scheduler.OnWorker("Logging");
                scheduler.Schedule<LoggerService>()
                    .Hourly();
                scheduler.Schedule<ActivityService>()
                    .EveryTenMinutes();
            })
            .OnError((exception) =>
            {
                Console.WriteLine(exception.Message);
            });

            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}
