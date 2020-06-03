using DSharpPlus;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class DiscordBot : IHostedService
    {
        private DiscordClient Client;

        public DiscordBot(DiscordClient Client)
        {
            this.Client = Client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var task = Client.ConnectAsync();
            await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var task = Client.DisconnectAsync();
            await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
}
