using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using MongoDB.Driver;
using RoWifi_Alpha.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("groupbinds"), Aliases("gb")]
    [RequireBotPermissions(Permissions.EmbedLinks | Permissions.AddReactions), RequireGuild, RequireRoWifiAdmin]
    [Description("Module to access groupbinds of a server")]
    public class Groupbinds : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand]
        [Description("Command to view groupbinds of a server")]
        public async Task GroupCommand(CommandContext Context) 
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.GroupBinds.Count == 0)
                throw new CommandException("Bind Viewing Failed", "There were no groupbinds found associated with this server. Perhaps you meant to use `groupbinds new`");

            List<Page> pages = new List<Page>();
            var GroupBindsList = guild.GroupBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<GroupBind> GBS in GroupBindsList)
            {
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Groupbinds").WithDescription($"Page {Page}");
                foreach (GroupBind bind in GBS)
                    embed.AddField($"Group Id: {bind.GroupId}", $"Roles: { string.Concat(bind.DiscordRoles.Select(r => $"<@&{r}> "))}", true);
                pages.Add(new Page(embed: embed));
                Page++;
            }
            if (Page == 2)
                await Context.RespondAsync(embed: pages[0].Embed);
            else
                await interactivity.SendPaginatedMessageAsync(Context.Channel, Context.User, pages);
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to add a new groupbind")]
        public async Task NewGroupbindAsync(CommandContext Context, [Description("The Id of the Group to create a bind with")]int GroupId, 
            [Description("The Roles to bind to the bind")]params DiscordRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Please ask the server owner to set up this server.");
            if (guild.GroupBinds.Any(g => g.GroupId == GroupId))
                throw new CommandException("Bind Addition Failed", $"A bind with {GroupId} as Group Id already exists");
            if (Roles.Length == 0)
                throw new CommandException("Bind Addition Failed", "Atleast one role must be mentioned to create a groupbind");
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");

            GroupBind bind = new GroupBind { GroupId = GroupId, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Group: {GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Group Bind Addition", $"Group Id: {bind.GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("delete"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to delete a groupbind"), Aliases("remove")]
        public async Task DeleteAsync(CommandContext Context, [Description("The Id of the Group to create a bind with")]int GroupId)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Please ask the server owner to set up this server.");

            GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
            if (bind == null)
                throw new CommandException("Bind Deletion Failed", $"A bind with {GroupId} as Group Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(r => r.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Group Id {GroupId} was successfully deleted");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Group Bind Deletion", $"Group Id: {bind.GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Group("modify")]
        [Description("Module to modify groupbinds")]
        public class ModifyGroupbinds : BaseCommandModule
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

            [Command("roles-add"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to add roles to the groupbind")]
            public async Task AddRolesAsync(CommandContext Context, [Description("The Id of the Group to create a bind with")]int GroupId, 
                [Description("The Roles to add to the bind")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group does not exist");
                if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                    throw new CommandException("Bind Modification Failed", "You cannot use the `@everyone` role in a bind");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.GroupBinds.Any(r => r.GroupId == GroupId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.GroupBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Group Bind Modification - Added Roles", $"Group Id: {bind.GroupId}", $"Added Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }

            [Command("roles-remove"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to remove roles from the bind")]
            public async Task RemoveRolesAsync(CommandContext Context, [Description("The Id of the Group to create a bind with")] int GroupId, 
                [Description("The Roles to remove from the bind")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.GroupBinds.Any(r => r.GroupId == GroupId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.GroupBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The given roles were successfully removed");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Group Bind Modification - Removed Roles", $"Group Id: {bind.GroupId}", $"Removed Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }
        }
    }
}
