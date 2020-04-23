using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class EventHandler
    {
        private DiscordSocketClient Client;
        private LoggerService Logger;

        public EventHandler(IServiceProvider provider, DiscordSocketClient client, LoggerService logger)
        {
            Client = client;
            Logger = logger;
            Client.JoinedGuild += OnGuildJoin;
            Client.LeftGuild += OnGuildLeave;
        }

        private async Task OnGuildLeave(SocketGuild arg)
        {
            string text = $"Left Guild - {arg.Name}";
            await Logger.LogEvent(text);
        }

        private async Task OnGuildJoin(SocketGuild arg)
        {
            string text = $"Joined Guild - {arg.Name}";
            await Logger.LogEvent(text);
        }
    }
}
