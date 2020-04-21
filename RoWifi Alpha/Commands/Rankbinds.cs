using Discord;
using RoWifi_Alpha.Addons.Interactive;
using Discord.Commands;
using RoWifi_Alpha.Preconditions;
using System.Threading.Tasks;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using Discord.WebSocket;
using System;
using RoWifi_Alpha.Services;

namespace RoWifi_Alpha.Commands
{
    [Group("rankbinds")]
    [Alias("rb")]
    [Summary("Module to access rankbinds of a server")]
    public class Rankbinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to view rankbinds of a server")]
        public async Task<RuntimeResult> ViewRankbindsAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.RankBinds.Count == 0)
                return RoWifiResult.FromError("Bind Viewing Failed", "There were no rankbinds found associated with this server. Perhaps you meant to use `rankbinds new`");
            var UniqueGroups = guild.RankBinds.Select(r => r.GroupId).Distinct();
            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            foreach (int Group in UniqueGroups)
            {
                int Page = 1;
                var RankBinds = guild.RankBinds.Where(r => r.GroupId == Group).OrderBy(r => r.RbxRankId).Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
                foreach (List<RankBind> RBS in RankBinds)
                {
                    EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                    embed.WithTitle("Rankbinds").WithDescription($"Group: {Group} - Page {Page}");
                    foreach (RankBind Bind in RBS)
                    {
                        embed.AddField($"Rank: {Bind.RbxRankId}", $"Prefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
                    }
                    embeds.Add(embed);
                    Page++;
                }
            }
            await PagedReplyAsync(embeds);
            return RoWifiResult.FromSuccess();
        }

        [Command("new"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to add a new rankbind")]
        public async Task<RuntimeResult> NewRankbindAsync([Summary("Id of the Group to bind")]int GroupId, 
            [Summary("The Rank Id of the Group to bind [0-255]")]int RankId, 
            [Summary("The prefix to be used before the nickname")]string Prefix, 
            [Summary("The deciding factor for the prefix conflict between two roles")]int Priority, 
            [Summary("The Discord Roles to be added to the bind")]params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            if (guild.RankBinds.Exists(r => r.GroupId == GroupId && r.RbxRankId == RankId))
                return RoWifiResult.FromError("Bind Addition Failed", "A bind with the given Group and Rank already exists. Please use `rankbinds modify` to modify rankbinds");

            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                return RoWifiResult.FromError("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");

            RankBind bind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Addition - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
            return RoWifiResult.FromSuccess();
        }

        [Command("delete"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to delete a rankbind")]
        public async Task<RuntimeResult> DeleteAsync([Summary("Id of the Group")]int GroupId, 
            [Summary("The Rank Id of the Group to delete")]int RankId)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

            RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
            if (bind == null)
                return RoWifiResult.FromError("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Group Id {GroupId} & Rank Id {RankId} was successfully deleted");
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Deletion - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
            return RoWifiResult.FromSuccess();
        }

        [Group("modify")]
        [Summary("Module to modify rankbinds of the server")]
        public class ModifyRankbinds : ModuleBase<SocketCommandContext>
        {
            public DatabaseService Database { get; set; }
            public LoggerService Logger { get; set; }

            [Command("prefix"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to modify the prefix of a rankbind")]
            public async Task<RuntimeResult> ModifyPrefixAsync([Summary("The Id of the Group")]int GroupId, 
                [Summary("The Rank Id of the group to modify")]int RankId, 
                [Summary("The new prefix of the bind")]string Prefix)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Prefix, Prefix);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The prefix was successfully modified");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Prefix", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nOld Prefix: {bind.Prefix}\nNew Prefix: {Prefix}");
                return RoWifiResult.FromSuccess();
            }

            [Command("priority"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to modify the priority of a rankbind")]
            public async Task<RuntimeResult> ModifyPriorityAsync([Summary("The Id of the Group")]int GroupId,
                [Summary("The Rank Id of the group to modify")]int RankId, 
                [Summary("The new priority of the bind")]int Priority)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Priority, Priority);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Priority", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nOld Priority: {bind.Priority}\nNew Priority: {Priority}");
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-add"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to add roles to a bind")]
            public async Task<RuntimeResult> ModifyRolesAddAsync([Summary("The Id of the Group")]int GroupId,
                [Summary("The Rank Id of the group to modify")]int RankId, 
                [Summary("The roles to be added to the bind")]params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Added Roles", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nAdded Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-remove"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to remove roles from a rankbinds")]
            public async Task<RuntimeResult> ModifyRolesRemoveAsync([Summary("The Id of the Group")]int GroupId,
                [Summary("The Rank Id of the group to modify")]int RankId, 
                [Summary("The roles to be removed from the group")]params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Removed Roles", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nRemoved Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
                return RoWifiResult.FromSuccess();
            }
        }

        [Command("create", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Interactive Command to create a bind")]
        public async Task<RuntimeResult> CreateRankbindAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            await ReplyAsync("Enter Group Id to bind\nSay `cancel` if you wish to cancel this command");
            SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled. Try again");
            bool Success = int.TryParse(response.Content, out int GroupId);
            if (!Success)
                return RoWifiResult.FromError("Bind Addition Failed", "Group Id was not found to be a valid integer");

            await ReplyAsync("Enter Rank Id of the Group to bind\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled. Try again");
            Success = int.TryParse(response.Content, out int RankId);
            if (!Success || RankId > 255 || RankId < 0)
                return RoWifiResult.FromError("Bind Addition Failed", "Rank Id was not found to be a valid integer");
            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                return RoWifiResult.FromError("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");

            await ReplyAsync("Enter Prefix to use in the nickname. Enter `N/A` if you do not wish to set a prefix.\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled. Try again");
            string Prefix = response.Content;

            await ReplyAsync("Enter the priority of this bind\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled. Try again");
            Success = int.TryParse(response.Content, out int Priority);
            if (!Success)
                return RoWifiResult.FromError("Bind Addition Failed", "Priority was not found to be a valid integer");

            await ReplyAsync("Ping the Discord Roles you wish to bind to this role. Enter `N/A` if you wish to not bind any role\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled. Try again");
            var Roles = response.MentionedRoles.ToArray();

            RankBind bind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Addition - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
            return RoWifiResult.FromSuccess();
        }

        [Command("multiple"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to add a range of rankbinds")]
        public async Task<RuntimeResult> MultipleAsync([Summary("Id of the Roblox Group")]int GroupId, 
            [Summary("Rank Id from which to create binds")]int MinRank, 
            [Summary("Rank Id till which to create binds")]int MaxRank, 
            [Summary("The prefix to be used before the nickname")]string Prefix, 
            [Summary("The deciding factor for the prefix conflict between two roles")]int Priority, 
            [Summary("The Roles to be added to the bind")]params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            
            List<JToken> TokensToAdd = await Roblox.GetGroupRolesInRange(GroupId, MinRank, MaxRank);
            if (TokensToAdd.Count == 0)
                return RoWifiResult.FromError("Bind Addition Failed", "There are no ranks between the given rank ids. Pleasr try again");

            foreach (JToken item in TokensToAdd)
            {
                RankBind bind = guild.RankBinds.Where(r => r.RbxGrpRoleId == (int)item["id"]).FirstOrDefault();
                if (bind == null)
                    guild.RankBinds.Add(new RankBind { GroupId = GroupId, RbxRankId = (int)item["rank"], RbxGrpRoleId = (int)item["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() });
                else
                {
                    if (bind.Prefix != Prefix)
                        bind.Prefix = Prefix;
                    if (bind.Priority != Priority)
                        bind.Priority = Priority;
                    foreach (IRole role in Roles)
                        if (!bind.DiscordRoles.Contains(role.Id))
                            bind.DiscordRoles.Append(role.Id);
                    int Index = guild.RankBinds.IndexOf(bind);
                    guild.RankBinds[Index] = bind;
                }
            }
            await Database.AddGuild(guild, false);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Binds Addition Successful").WithDescription($"The new binds were successfully added");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("auto"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to auto add rankbinds from the Roblox Group")]
        public async Task<RuntimeResult> AutoRankbindsAsync([Summary("Id of the Roblox Group")]int GroupId, 
            [Summary("The deciding factor for the prefix conflict between two roles")]int Priority, 
            [Summary("The Roles to be added to the binds")]params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            List<JToken> TokensToAdd = await Roblox.GetGroupRolesInRange(GroupId, 1, 255);
            if (TokensToAdd.Count == 0)
                return RoWifiResult.FromError("Bind Addition Failed", "There are no ranks between the given rank ids. Pleasr try again");

            foreach (JToken item in TokensToAdd)
            {
                RankBind bind = guild.RankBinds.Where(r => r.RbxGrpRoleId == (int)item["id"]).FirstOrDefault();
                string Prefix = Regex.Match((string)item["name"], @"\[(.*?)\]").Groups[1].Value;
                Prefix = Prefix.Length == 0 ? "N/A" : $"[{Prefix}]";
                if (bind == null)
                    guild.RankBinds.Add(new RankBind { GroupId = GroupId, RbxRankId = (int)item["rank"], RbxGrpRoleId = (int)item["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() });
                else
                {
                    if (bind.Prefix != Prefix)
                        bind.Prefix = Prefix;
                    if (bind.Priority != Priority)
                        bind.Priority = Priority;
                    foreach (IRole role in Roles)
                        if (!bind.DiscordRoles.Contains(role.Id))
                            bind.DiscordRoles.Append(role.Id);
                    int Index = guild.RankBinds.IndexOf(bind);
                    guild.RankBinds[Index] = bind;
                }
            }
            await Database.AddGuild(guild, false);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Binds Addition Successful").WithDescription($"The new binds were successfully added");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}