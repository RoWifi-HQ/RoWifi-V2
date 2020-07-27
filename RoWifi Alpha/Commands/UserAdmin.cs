using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;

namespace RoWifi_Alpha.Commands
{
    public class UserAdmin : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [Command("verify"), RequireGuild]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        [Description("Command to link Roblox Account to Discord Account")]
        public async Task VerifyAsync(CommandContext Context, 
            [Description("The Roblox Username to bind to the Discord Account")]string RobloxName = "",
            [Description("Option to do verification by. Choices: Code/Game")] string Option = "")
        {
            var interactivity = Context.Client.GetInteractivity();
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user != null)
                throw new CommandException("User Already Verified", "To change your verified account, use `reverify`. To get your roles, use `update`");

            if (RobloxName.Length == 0)
            {
                await Context.RespondAsync("Enter your Roblox Name.\nSay `cancel` if you wish to cancel this command");
                var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));
                if (response.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                RobloxName = response.Result.Content;
            }

            int? RobloxId = await Roblox.GetIdFromUsername(RobloxName);
            if (RobloxId == null)
                throw new CommandException("Verification Failed", "Invalid Roblox Username. Please try again");

            if (Option.Length == 0)
            {
                await Context.RespondAsync("Enter the type of verification you wish to perform\nOptions: `Code`, `Game`.\nSay `cancel` if you wish to cancel this command");
                var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));
                if (response.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                Option = response.Result.Content;
            }

            if (Option.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                string Code = Miscellanous.GenerateCode();
                embed.AddField("Verification Process", "Enter the following code in your Roblox status/description")
                    .AddField("Code", Code)
                    .AddField("Further Instructions", "After doing so, reply to me saying 'done'.");
                await Context.RespondAsync(embed: embed.Build());

                var response2 = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id && (xm.Content.Equals("done", StringComparison.OrdinalIgnoreCase) || xm.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromMinutes(5));
                if (response2.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response2.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                bool Present = await Roblox.CheckCode(RobloxId.Value, Code);

                if (Present)
                {
                    RoUser newUser = new RoUser { DiscordId = Context.User.Id, RobloxId = RobloxId.Value };
                    RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                    await Database.AddUser(newUser);
                    embed = Miscellanous.GetDefaultEmbed();
                    embed.WithColor(DiscordColor.Green).WithTitle("Verification Successful").WithDescription("To get your roles, run `update`. To change your linked Roblox Account, use `reverify`");
                    await Context.RespondAsync(embed: embed.Build());

                    if (guild != null && guild.Settings.UpdateOnVerify)
                        await UpdateAsync(Context);
                }
                else
                    throw new CommandException("Verification Failed", $"`{Code}` was not found in the profile. Please try again.");
            }
            else if (Option.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                string GameUrl = "https://www.roblox.com/games/5146847848/Verification-Center";
                embed = Miscellanous.GetDefaultEmbed();
                embed.AddField("Further Steps", $"Please join the following game to verify yourself: [Click Here]({GameUrl})");
                await Context.RespondAsync(embed: embed.Build());
                QueueUser qUser = new QueueUser { RobloxId = RobloxId.Value, DiscordId = Context.User.Id, Verified = false };
                await Database.AddQueueUser(qUser);
            }
            else
                throw new CommandException("Verification Failed", "Invalid Option was selected");
        }

        [Command("reverify"), RequireGuild]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        [Description("Command to change linked Roblox Account")]
        public async Task ReverifyAsync(CommandContext Context, 
            [Description("The Roblox Username to bind to the Discord Account")]string RobloxName = "",
            [Description("Option to do verification by. Choices: Code/Game")] string Option = "")
        {
            var interactivity = Context.Client.GetInteractivity();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user == null)
                throw new CommandException("User Not Verified", "To verify your account, use `verify`");

            if (RobloxName.Length == 0)
            {
                await Context.RespondAsync("Enter your Roblox Name.\nSay `cancel` if you wish to cancel this command");
                var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));
                if (response.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                RobloxName = response.Result.Content;
            }

            int? RobloxId = await Roblox.GetIdFromUsername(RobloxName);
            if (RobloxId == null)
                throw new CommandException("Verification Failed", "Invalid Roblox Username. Please try again");

            if (Option.Length == 0)
            {
                await Context.RespondAsync("Enter the type of verification you wish to perform\nOptions: `Code`, `Game`.\nSay `cancel` if you wish to cancel this command");
                var response = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));
                if (response.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                Option = response.Result.Content;
            }

            if (Option.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                string Code = Miscellanous.GenerateCode();
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.AddField("Verification Process", "Enter the following code in your Roblox status/description")
                    .AddField("Code", Code)
                    .AddField("Further Instructions", "After doing so, reply to me saying 'done'.");
                await Context.RespondAsync(embed: embed.Build());

                var response2 = await interactivity.WaitForMessageAsync(xm => xm.Author.Id == Context.User.Id && (xm.Content.Equals("done", StringComparison.OrdinalIgnoreCase) || xm.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromMinutes(5));
                if (response2.TimedOut)
                    throw new CommandException("Verification Failed", "Command timed out. Please try again");
                if (response2.Result.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    throw new CommandException("Verification Failed", "Command has been cancelled");
                bool Present = await Roblox.CheckCode(RobloxId.Value, Code);

                if (Present)
                {
                    RoUser newUser = new RoUser { DiscordId = Context.User.Id, RobloxId = RobloxId.Value };
                    RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                    await Database.AddUser(newUser, false);
                    embed = Miscellanous.GetDefaultEmbed();
                    embed.WithColor(DiscordColor.Green).WithTitle("Verification Successful").WithDescription("To get your roles, run `update`. To change your linked Roblox Account, use `reverify`");
                    await Context.RespondAsync(embed: embed.Build());

                    if (guild != null && guild.Settings.UpdateOnVerify)
                        await UpdateAsync(Context);
                }
                else
                    throw new CommandException("Verification Failed", $"`{Code}` was not found in the profile. Please try again.");
            }
            else if (Option.Equals("game", StringComparison.OrdinalIgnoreCase))
            {
                string GameUrl = "https://www.roblox.com/games/5146847848/Verification-Center";
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.AddField("Further Steps", $"Please join the following game to verify yourself: [Click Here]({GameUrl})");
                await Context.RespondAsync(embed: embed.Build());
                QueueUser qUser = new QueueUser { RobloxId = RobloxId.Value, DiscordId = Context.User.Id, Verified = true };
                await Database.AddQueueUser(qUser);
            }
        }

        [Command("update"), RequireGuild, Aliases("getroles")]
        [RequireBotPermissions(Permissions.ManageRoles | Permissions.ManageNicknames | Permissions.EmbedLinks)]
        [Description("Command to update a user's roles")]
        public async Task UpdateAsync(CommandContext Context, [Description("The User to be updated")]DiscordMember member = null)
        {
            if (member == null)
                member = Context.Member;
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            RoUser user = await Database.GetUserAsync(member.Id);
            if(user == null)
                throw new CommandException("Update Failed", "User was not verified. Please ask the user to verify.");
            if(member.Roles.Where(r => r != null).Any(r => r.Name == "RoWifi Bypass"))
                throw new CommandException("Update Skipped", "`RoWifi Bypass` was found in the user roles. Update may not be performed on this user.");
            if (member.IsOwner)
                throw new CommandException("Update Skipped", "Due to Discord limitations, I am unable to update the server owner");
            int botPosition = Context.Guild.CurrentMember.Roles.OrderByDescending(r => r.Position).FirstOrDefault().Position;
            int memberPosition = member.Roles.OrderByDescending(r => r.Position).FirstOrDefault()?.Position ?? 0;
            if (botPosition <= memberPosition)
                throw new CommandException("Update Skipped", "I cannot update users with a higher role than mine. Please move my role as high as possible.");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Update Failed", "Server was not setup. Please ask the server owner to set up this server.");
            try
            {
                (List<ulong> AddedRoles, List<ulong> RemovedRoles, string DiscNick) = await user.UpdateAsync(Roblox, Context.Guild, guild, member);
                string AddStr = "";
                foreach (ulong item in AddedRoles)
                    AddStr += $"- <@&{item}>\n";
                string RemoveStr = "";
                foreach (ulong item in RemovedRoles)
                    RemoveStr += $"- <@&{item}>\n";

                AddStr = AddStr.Length == 0 ? "None" : AddStr;
                RemoveStr = RemoveStr.Length == 0 ? "None" : RemoveStr;
                DiscNick = DiscNick.Length == 0 ? "None" : DiscNick;

                embed.AddField("Nickname", DiscNick)
                    .AddField("Added Roles", AddStr)
                    .AddField("Removed Roles", RemoveStr)
                    .WithColor(DiscordColor.Green)
                    .WithTitle("Update");

                await Context.RespondAsync(embed: embed.Build());
                await Logger.LogServer(Context.Guild, embed.Build());
            }
            catch(UnauthorizedException)
            {
                throw new CommandException("Update Failed", "We were unable to give you one or more roles since they seem to present above the bot's role"); ;
            }
            catch(BlacklistException)
            {
                throw new CommandException("Update Failed", "User was found on the server blacklist");
            }
        }

        [Command("userinfo"), RequireGuild]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        public async Task UserInfoAsync(CommandContext Context, [Description("The User whose info is to be viewed")]DiscordMember member = null)
        {
            if (member == null)
                member = Context.Member;

            RoUser user = await Database.GetUserAsync(member.Id);
            if (user == null)
                throw new CommandException("User Information Failed", "User was not verified. Please ask the user to verify.");
            Premium premium = await Database.GetPremium(member.Id);

            string Username = await Roblox.GetUsernameFromId(user.RobloxId);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle(member.Username)
                .WithDescription("Profile Information")
                .WithThumbnail($"http://www.roblox.com/Thumbs/Avatar.ashx?x=150&y=150&Format=Png&username={Username}")
                .AddField("Username", Username)
                .AddField("Roblox Id", user.RobloxId.ToString())
                .AddField("Discord Id", user.DiscordId.ToString())
                .AddField("Tier", premium?.PType.ToString() ?? "Basic");
            await Context.RespondAsync(embed: embed.Build());
        }
    }
}