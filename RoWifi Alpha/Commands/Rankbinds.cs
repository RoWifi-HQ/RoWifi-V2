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

namespace RoWifi_Alpha.Commands
{
    [Group("rankbinds")]
    public class Rankbinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
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
        public async Task<RuntimeResult> NewRankbindAsync(int GroupId, int RankId, string Prefix, int Priority, params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            if (guild.RankBinds.Exists(r => r.GroupId == GroupId && r.RbxRankId == RankId))
                return RoWifiResult.FromError("Bind Addition Failed", "A bind with the given Group and Rank already exists. Please use `rankbinds modify` to modify rankbinds");

            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                return RoWifiResult.FromError("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");

            RankBind NewBind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, NewBind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {NewBind.RbxRankId}", $"Prefix: {NewBind.Prefix}\nPriority: {NewBind.Priority}\nRoles: {string.Concat(NewBind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("delete"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> DeleteAsync(int GroupId, int RankId)
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
            return RoWifiResult.FromSuccess();
        }

        [Group("modify")]
        public class ModifyRankbinds : ModuleBase<SocketCommandContext>
        {
            public DatabaseService Database { get; set; }

            [Command("prefix"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyPrefixAsync(int GroupId, int RankId, string Prefix)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Prefix, Prefix);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The prefix was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("priority"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyPriorityAsync(int GroupId, int RankId, int Priority)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Priority, Priority);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-add"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyRolesAddAsync(int GroupId, int RankId, params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-remove"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyRolesRemoveAsync(int GroupId, int RankId, params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }
        }

        [Command("create", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
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

            RankBind NewBind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, NewBind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {NewBind.RbxRankId}", $"Prefix: {NewBind.Prefix}\nPriority: {NewBind.Priority}\nRoles: {string.Concat(NewBind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("multiple"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> MultipleAsync(int GroupId, int MinRank, int MaxRank, string Prefix, int Priority, IRole[] Roles)
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
        public async Task<RuntimeResult> AutoRankbindsAsync(int GroupId, int Priority, IRole[] Roles)
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