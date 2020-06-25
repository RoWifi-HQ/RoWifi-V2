using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using RoWifi_Alpha.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("rankbinds")]
    [Aliases("rb")]
    [RequireBotPermissions(Permissions.EmbedLinks | Permissions.AddReactions), RequireGuild, RequireRoWifiAdmin]
    [Description("Module to access rankbinds of a server")]
    public class Rankbinds : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand]
        [Description("Command to view rankbinds of a server")]
        public async Task GroupCommand(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.RankBinds.Count == 0)
                throw new CommandException("Bind Viewing Failed", "There were no rankbinds found associated with this server. Perhaps you meant to use `rankbinds new`");
            var UniqueGroups = guild.RankBinds.Select(r => r.GroupId).Distinct();
            List<Page> pages = new List<Page>();
            int Page = 1;
            foreach (int Group in UniqueGroups)
            {
                var RankBinds = guild.RankBinds.Where(r => r.GroupId == Group).OrderBy(r => r.RbxRankId).Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
                foreach (List<RankBind> RBS in RankBinds)
                {
                    DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                    embed.WithTitle("Rankbinds").WithDescription($"Group: {Group} - Page {Page}");
                    foreach (RankBind Bind in RBS)
                    {
                        embed.AddField($"Rank: {Bind.RbxRankId}", $"Prefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
                    }
                    pages.Add(new Page(embed: embed));
                    Page++;
                }
            }
            if (Page == 2)
                await Context.RespondAsync(embed: pages[0].Embed);
            else
                await interactivity.SendPaginatedMessageAsync(Context.Channel, Context.User, pages);
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin, Priority(4)]
        [Description("Command to add a new rankbind")]
        public async Task NewRankbindAsync(CommandContext Context, [Description("Id of the Group to bind")]int GroupId, 
            [Description("The Rank Id of the Group to bind [0-255]")]int RankId, 
            [Description("The prefix to be used before the nickname")]string Prefix, 
            [Description("The deciding factor for the prefix conflict between two roles")]int Priority, 
            [Description("The Discord Roles to be added to the bind")]params DiscordRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            if (guild.RankBinds.Exists(r => r.GroupId == GroupId && r.RbxRankId == RankId))
                throw new CommandException("Bind Addition Failed", "A bind with the given Group and Rank already exists. Please use `rankbinds modify` to modify rankbinds");
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");

            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                throw new CommandException("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");
            if (Prefix.Equals("auto"))
            {
                Prefix = Regex.Match((string)RankInfo["name"], @"\[(.*?)\]").Groups[1].Value;
                Prefix = Prefix.Length == 0 ? "N/A" : $"[{Prefix}]";
            }

            RankBind bind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Addition - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("delete"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to delete a rankbind"), Aliases("remove")]
        public async Task DeleteAsync(CommandContext Context, [Description("Id of the Group")]int GroupId, 
            [Description("The Rank Id of the Group to delete")]int RankId)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");

            RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
            if (bind == null)
                throw new CommandException("Bind Deletion Failed", "A bind with the given Group and Rank does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Group Id {GroupId} & Rank Id {RankId} was successfully deleted");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Deletion - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Group("modify")]
        [Description("Module to modify rankbinds of the server")]
        public class ModifyRankbinds : BaseCommandModule
        {
            public DatabaseService Database { get; set; }
            public LoggerService Logger { get; set; }

            [GroupCommand, RequireGuild]
            public async Task GroupCommand(CommandContext Context)
            {
                var commands = Context.CommandsNext;
                var content = "help " + Context.Command.QualifiedName;
                var cmd = commands.FindCommand(content, out var args);
                var ctx = commands.CreateFakeContext(Context.User, Context.Channel, content, Context.Prefix, cmd, args);
                await commands.ExecuteCommandAsync(ctx);
            }

            [Command("prefix"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to modify the prefix of a rankbind")]
            public async Task ModifyPrefixAsync(CommandContext Context, [Description("The Id of the Group")]int GroupId, 
                [Description("The Rank Id of the group to modify")]int RankId, 
                [Description("The new prefix of the bind")]string Prefix)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Prefix, Prefix);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The prefix was successfully modified");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Prefix", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nOld Prefix: {bind.Prefix}\nNew Prefix: {Prefix}");
            }

            [Command("priority"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to modify the priority of a rankbind")]
            public async Task ModifyPriorityAsync(CommandContext Context, [Description("The Id of the Group")]int GroupId,
                [Description("The Rank Id of the group to modify")]int RankId, 
                [Description("The new priority of the bind")]int Priority)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.RankBinds[-1].Priority, Priority);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Priority", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nOld Priority: {bind.Priority}\nNew Priority: {Priority}");
            }

            [Command("roles-add"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to add roles to a bind")]
            public async Task ModifyRolesAddAsync(CommandContext Context, [Description("The Id of the Group")]int GroupId,
                [Description("The Rank Id of the group to modify")]int RankId, 
                [Description("The roles to be added to the bind")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group and Rank does not exist");
                if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                    throw new CommandException("Bind Modification Failed", "You cannot use the `@everyone` role in a bind");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Added Roles", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nAdded Roles: {string.Concat(Roles.Select(r => $" <@&{r.Id}> "))}");
            }

            [Command("roles-remove"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to remove roles from a rankbinds")]
            public async Task ModifyRolesRemoveAsync(CommandContext Context, [Description("The Id of the Group")]int GroupId,
                [Description("The Rank Id of the group to modify")]int RankId, 
                [Description("The roles to be removed from the group")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                RankBind bind = guild.RankBinds.Where(r => r.GroupId == GroupId && r.RbxRankId == RankId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.RankBinds.Any(r => r.GroupId == GroupId && r.RbxRankId == RankId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.RankBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The given roles were successfully removed");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Rank Bind Modification - Removed Roles", $"Group Id: {bind.GroupId}", $"Rank Id: {bind.RbxRankId}\nRemoved Roles: {string.Concat(Roles.Select(r => $" <@&{r.Id}> "))}");
            }
        }

        [Command("create"), RequireGuild, RequireRoWifiAdmin]
        [Description("Interactive Command to create a bind")]
        public async Task CreateRankbindAsync(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            await Context.RespondAsync("Enter Group Id to bind\nSay `cancel` if you wish to cancel this command");
            var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled. Try again");
            bool Success = int.TryParse(response.Result.Content, out int GroupId);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Group Id was not found to be a valid integer");

            await Context.RespondAsync("Enter Rank Id of the Group to bind\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled. Try again");
            Success = int.TryParse(response.Result.Content, out int RankId);
            if (!Success || RankId > 255 || RankId < 0)
                throw new CommandException("Bind Addition Failed", "Rank Id was not found to be a valid integer");
            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                throw new CommandException("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");

            await Context.RespondAsync("Enter Prefix to use in the nickname. Enter `N/A` if you do not wish to set a prefix.\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled. Try again");
            string Prefix = response.Result.Content;

            await Context.RespondAsync("Enter the priority of this bind\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled. Try again");
            Success = int.TryParse(response.Result.Content, out int Priority);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Priority was not found to be a valid integer");

            await Context.RespondAsync("Ping the Discord Roles you wish to bind to this role. Enter `N/A` if you wish to not bind any role\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled. Try again");
            var Roles = response.Result.MentionedRoles.ToArray();
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");

            RankBind bind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Addition - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin, Priority(3)]
        [RequireBotPermissions(Permissions.ManageRoles)]
        public async Task NewRankbindWithAuto(CommandContext Context, int GroupId, int RankId, string Prefix, int Priority, string Roles)
        {
            if (!Roles.Equals("auto"))
                throw new CommandException("Bind Addition Failed", "Invalid choice for Roles. Please try again");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            if (guild.RankBinds.Exists(r => r.GroupId == GroupId && r.RbxRankId == RankId))
                throw new CommandException("Bind Addition Failed", "A bind with the given Group and Rank already exists. Please use `rankbinds modify` to modify rankbinds");

            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                throw new CommandException("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");
            DiscordRole Role = Context.Guild.Roles.Values.Where(r => r.Name == RankInfo["name"].ToString()).FirstOrDefault();
            if (Role == null) 
                Role = await Context.Guild.CreateRoleAsync(RankInfo["name"].ToString(), mentionable: false);
            if (Prefix.Equals("auto"))
            {
                Prefix = Regex.Match((string)RankInfo["name"], @"\[(.*?)\]").Groups[1].Value;
                Prefix = Prefix.Length == 0 ? "N/A" : $"[{Prefix}]";
            }

            RankBind bind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = new ulong[] {Role.Id} };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, $"Rank Bind Addition - Group {bind.GroupId}", $"Rank Id: {bind.RbxRankId}", $"Prefix: {bind.Prefix}\nPriority: {bind.Priority}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin, Priority(2)]
        public async Task MultipleRankbinds(CommandContext Context, int GroupId, string RankId, string Prefix, int Priority, params DiscordRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");

            var Ranks = RankId.Split('-');
            if (Ranks.Length != 2)
                throw new CommandException("Bind Addition Failed", "Invalid Rank Id Range");
            bool Success = int.TryParse(Ranks[0], out int MinRank);
            Success = int.TryParse(Ranks[1], out int MaxRank);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Unable to find Rank Id Range");

            List<JToken> TokensToAdd = await Roblox.GetGroupRolesInRange(GroupId, MinRank, MaxRank);
            foreach(JToken RankInfo in TokensToAdd)
            {
                RankBind bind = guild.RankBinds.Where(r => r.RbxGrpRoleId == (int)RankInfo["id"]).FirstOrDefault();
                string PrefixToAdd = Prefix;
                if (Prefix.Equals("auto"))
                {
                    PrefixToAdd = Regex.Match((string)RankInfo["name"], @"\[(.*?)\]").Groups[1].Value;
                    PrefixToAdd = PrefixToAdd.Length == 0 ? "N/A" : $"[{PrefixToAdd}]";
                }
                if (bind == null)
                    guild.RankBinds.Add(new RankBind { GroupId = GroupId, RbxRankId = (int)RankInfo["rank"], RbxGrpRoleId = (int)RankInfo["id"], Prefix = PrefixToAdd, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() });
                else
                {
                    if (bind.Prefix != PrefixToAdd)
                        bind.Prefix = PrefixToAdd;
                    if (bind.Priority != Priority)
                        bind.Priority = Priority;
                    foreach (DiscordRole role in Roles)
                        if (!bind.DiscordRoles.Contains(role.Id))
                            bind.DiscordRoles.Append(role.Id);
                    int Index = guild.RankBinds.IndexOf(bind);
                    guild.RankBinds[Index] = bind;
                }
            }
            await Database.AddGuild(guild, false);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Binds Addition Successful").WithDescription($"The new binds were successfully added");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin, Priority(1)]
        [RequireBotPermissions(Permissions.ManageRoles)]
        public async Task MultipleRankbindsWithAuto(CommandContext Context, int GroupId, string RankId, string Prefix, int Priority, string Roles)
        {
            if (!Roles.Equals("auto"))
                throw new CommandException("Bind Addition Failed", "Invalid choice for Roles. Please try again");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            var Ranks = RankId.Split('-');
            if (Ranks.Length != 2)
                throw new CommandException("Bind Addition Failed", "Invalid Rank Id Range");
            bool Success = int.TryParse(Ranks[0], out int MinRank);
            Success = int.TryParse(Ranks[1], out int MaxRank);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Unable to find Rank Id Range");

            List<JToken> TokensToAdd = await Roblox.GetGroupRolesInRange(GroupId, MinRank, MaxRank);
            foreach (JToken RankInfo in TokensToAdd)
            {
                RankBind bind = guild.RankBinds.Where(r => r.RbxGrpRoleId == (int)RankInfo["id"]).FirstOrDefault();
                string PrefixToAdd = Prefix;
                if (Prefix.Equals("auto"))
                {
                    PrefixToAdd = Regex.Match((string)RankInfo["name"], @"\[(.*?)\]").Groups[1].Value;
                    PrefixToAdd = PrefixToAdd.Length == 0 ? "N/A" : $"[{PrefixToAdd}]";
                }
                string RoleName = RankInfo["name"].ToString().Trim();
                DiscordRole RoleToAdd = Context.Guild.Roles.Values.Where(r => r.Name.Equals(RoleName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                Console.WriteLine(RankInfo["name"]);
                Console.WriteLine(RoleToAdd?.Id);
                if (RoleToAdd == null)
                    RoleToAdd = await Context.Guild.CreateRoleAsync(RoleName, mentionable: false);
                if (bind == null)
                    guild.RankBinds.Add(new RankBind { GroupId = GroupId, RbxRankId = (int)RankInfo["rank"], RbxGrpRoleId = (int)RankInfo["id"], Prefix = PrefixToAdd, Priority = Priority, DiscordRoles = new ulong[] { RoleToAdd.Id } });
                else
                {
                    if (bind.Prefix != PrefixToAdd)
                        bind.Prefix = PrefixToAdd;
                    if (bind.Priority != Priority)
                        bind.Priority = Priority;
                    if (!bind.DiscordRoles.Contains(RoleToAdd.Id))
                        bind.DiscordRoles.Append(RoleToAdd.Id);
                    int Index = guild.RankBinds.IndexOf(bind);
                    guild.RankBinds[Index] = bind;
                }
            }
            await Database.AddGuild(guild, false);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Binds Addition Successful").WithDescription($"The new binds were successfully added");
            await Context.RespondAsync(embed: embed.Build());
        }
    }
}