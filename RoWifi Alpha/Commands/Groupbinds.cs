using Discord;
using Discord.Commands;
using MongoDB.Driver;
using RoWifi_Alpha.Addons.Interactive;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("groupbinds"), Alias("gb")]
    [Summary("Module to access groupbinds of a server")]
    public class Groupbinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public LoggerService Logger { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to view groupbinds of a server")]
        public async Task<RuntimeResult> GroupCommand() 
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.GroupBinds.Count == 0)
                return RoWifiResult.FromError("Bind Viewing Failed", "There were no groupbinds found associated with this server. Perhaps you meant to use `groupbinds new`");

            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            var GroupBindsList = guild.GroupBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<GroupBind> GBS in GroupBindsList)
            {
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Groupbinds").WithDescription($"Page {Page}");
                foreach (GroupBind bind in GBS)
                    embed.AddField($"Group Id: {bind.GroupId}", $"Roles: { string.Concat(bind.DiscordRoles.Select(r => $"<@&{r}> "))}", true);
                embeds.Add(embed);
                Page++;
            }
            if (Page == 2)
                await ReplyAsync(embed: embeds[0].Build());
            else
                await PagedReplyAsync(embeds);
            return RoWifiResult.FromSuccess();
        }

        [Command("new"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to add a new groupbind")]
        public async Task<RuntimeResult> NewGroupbindAsync([Summary("The Id of the Group to create a bind with")]int GroupId, 
            [Summary("The Roles to bind to the bind")]params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Please ask the server owner to set up this server.");
            if (guild.GroupBinds.Any(g => g.GroupId == GroupId))
                return RoWifiResult.FromError("Bind Addition Failed", $"A bind with {GroupId} as Group Id already exists");
            if (Roles.Length == 0)
                return RoWifiResult.FromError("Bind Addition Failed", "Atleast one role must be mentioned to create a groupbind");

            GroupBind bind = new GroupBind { GroupId = GroupId, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Group: {GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Group Bind Addition", $"Group Id: {bind.GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
            return RoWifiResult.FromSuccess();
        }

        [Command("delete"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to delete a groupbind")]
        public async Task<RuntimeResult> DeleteAsync([Summary("The Id of the Group to create a bind with")]int GroupId)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Please ask the server owner to set up this server.");

            GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
            if (bind == null)
                return RoWifiResult.FromError("Bind Deletion Failed", $"A bind with {GroupId} as Group Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(r => r.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Group Id {GroupId} was successfully deleted");
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Group Bind Deletion", $"Group Id: {bind.GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
            return RoWifiResult.FromSuccess();
        }

        [Group("modify")]
        [Summary("Module to modify groupbinds")]
        public class ModifyGroupbinds : ModuleBase<SocketCommandContext>
        {
            public DatabaseService Database { get; set; }
            public LoggerService Logger { get; set; }

            [Command("roles-add"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to add roles to the groupbind")]
            public async Task<RuntimeResult> AddRolesAsync([Summary("The Id of the Group to create a bind with")]int GroupId, 
                [Summary("The Roles to add to the bind")]params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.GroupBinds.Any(r => r.GroupId == GroupId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.GroupBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Group Bind Modification - Added Roles", $"Group Id: {bind.GroupId}", $"Added Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-remove"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            [Summary("Command to remove roles from the bind")]
            public async Task<RuntimeResult> RemoveRolesAsync([Summary("The Id of the Group to create a bind with")] int GroupId, 
                [Summary("The Roles to remove from the bind")]params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Group and Rank does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.GroupBinds.Any(r => r.GroupId == GroupId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.GroupBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Group Bind Modification - Removed Roles", $"Group Id: {bind.GroupId}", $"Removed Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
                return RoWifiResult.FromSuccess();
            }
        }
    }
}
