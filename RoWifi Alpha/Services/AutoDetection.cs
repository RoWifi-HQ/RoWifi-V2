using Coravel.Invocable;
using Discord;
using Discord.WebSocket;
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
        private readonly DiscordSocketClient Client;
        private readonly RobloxService Roblox;
        private readonly LoggerService Logger;

        public AutoDetection(IServiceProvider provider, DatabaseService database, RobloxService roblox, DiscordSocketClient client, LoggerService logger)
        {
            Database = database;
            Client = client;
            Roblox = roblox;
            Logger = logger;
        }

        public async Task Invoke()
        {
            if (Client.ConnectionState == ConnectionState.Disconnected) return;
            Stopwatch watch = new Stopwatch();

            var PremiumGuilds = await Database.GetGuilds(Client.Guilds.Select(g => g.Id), true);
            foreach (RoGuild guild in PremiumGuilds)
            {
                IGuild server = Client.GetGuild(guild.GuildId);
                Console.WriteLine("Starting detection on " + server.Name);
                watch.Start();

                Dictionary<ulong, IGuildUser> AllDiscordUsers = (await server.GetUsersAsync()).ToDictionary(x => x.Id, x => x);
                IEnumerable<RoUser> VerifiedUsers = await Database.GetUsersAsync(AllDiscordUsers.Keys);
                var BypassRoleId = server.Roles.Where(r => r != null).Where(r => r.Name == "RoWifi Bypass").FirstOrDefault()?.Id ?? 0;
                foreach (RoUser user in VerifiedUsers)
                {
                    try
                    {
                        if (AllDiscordUsers[user.DiscordId].RoleIds.Contains(BypassRoleId)) continue;
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

                            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
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
