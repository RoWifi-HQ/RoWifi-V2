using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RoWifi_Alpha.Addons.Interactive;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    public class GroupAdmin : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [Command("setup", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to start configuration of the server")]
        [Alias("reset")]
        public async Task<RuntimeResult> SetupAsync()
        {
            RoGuild guild = new RoGuild(Context.Guild.Id);

            await ReplyAsync("Which role would you like to bind as your verification role?\nPlease tag the role for the bot to be able to detect it");
            SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.MentionedRoles.Count > 0)
                return RoWifiResult.FromError("Setup Failed", "Failed to detect a response or there was no mentioned role in the response");
            guild.VerificationRole = response.MentionedRoles.First().Id;

            await ReplyAsync("Which role would you like to bind as your verified role?\nPlease tag the role for the bot to be able to detect it");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.MentionedRoles.Count > 0)
                return RoWifiResult.FromError("Setup Failed", "Failed to detect a response or there was no mentioned role in the response");
            guild.VerifiedRole = response.MentionedRoles.First().Id;

            await Database.AddGuild(guild, await Database.GetGuild(Context.Guild.Id) == null);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Setup Successful").WithDescription("Server has been setup successfully. Use `rankbinds new` or `groupbinds new` to start setting up your binds");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("update-all", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to update all verified users in a server")]
        public async Task<RoWifiResult> UpdateAllAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Update All Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (!guild.Settings.AutoDetection)
                return RoWifiResult.FromError("Update All Failed", "This command may only be used in Premium Servers");
            await ReplyAsync("Updating Verifed Users...");
            
            IGuild server = Context.Guild;
            _ = Task.Run(async () =>
            {
                Dictionary<ulong, IGuildUser> AllDiscordUsers = (await server.GetUsersAsync()).ToDictionary(x => x.Id, x => x);
                IEnumerable<RoUser> VerifiedUsers = await Database.GetUsersAsync(AllDiscordUsers.Keys);
                var BypassRoleId = server.Roles.Where(r => r != null).Where(r => r.Name == "RoWifi Bypass").FirstOrDefault()?.Id ?? 0;
                foreach (RoUser user in VerifiedUsers)
                {
                    try
                    {
                        if (AllDiscordUsers[user.DiscordId].RoleIds.Contains(BypassRoleId)) continue;
                        (List<ulong> AddedRoles, List<ulong> RemovedRoles, string DiscNick) = await user.UpdateAsync(Roblox, server, guild,
                            AllDiscordUsers[user.DiscordId], "Mass Update");

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
                            embed.WithTitle($"Mass Update [{AllDiscordUsers[user.DiscordId].Nickname}]")
                                .AddField("Nickname", DiscNick)
                                .AddField("Added Roles", AddStr)
                                .AddField("Removed Roles", RemoveStr);
                            await Logger.LogServer(server, embed.Build());
                        }
                    }
                    catch (Exception) { }
                }
                await ReplyAsync("All Verified Users have been updated successfully");
            });
            return RoWifiResult.FromSuccess();
        }
    }
}
