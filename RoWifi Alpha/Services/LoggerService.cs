using Coravel.Invocable;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class LoggerService : IInvocable
    {
        public DiscordWebhookClient DebugWebhook = new DiscordWebhookClient(Environment.GetEnvironmentVariable("LOG_DEBUG"));
        public DiscordWebhookClient PremiumWebhook = new DiscordWebhookClient(Environment.GetEnvironmentVariable("LOG_PREMIUM"));
        public DiscordWebhookClient MainWebhook = new DiscordWebhookClient(Environment.GetEnvironmentVariable("LOG_MAIN"));

        private static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly string projectId = "ro-wifi";
        private readonly MetricServiceClient MetricClient = MetricServiceClient.Create();

        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;

        public LoggerService(IServiceProvider provider, DiscordSocketClient client)
        {
            _provider = provider;
            _client = client;
        }

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
            await DebugWebhook.SendMessageAsync(text);
        }

        public async Task LogEvent(string text)
        {
            await MainWebhook.SendMessageAsync(text);
        }

        public async Task LogPremium(string text)
        {
            await PremiumWebhook.SendMessageAsync(text);
        }

        public async Task LogAction(IGuild guild, SocketUser user, params object[] values)
        {
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle($"Action by {user.Username}").WithDescription(values[0].ToString())
                .AddField(values[1].ToString(), values[2].ToString());
            await LogServer(guild, embed.Build());
        }

        private TimeSeries GetMetricInfo(string Type, int Shard, long value)
        {
            Point dataPoint = new Point();
            TypedValue metricTotal = new TypedValue { Int64Value = value };
            dataPoint.Value = metricTotal;
            Timestamp timeStamp = new Timestamp { Seconds = (long)(DateTime.UtcNow - s_unixEpoch).TotalSeconds };
            TimeInterval interval = new TimeInterval{ EndTime = timeStamp };
            dataPoint.Interval = interval;

            Metric metric = new Metric{ Type = $"custom.googleapis.com/rowifi/{Type}" };
            metric.Labels.Add("shard_id", Shard.ToString());

            MonitoredResource resource = new MonitoredResource { Type = "global" };
            resource.Labels.Add("project_id", projectId);

            TimeSeries timeSeriesData = new TimeSeries {Metric = metric, Resource = resource };
            timeSeriesData.Points.Add(dataPoint);

            return timeSeriesData;
        }

        public async Task LogMetrics()
        {
            var guildData = GetMetricInfo("guilds", _client.ShardId, _client.Guilds.Count);
            var usersData = GetMetricInfo("users", _client.ShardId, _client.Guilds.Select(g => g.MemberCount).Sum());

            ProjectName name = new ProjectName(projectId);
            IEnumerable<TimeSeries> timeSeries = new List<TimeSeries> { guildData, usersData };

            await MetricClient.CreateTimeSeriesAsync(name, timeSeries);
        }

        public async Task Invoke()
        {
            await LogMetrics();
        }
    }
}
