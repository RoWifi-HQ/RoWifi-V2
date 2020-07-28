using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class EventHandler : IHostedService
    {
        private readonly DiscordClient Client;
        private readonly LoggerService Logger;
        private readonly DatabaseService Database;
        private readonly RobloxService Roblox;

        public EventHandler(DiscordClient client, CommandsNextExtension commands, LoggerService logger)
        {
            Client = client;
            Logger = logger;
            Database = commands.Services.GetRequiredService<DatabaseService>();
            Roblox = commands.Services.GetRequiredService<RobloxService>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Client.GuildCreated += OnGuildJoin;
            Client.GuildDeleted += OnGuildLeave;
            Client.GuildMemberAdded += OnMemberJoin;
            return Task.CompletedTask;
        }

        private Task OnMemberJoin(GuildMemberAddEventArgs arg)
        {
            _ = Task.Run(async () =>
            {
                RoGuild guild = await Database.GetGuild(arg.Guild.Id);
                if (guild == null) return;
                if (!guild.Settings.UpdateOnJoin) return;
                RoUser user = await Database.GetUserAsync(arg.Member.Id);
                if (user == null) return;
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                try
                {
                    (List<ulong> AddedRoles, List<ulong> RemovedRoles, string DiscNick) = await user.UpdateAsync(Roblox, arg.Guild, guild, arg.Member);
                    string AddStr = "";
                    foreach (ulong item in AddedRoles)
                        AddStr += $"- <@&{item}>\n";
                    string RemoveStr = "";
                    foreach (ulong item in RemovedRoles)
                        RemoveStr += $"- <@&{item}>\n";

                    AddStr = AddStr.Length == 0 ? "None" : AddStr;
                    RemoveStr = RemoveStr.Length == 0 ? "None" : RemoveStr;
                    DiscNick = DiscNick.Length == 0 ? "None" : DiscNick;

                    embed.AddField("Nickname", DiscNick)
                         .AddField("Added Roles", AddStr)
                         .AddField("Removed Roles", RemoveStr)
                         .WithColor(DiscordColor.Green)
                         .WithTitle("Update");
                    await Logger.LogServer(arg.Guild, embed.Build());
                }
                catch (BlacklistException) { }
                catch (Exception) { }
            });
            return Task.CompletedTask;
        }

        private async Task OnGuildLeave(GuildDeleteEventArgs arg)
        {
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed()
                    .WithTitle("Guild Leave")
                    .WithDescription($"Name: {arg.Guild.Name}\nServer Id: {arg.Guild.Id}\nOwner Id: {arg.Guild.Owner.Id}");
            await Logger.LogEvent(embed.Build());
        }

        private async Task OnGuildJoin(GuildCreateEventArgs arg)
        {
            string content = "Thank you for adding RoWifi! To get started, please set up your server using `!setup`" +
                            "\n\nTo get more information about announcements & updates, please join our support server\nhttps://www.discord.gg/h4BGGyR" +
                            "\n\nTo view our documentation, please visit our website\nhttps://rowifi.link";
            try { await arg.Guild.GetDefaultChannel().SendMessageAsync(content); } catch(Exception) { }
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed()
                    .WithTitle("Guild Join")
                    .WithDescription($"Name: {arg.Guild.Name}\nServer Id: {arg.Guild.Id}\nOwner Id: {arg.Guild.Owner.Id}");
            await Logger.LogEvent(embed.Build());
        }

        public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
