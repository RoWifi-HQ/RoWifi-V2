using Discord;
using RoWifi_Alpha.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Coravel;

namespace RoWifi_Alpha
{
    public class Program
    {
        public static void Main(string[] _)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                DiscordSocketClient client = services.GetRequiredService<DiscordSocketClient>();
                client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscToken"));
                await client.StartAsync();

                services.UseScheduler(scheduler =>
                {
                    scheduler.Schedule<AutoDetection>()
                        .Cron("00 */3 * * *");
                });

                await services.GetRequiredService<CommandHandler>().InitializeAsync();
                await Task.Delay(-1);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<HttpClient>()
                .AddSingleton<InteractiveService>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<LoggerService>();

            services.AddHttpClient<RobloxService>();
            services.AddHttpClient<PatreonService>();
            services.AddMemoryCache();

            services.AddScheduler();

            return services.BuildServiceProvider();
        }
    }
}
