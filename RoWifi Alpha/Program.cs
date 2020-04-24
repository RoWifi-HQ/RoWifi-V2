using Discord;
using RoWifi_Alpha.Addons.Interactive;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Coravel;
using Microsoft.Extensions.Hosting;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Reliability;
using Microsoft.Extensions.Logging;

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
                    x.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureDiscordHost<DiscordSocketClient>((context, config) =>
                {
                    config.SetToken(Environment.GetEnvironmentVariable("DiscToken"));
                    config.SetDiscordConfiguration(new DiscordSocketConfig
                    {
                        AlwaysDownloadUsers = true,
                        LogLevel = LogSeverity.Info
                    });
                })
                .UseCommandService((context, config) =>
                {
                    config.LogLevel = LogSeverity.Info;
                    config.CaseSensitiveCommands = false;
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<CommandHandler>()
                    .AddSingleton<IHostedService>(s => s.GetService<CommandHandler>())
                    .AddSingleton<HttpClient>()
                    .AddSingleton<InteractiveService>()
                    .AddSingleton<DatabaseService>()
                    .AddSingleton<LoggerService>()
                    .AddSingleton<ActivityService>()
                    .AddSingleton<AutoDetection>();

                    services.AddHttpClient<RobloxService>();
                    services.AddHttpClient<PatreonService>();
                    services.AddMemoryCache();

                    services.AddScheduler();
                })
                .UseConsoleLifetime();

            IHost host = builder.Build();

            host.Services.UseScheduler(scheduler =>
            {
                scheduler.OnWorker("CPU Intensive");
                scheduler.Schedule<AutoDetection>()
                    .Cron("00 */3 * * *");
                scheduler.OnWorker("Logging");
                scheduler.Schedule<LoggerService>()
                    .EveryFiveMinutes();
                scheduler.Schedule<ActivityService>()
                    .EveryTenMinutes();
            })
            .OnError((exception) =>
            {
                Console.WriteLine(exception.Message);
            });

            using (host)
            {
                await host.RunReliablyAsync();
            }
        }
    }
}
