using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class EventHandler : InitializedService
    {
        private DiscordSocketClient Client;
        private readonly LoggerService Logger;
        private readonly DatabaseService Database;
        private readonly RobloxService Roblox;

        public EventHandler(IServiceProvider provider, DiscordSocketClient client, LoggerService logger, DatabaseService database, RobloxService roblox)
        {
            Client = client;
            Logger = logger;
            Database = database;
            Roblox = roblox;
        }

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            Client.JoinedGuild += OnGuildJoin;
            Client.LeftGuild += OnGuildLeave;
            Client.UserJoined += OnMemberJoin;
            return Task.CompletedTask;
        }

        private async Task OnMemberJoin(SocketGuildUser arg)
        {
            RoGuild guild = await Database.GetGuild(arg.Guild.Id);
            if (guild == null) return;
            if (!guild.Settings.UpdateOnJoin) return;
            RoUser user = await Database.GetUserAsync(arg.Id);
            if (user == null) return;
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            try
            {
                (List<ulong> AddedRoles, List<ulong> RemovedRoles, string DiscNick) = await user.UpdateAsync(Roblox, arg.Guild, guild, arg);
                string AddStr = "";
                foreach (ulong item in AddedRoles)
                    AddStr += $"- <@&{item}>\n";
                string RemoveStr = "";
                foreach (ulong item in RemovedRoles)
                    RemoveStr += $"- <@&{item}>\n";

                AddStr = AddStr.Length == 0 ? "None" : AddStr;
                RemoveStr = RemoveStr.Length == 0 ? "None" : RemoveStr;
                DiscNick = DiscNick.Length == 0 ? "None" : DiscNick;

                var fields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder().WithName("Nickname").WithValue(DiscNick),
                    new EmbedFieldBuilder().WithName("Added Roles").WithValue(AddStr),
                    new EmbedFieldBuilder().WithName("Removed Roles").WithValue(RemoveStr)
                };

                embed.WithFields(fields).WithColor(Color.Green).WithTitle("Update");
                await Logger.LogServer(arg.Guild, embed.Build());
            }
            catch (BlacklistException) { }
            catch (Exception) { }
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
