using Coravel.Invocable;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class ActivityService : IInvocable
    {
        public DiscordSocketClient Client { get; set; }
        private bool ShowMembers = false;

        public async Task Invoke()
        {
            if (ShowMembers)
            {
                var Servers = Client.Guilds.Count;
                await Client.SetGameAsync($"{Servers} Servers | Shard {Client.ShardId}");
            }
            else
            {
                var Members = Client.Guilds.Select(g => g.MemberCount).Sum();
                await Client.SetGameAsync($"{Members} Members | Shard {Client.ShardId}");
            }
            ShowMembers = !ShowMembers;
        }
    }
}
