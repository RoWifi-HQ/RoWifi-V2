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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("assetbinds"), Aliases("ab")]
    [RequireBotPermissions(Permissions.EmbedLinks | Permissions.AddReactions | Permissions.ManageMessages), RequireGuild, RequireRoWifiAdmin]
    [Description("Module to access assetbinds of a server")]
    public class Assetbinds : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand]
        [Description("Command to view assetbinds of a server")]
        public async Task GroupCommand(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.AssetBinds == null || guild.AssetBinds.Count == 0)
                throw new CommandException("Bind Viewing Failed", "There were no assetbinds found associated with this server. Perhaps you meant to use `assetbinds new`");

            List<Page> pages = new List<Page>();
            var AssetBindsList = guild.AssetBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<AssetBind> ABS in AssetBindsList)
            {
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Asset Binds").WithDescription($"Page {Page}");
                foreach (AssetBind bind in ABS)
                    embed.AddField($"Asset Id: {bind.Id}", $"Type: {bind.Type}\nRoles: { string.Concat(bind.DiscordRoles.Select(r => $"<@&{r}> "))}", true);
                pages.Add(new Page(embed: embed));
                Page++;
            }
            if (Page == 2)
                await Context.RespondAsync(embed: pages[0].Embed);
            else
                await interactivity.SendPaginatedMessageAsync(Context.Channel, Context.User, pages);
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to add a new assetbind")]
        public async Task NewAssetBind(CommandContext Context, [Description("The Type of the Asset to bind. Options: `Asset` `Badge` `Gamepass`")] string Type,
            [Description("The Id of the Asset to bind")] ulong AssetId,
            [Description("The Discord Role to use in this bind")] params DiscordRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Please ask the server owner to set up this server.");
            if (guild.AssetBinds != null && guild.AssetBinds.Any(g => g.Id == AssetId))
                throw new CommandException("Bind Addition Failed", $"A bind with {AssetId} as Asset Id already exists");
            if (Roles.Length == 0)
                throw new CommandException("Bind Addition Failed", "Atleast one role must be mentioned to create a bind");
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");
            bool Success = Enum.TryParse(Type, true, out AssetType result);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Invalid Option Selected");
            AssetBind bind = new AssetBind { Id = AssetId, Type = result, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update;
            if (guild.AssetBinds == null)
                update = Builders<RoGuild>.Update.Set(g => g.AssetBinds, new List<AssetBind>() { bind });
            else
                update = Builders<RoGuild>.Update.Push(g => g.AssetBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Asset: {AssetId}", $"Type: {result}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Asset Bind Addition", $"Asset Id: {bind.Id}", $"Type: {result}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("delete"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to delete a assetbind"), Aliases("remove")]
        public async Task DeleteAsync(CommandContext Context, [Description("The Asset Id whose bind to delete")] ulong Id)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Please ask the server owner to set up this server.");

            AssetBind bind = guild.AssetBinds?.Where(a => a.Id == Id).FirstOrDefault();
            if (bind == null)
                throw new CommandException("Bind Deletion Failed", $"A bind with {Id} as Group Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.AssetBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Asset Id {Id} was successfully deleted");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Asset Bind Deletion", $"Asset Id: {bind.Id}", $"Type: {bind.Type}\nRoles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Group("modify"), RequireBotPermissions(Permissions.EmbedLinks)]
        [Description("Module to modify assetbinds")]
        public class ModifyAssetbinds : BaseCommandModule
        {
            public DatabaseService Database { get; set; }
            public LoggerService Logger { get; set; }

            [GroupCommand]
            public async Task GroupCommand(CommandContext Context)
            {
                var commands = Context.CommandsNext;
                var content = "help " + Context.Command.QualifiedName;
                var cmd = commands.FindCommand(content, out var args);
                var ctx = commands.CreateFakeContext(Context.User, Context.Channel, content, Context.Prefix, cmd, args);
                await commands.ExecuteCommandAsync(ctx);
            }

            [Command("roles-add"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to add roles to an assetbind")]
            public async Task AddRolesAsync(CommandContext Context, [Description("The Asset Id to modify")] ulong AssetId,
                [Description("The Discord Roles to add to the bind")] params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                AssetBind bind = guild.AssetBinds?.Where(a => a.Id == AssetId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group does not exist");
                if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                    throw new CommandException("Bind Modification Failed", "You cannot use the `@everyone` role in a bind");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.AssetBinds.Any(a => a.Id == AssetId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.AddToSetEach(r => r.AssetBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Asset Bind Modification - Added Roles", $"Group Id: {bind.Id}", $"Type: {bind.Type}Added Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }

            [Command("roles-remove"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to remove roles from an assetbind")]
            public async Task RemoveRolesAsync(CommandContext Context, [Description("The Asset Id to modify")] ulong AssetId,
                [Description("The Discord Roles to add to the bind")] params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

                AssetBind bind = guild.AssetBinds?.Where(a => a.Id == AssetId).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Group does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.AssetBinds.Any(a => a.Id == AssetId));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.AssetBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The roles were successfully removed");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Asset Bind Modification - Removed Roles", $"Group Id: {bind.Id}", $"Type: {bind.Type}Removed Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }
        }
    }
}
