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
    [Group("blacklists"), Aliases("blacklist", "bl")]
    [RequireBotPermissions(Permissions.EmbedLinks), RequireGuild, RequireRoWifiAdmin]
    [Description("Module to blacklist users from a server")]
    public class Blacklists : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand]
        [Description("View users blacklisted from the server")]
        public async Task GroupCommand(CommandContext Context)
        {
            var interactivity = Context.Client.GetInteractivity();
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Blacklist Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Blacklists == null || guild.Blacklists.Count == 0)
                throw new CommandException("Blacklist Viewing Failed", "There are no blacklists associated with this server");

            List<Page> pages = new List<Page>();
            var BlacklistList = guild.Blacklists.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;

            foreach (List<RoBlacklist> blacklists in BlacklistList)
            {
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Blacklists").WithDescription($"Page {Page}");
                foreach (RoBlacklist blacklist in guild.Blacklists)
                {
                    if (blacklist.Type == BlacklistType.Name)
                        embed.AddField($"Id: {blacklist.Id}", $"Type: Id\nReason: {blacklist.Reason}", true);
                    else if (blacklist.Type == BlacklistType.Group)
                        embed.AddField($"Id: {blacklist.Id}", $"Type: Group\nReason: {blacklist.Reason}", true);
                    else if (blacklist.Type == BlacklistType.Custom)
                        embed.AddField($"Code: {blacklist.Id}", $"Type: Custom\nReason: {blacklist.Reason}", true);
                }
                pages.Add(new Page(embed: embed));
                Page++;
            }
            if (Page == 2)
                await Context.RespondAsync(embed: pages[0].Embed);
            else
                await interactivity.SendPaginatedMessageAsync(Context.Channel, Context.User, pages);
        }

        [Command("name"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to blacklist a user from Roblox Username")]
        public async Task BlacklistNameAsync(CommandContext Context, [Description("The Roblox Username of the user to blacklist")] string Name, 
            [RemainingText, Description("The reason to blacklist the user for")] string Reason = "")
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            int? RobloxId = await Roblox.GetIdFromUsername(Name);
            if (RobloxId == null)
                throw new CommandException("Blacklist Addition Failed", "There was no Roblox Id found associated with this name");
            if (Reason.Length == 0)
                Reason = "N/A";

            RoBlacklist blacklist = new RoBlacklist(RobloxId.ToString(), Reason, BlacklistType.Name);
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Blacklist Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Id\nReason: {blacklist.Reason}");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Blacklist Addition", $"Id: {blacklist.Id}", $"Type: Id\nReason: {blacklist.Reason}");
        }

        [Command("group"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to blacklist an entire group")]
        public async Task BlacklistGroupAsync(CommandContext Context, [Description("The Id of the Roblox group to blacklist")]int Id, 
            [RemainingText, Description("The reason to blacklist the group for")] string Reason = "")
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (Reason.Length == 0)
                Reason = "N/A";

            RoBlacklist blacklist = new RoBlacklist(Id.ToString(), Reason, BlacklistType.Group);
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Blacklist Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Group\nReason: {blacklist.Reason}");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Blacklist Addition", $"Id: {blacklist.Id}", $"Type: Group\nReason: {blacklist.Reason}");
        }

        [Command("custom"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to create your own blacklists")]
        public async Task BlacklistCustomAsync(CommandContext Context, [RemainingText, Description("The custom code to define the blacklist")] string Code = "")
        {
            if (Code.Length == 0)
                throw new CommandException("Blacklist Addition Failed", "The blacklist code should not empty");
            var interactivity = Context.Client.GetInteractivity();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user == null)
                throw new CommandException("Blacklist Addition Failed", "You must be verified to use this feature");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

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
                throw new CommandException("Blacklist Addition Failed", $"Command Error: {e.Message}");
            }

            await Context.RespondAsync("Enter the reason of this blacklist.\nSay `cancel` if you wish to cancel this command");
            var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id);
            if (response.TimedOut || response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                throw new CommandException("Blacklist Addition Failed", "Command has been cancelled");
            string Reason = response.Result.Content;

            RoBlacklist blacklist = new RoBlacklist(Code, Reason, BlacklistType.Custom);
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Bind Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Custom\nReason: {blacklist.Reason}");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Blacklist Addition", $"Id: {blacklist.Id}", $"Type: Custom\nReason: {blacklist.Reason}");
        }

        [Command("remove"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to remove a blacklist"), Aliases("delete")]
        public async Task BlacklistDeleteAsync(CommandContext Context, [RemainingText, Description("The Id of the assigned blacklist")] string Id)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Blacklist Removal Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Blacklists == null || guild.Blacklists.Count == 0)
                throw new CommandException("Blacklist Removal Failed", "There are no blacklists associated with this server");
            RoBlacklist blacklist = guild.Blacklists.Where(b => b.Id == Id).FirstOrDefault();
            if (blacklist == null)
                throw new CommandException("Blacklist Removal Failed", "There was no blacklist found associated with the given Id");
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Blacklist Removal Successful").WithDescription($"The blacklist with Id {Id} was successfully deleted");
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Blacklist Deletion", $"Id: {blacklist.Id}", $"Reason: {blacklist.Reason}");
        }
    }
}
