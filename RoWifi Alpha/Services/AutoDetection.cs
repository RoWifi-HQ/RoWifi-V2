using Coravel.Invocable;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Services
{
    public class AutoDetection : IInvocable
    {
        private readonly DatabaseService Database;
        private readonly DiscordClient Client;
        private readonly RobloxService Roblox;
        private readonly LoggerService Logger;

        public AutoDetection(DiscordClient client, CommandsNextExtension commands, LoggerService logger)
        {
            Database = commands.Services.GetRequiredService<DatabaseService>();
            Client = client;
            Roblox = commands.Services.GetRequiredService<RobloxService>();
            Logger = logger;
        }

        public async Task Invoke()
        {
            Stopwatch watch = new Stopwatch();

            var PremiumGuilds = await Database.GetGuilds(Client.Guilds.Select(g => g.Key), true);
            foreach (RoGuild guild in PremiumGuilds)
            {
                DiscordGuild server = await Client.GetGuildAsync(guild.GuildId);
                Console.WriteLine("Starting detection on " + server.Name);
                watch.Start();

                Dictionary<ulong, DiscordMember> AllDiscordUsers = (await server.GetAllMembersAsync()).ToDictionary(x => x.Id, x => x);
                IEnumerable<RoUser> VerifiedUsers = await Database.GetUsersAsync(AllDiscordUsers.Keys);
                var BypassRoleId = server.Roles.Values.Where(r => r != null).Where(r => r.Name == "RoWifi Bypass").FirstOrDefault()?.Id ?? 0;
                foreach (RoUser user in VerifiedUsers)
                {
                    try
                    {
                        if (AllDiscordUsers[user.DiscordId].Roles.Where(r => r != null).Any(r => r.Name == "RoWifi Bypass")) continue;
                        (List<ulong> AddedRoles, List<ulong> RemovedRoles, string DiscNick) = await user.UpdateAsync(Roblox, server, guild,
                        AllDiscordUsers[user.DiscordId], "Auto Detection");

                        if (AddedRoles.Count > 0 || RemovedRoles.Count > 0)
                        {
                            string AddStr = "";
                            foreach (ulong item in AddedRoles)
                                AddStr += $"- <@&{item}>\n";
                            string RemoveStr = "";
                            foreach (ulong item in RemovedRoles)
                                RemoveStr += $"- <@&{item}>\n";

                            AddStr = AddStr.Length == 0 ? "None" : AddStr;
                            RemoveStr = RemoveStr.Length == 0 ? "None" : RemoveStr;
                            DiscNick = DiscNick.Length == 0 ? "None" : DiscNick;

                            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                            embed.WithTitle($"Auto Detection [{AllDiscordUsers[user.DiscordId].Nickname}]")
                                .AddField("Nickname", DiscNick)
                                .AddField("Added Roles", AddStr)
                                .AddField("Removed Roles", RemoveStr);
                            await Logger.LogServer(server, embed.Build());
                        }
                    } catch(Exception) { }
                }
                watch.Stop();
                TimeSpan ts = watch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                await Logger.LogPremium($"Auto Detection [{server.Name}] - {elapsedTime}");
                watch.Reset();
            }
        }
    }
}
