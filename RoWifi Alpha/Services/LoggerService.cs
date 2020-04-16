using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class LoggerService
    {
        public DiscordWebhookClient Webhook = new DiscordWebhookClient(Environment.GetEnvironmentVariable("LOG_DEBUG"));

        public async Task LogServer(IGuild guild, Embed embed)
        {
            ITextChannel channel = (await guild.GetTextChannelsAsync()).Where(r => r.Name == "rowifi-logs").FirstOrDefault();
            if(channel != null)
            {
                await channel.SendMessageAsync(embed: embed);
            }
        }

        public async Task LogDebug(string text)
        {
            await Webhook.SendMessageAsync(text);
        }
    }
}
