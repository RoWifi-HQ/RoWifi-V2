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
    [Group("custombinds"), Aliases("cb")]
    [RequireBotPermissions(Permissions.EmbedLinks | Permissions.AddReactions), RequireGuild, RequireRoWifiAdmin]
    [Description("Module to access custombinds of a server")]
    public class Custombinds : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand]
        [Description("Command to view the custombinds of a server")]
        public async Task GroupCommand(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.CustomBinds == null || guild.CustomBinds.Count == 0)
                throw new CommandException("Bind Viewing Failed", "There were no custombinds found associated with this server. Perhaps you meant to use `custombinds new`");
            
            List<Page> pages = new List<Page>();
            var CustomBindsList = guild.CustomBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<CustomBind> CBS in CustomBindsList)
            {
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Custombinds").WithDescription($"Page - {Page}");
                foreach (CustomBind Bind in CBS)
                    embed.AddField($"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
                pages.Add(new Page(embed: embed));
                Page++;
            }
            if (Page == 2)
                await Context.RespondAsync(embed: pages[0].Embed);
            else
                await interactivity.SendPaginatedMessageAsync(Context.Channel, Context.User, pages);
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to add a new custombind to the server")]
        public async Task NewCustombindAsync(CommandContext Context, [RemainingText, Description("The custom code to define the bind")]string Code)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user == null)
                throw new CommandException("Bind Addition Failed", "You must be verified to use this feature");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            try
            {
                RoCommand cmd = new RoCommand(Code);
                Dictionary<int, int> Ranks = await Roblox.GetUserRoles(user.RobloxId);
                string Username = await Roblox.GetUsernameFromId(user.RobloxId);
                RoCommandUser CommandUser = new RoCommandUser(user, Context.Member, Ranks, Username);
                cmd.Evaluate(CommandUser);
            }
            catch (Exception e)
            {
                throw new CommandException("Bind Addition Failed", $"Command Error: {e.Message}");
            }

            await Context.RespondAsync("Enter Prefix to use in the nickname. Enter `N/A` if you do not wish to set a prefix.\nSay `cancel` if you wish to cancel this command");
            var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled");
            string Prefix = response.Result.Content;

            await Context.RespondAsync("Enter the priority of this bind\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled");
            bool Success = int.TryParse(response.Result.Content, out int Priority);
            if (!Success)
                throw new CommandException("Bind Addition Failed", "Priority was not found to be a valid number");

            await Context.RespondAsync("Ping the Discord Roles you wish to bind to this role. Enter `N/A` if you wish to not bind any role\nSay `cancel` if you wish to cancel this command");
            response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Bind Addition Failed", "Command has been cancelled");
            DiscordRole[] Roles = response.Result.MentionedRoles.ToArray();
            if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                throw new CommandException("Bind Addition Failed", "You cannot use the `@everyone` role in a bind");

            int Id = 1;
            if (guild.CustomBinds != null && guild.CustomBinds.Count > 0)
                Id = guild.CustomBinds.OrderBy(c => c.Id).Last().Id + 1;
            CustomBind Bind = new CustomBind(Id, Code, Prefix, Priority, Roles.Select(r => r.Id).ToArray());
            UpdateDefinition<RoGuild> update;
            if (guild.CustomBinds == null)
                update = Builders<RoGuild>.Update.Set(g => g.CustomBinds, new List<CustomBind>() { Bind });
            else
                update = Builders<RoGuild>.Update.Push(g => g.CustomBinds, Bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Addition", $"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Command("delete"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to delete an existing custombind"), Aliases("remove")]
        public async Task DeleteCustombindAsync(CommandContext Context, [Description("The Id of the assigned custombind")]int Id)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.CustomBinds == null || guild.CustomBinds.Count == 0)
                throw new CommandException("Bind Deletion Failed", "This server has no custombinds to delete");

            CustomBind Bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
            if (Bind == null)
                throw new CommandException("Bind Deletion Failed", $"A bind with {Id} as Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.CustomBinds, Bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Id {Id} was successfully deleted");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Deletion", $"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}");
        }

        [Group("modify"), Aliases("m")]
        [Description("Module to modify existing custombinds")]
        public class ModifyCustombinds : BaseCommandModule
        {
            public DatabaseService Database { get; set; }
            public RobloxService Roblox { get; set; }
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
            [Description("Command to modify the prefix of a custombind")]
            public async Task ModifyPrefixAsync(CommandContext Context, [Description("The Id of the assigned custombind")]int Id, 
                [Description("The new prefix to be assigned")]string Prefix)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                CustomBind Bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (Bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Prefix, Prefix);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The prefix was successfully modified");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Modification - Prefix", $"Bind Id: {Bind.Id}", $"Old Prefix: {Bind.Prefix}\nNew Prefix: {Prefix}");;
            }

            [Command("priority"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to modify priority of a custombind")]
            public async Task ModifyPriorityAsync(CommandContext Context, [Description("The Id of the assigned custombind")] int Id, 
                [Description("The new priority to be assigned")]int Priority)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                CustomBind Bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (Bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Priority, Priority);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Modification - Priority", $"Bind Id: {Bind.Id}", $"Old Priority: {Bind.Priority}\nNew Prefix: {Priority}");
            }

            [Command("code"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to modify the code of a custombind")]
            public async Task ModifyCodeAsync(CommandContext Context, [Description("The Id of the assigned custombind")] int Id, 
                [RemainingText, Description("The new custom code to be assigned")] string Code)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Id does not exist");
                RoUser user = await Database.GetUserAsync(Context.User.Id);
                if (user == null)
                    throw new CommandException("Bind Modification Failed", "You must be verified to use this feature");

                try
                {
                    RoCommand cmd = new RoCommand(Code);
                    Dictionary<int, int> Ranks = await Roblox.GetUserRoles(user.RobloxId);
                    string Username = await Roblox.GetUsernameFromId(user.RobloxId);
                    RoCommandUser CommandUser = new RoCommandUser(user, Context.Member, Ranks, Username);
                    cmd.Evaluate(CommandUser);
                }
                catch (Exception e)
                {
                    throw new CommandException("Bind Modification Failed", $"Command Error: {e.Message}");
                }

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Code, Code);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The code was successfully modified");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Modification - Code", $"Bind Id: {bind.Id}", $"Old Code: {bind.Code}\nNew Prefix: {Code}");
            }

            [Command("roles-add"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to add roles to a custombind")]
            public async Task AddRolesAsync(CommandContext Context, [Description("The Id of the assigned custombind")] int Id, 
                [Description("The Roles to bind to the bind")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Id does not exist");
                if (Roles.Any(r => r.Id == Context.Guild.EveryoneRole.Id))
                    throw new CommandException("Bind Modification Failed", "You cannot use the `@everyone` role in a bind");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PushEach(r => r.CustomBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The new roles were successfully added");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Modification - Added Roles", $"Bind Id: {bind.Id}", $"Added Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }

            [Command("roles-remove"), RequireGuild, RequireRoWifiAdmin]
            [Description("Command to remove binded roles from a custombind")]
            public async Task RemoveRolesAsync(CommandContext Context, [Description("The Id of the assigned custombind")] int Id, 
                [Description("The roles to remove from the bind")]params DiscordRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    throw new CommandException("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    throw new CommandException("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.CustomBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(DiscordColor.Green).WithTitle("Bind Modification Successful").WithDescription($"The given roles were successfully removed");
                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogAction(Context.Guild, Context.User, "Custom Bind Modification - Added Roles", $"Bind Id: {bind.Id}", $"Removed Roles: {string.Concat(Roles.Select(r => $" <@&{ r}> "))}");
            }
        }
    }
}