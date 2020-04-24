using Coravel.Invocable;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class ActivityService : IInvocable
    {
        private readonly DiscordSocketClient Client;
        private bool ShowMembers = false;

        public ActivityService(IServiceProvider provider, DiscordSocketClient client)
        {
            Client = client;
        }

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
