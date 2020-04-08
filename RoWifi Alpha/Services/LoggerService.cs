using Discord;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class LoggerService
    {
        public async Task LogServer(IGuild guild, Embed embed)
        {
            ITextChannel channel = (await guild.GetTextChannelsAsync()).Where(r => r.Name == "rowifi-logs").FirstOrDefault();
            if(channel != null)
            {
                await channel.SendMessageAsync(embed: embed);
            }
        }
    }
}
