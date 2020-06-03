using Coravel.Invocable;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class ActivityService : IInvocable
    {
        private readonly DiscordClient Client;
        private bool ShowMembers = false;

        public ActivityService(IServiceProvider provider)
        {
            Client = provider.GetRequiredService<DiscordClient>();
        }

        public async Task Invoke()
        {
            if (ShowMembers)
            {
                var Servers = Client.Guilds.Count;
                var activity = new DiscordActivity($"{Servers} Servers | Shard {Client.ShardId}", ActivityType.Streaming);
                await Client.UpdateStatusAsync(activity);
            }
            else
            {
                var Members = Client.Guilds.Select(g => g.Value.MemberCount).Sum();
                var activity = new DiscordActivity($"{Members} Members | Shard {Client.ShardId}", ActivityType.Watching);
                await Client.UpdateStatusAsync(activity);
            }
            ShowMembers = !ShowMembers;
        }
    }
}
