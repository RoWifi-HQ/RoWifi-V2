using Coravel.Invocable;
using DSharpPlus;
using DSharpPlus.Entities;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class LoggerService : IInvocable
    {
        private readonly DiscordWebhookClient Webhooks;
        private readonly DiscordWebhook Debug;
        private readonly DiscordWebhook Premium;
        private readonly DiscordWebhook Main;

        private static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly string projectId = "rowifi";
        private readonly MetricServiceClient MetricClient = MetricServiceClient.Create();

        private readonly DiscordClient _client;

        public LoggerService(IServiceProvider services)
        {
            _client = services.GetService<DiscordClient>();
            Webhooks = new DiscordWebhookClient();
            Debug = Webhooks.AddWebhookAsync(new Uri(Environment.GetEnvironmentVariable("LOG_DEBUG"))).GetAwaiter().GetResult();
            Premium = Webhooks.AddWebhookAsync(new Uri(Environment.GetEnvironmentVariable("LOG_PREMIUM"))).GetAwaiter().GetResult();
            Main = Webhooks.AddWebhookAsync(new Uri(Environment.GetEnvironmentVariable("LOG_MAIN"))).GetAwaiter().GetResult();
        }

        public async Task LogServer(DiscordGuild guild, DiscordEmbed embed)
        {
            try
            {
                DiscordChannel channel = guild.Channels.Values.Where(r => r.Name == "rowifi-logs").FirstOrDefault();
                if (channel != null)
                {
                    await channel.SendMessageAsync(embed: embed);
                }
            }
            catch(Exception) { }
        }

        public async Task LogDebug(string text)
        {
            await Debug.ExecuteAsync(new DiscordWebhookBuilder 
            { 
                Content = text
            });
        }

        public async Task LogEvent(DiscordEmbed embed)
        {
            await Main.ExecuteAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }

        public async Task LogPremium(string text)
        {
            await Premium.ExecuteAsync(new DiscordWebhookBuilder
            {
                Content = text
            });
        }

        public async Task LogAction(DiscordGuild guild, DiscordUser user, params object[] values)
        {
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
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
            var usersData = GetMetricInfo("users", _client.ShardId, _client.Guilds.Select(g => g.Value.MemberCount).Sum());

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
