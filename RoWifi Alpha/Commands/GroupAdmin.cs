using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using RoWifi_Alpha.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    public class GroupAdmin : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [Command("setup"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to start configuration of the server")]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        [Aliases("reset")]
        public async Task SetupAsync(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            var commands = Context.Client.GetCommandsNext();
            RoGuild guild = new RoGuild(Context.Guild.Id);

            await Context.RespondAsync("Which role would you like to bind as your verification role?\nPlease tag the role for the bot to be able to detect it");
            var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut)
                throw new CommandException("Setup Failed", "Failed to detect a response");
            try
            {
                DiscordRole VerificationRole = (DiscordRole)await commands.ConvertArgument<DiscordRole>(response.Result.Content.Trim(), Context);
                guild.VerificationRole = VerificationRole.Id;
            }
            catch(Exception)
            {
                throw new CommandException("Setup Failed", "Invalid Role Entered");
            }

            await Context.RespondAsync("Which role would you like to bind as your verified role?\nPlease tag the role for the bot to be able to detect it");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut)
                throw new CommandException("Setup Failed", "Failed to detect a response or there was no mentioned role in the response");
            try
            {
                DiscordRole VerifiedRole = (DiscordRole)await commands.ConvertArgument<DiscordRole>(response.Result.Content.Trim(), Context);
                guild.VerifiedRole = VerifiedRole.Id;
            }
            catch(Exception)
            {
                throw new CommandException("Setup Failed", "Invalid Role Entered");
            }

            RoGuild Existing = await Database.GetGuild(Context.Guild.Id);
            if (Existing != null)
                guild.CommandPrefix = Existing.CommandPrefix;
            await Database.AddGuild(guild, Existing == null);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Setup Successful").WithDescription("Server has been setup successfully. Use `rankbinds new` or `groupbinds new` to start setting up your binds");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("serverinfo"), RequireGuild, RequireRoWifiAdmin, Aliases("si")]
        [Description("Command to view server info")]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        public async Task ServerInfoAsync(CommandContext Context)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            string Tier = "Normal";
            if (guild.Settings.Type == GuildType.Alpha) Tier = "Alpha";
            if (guild.Settings.Type == GuildType.Beta) Tier = "Beta";

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Guild Id", $"{Context.Guild.Id}", true)
                .AddField("Member Count", $"{Context.Guild.MemberCount}", true)
                .AddField("Shard Id", $"{Context.Client.ShardId}", true)
                .AddField("Tier", Tier, true)
                .AddField("Prefix", $"{guild.CommandPrefix ?? "!"}", true)
                .AddField("Verification Role", $"<@&{guild.VerificationRole}>", true)
                .AddField("Verified Role", $"<@&{guild.VerifiedRole}>", true)
                .AddField("Rankbinds", $"{guild.RankBinds.Count}", true)
                .AddField("Groupbinds", $"{guild.GroupBinds.Count}", true);
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("update-all"), RequireGuild, RequireRoWifiAdmin, Cooldown(1, 6 * 60 * 60, CooldownBucketType.Guild)]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        [Description("Command to update all verified users in a server")]
        public async Task UpdateAllAsync(CommandContext Context)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Update All Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (!guild.Settings.AutoDetection)
                throw new CommandException("Update All Failed", "This command may only be used in Premium Servers");
            await Context.RespondAsync("Updating Verifed Users...");
            
            DiscordGuild server = Context.Guild;
            _ = Task.Run(async () =>
            {
                Dictionary<ulong, DiscordMember> AllDiscordUsers = (await server.GetAllMembersAsync()).ToDictionary(x => x.Id, x => x);
                IEnumerable<RoUser> VerifiedUsers = await Database.GetUsersAsync(AllDiscordUsers.Keys);
                var BypassRoleId = server.Roles.Values.Where(r => r != null).Where(r => r.Name == "RoWifi Bypass").FirstOrDefault()?.Id ?? 0;
                foreach (RoUser user in VerifiedUsers)
                {
                    try
                    {
                        if (AllDiscordUsers[user.DiscordId].Roles.ToList().Exists(r => r.Id == BypassRoleId)) continue;
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

                            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                            embed.WithTitle($"Mass Update [{AllDiscordUsers[user.DiscordId].Nickname}]")
                                .AddField("Nickname", DiscNick)
                                .AddField("Added Roles", AddStr)
                                .AddField("Removed Roles", RemoveStr);
                            await Logger.LogServer(server, embed.Build());
                        }
                    }
                    catch (Exception) { }
                }
                await Context.RespondAsync("All Verified Users have been updated successfully");
            });
        }
    }
}
