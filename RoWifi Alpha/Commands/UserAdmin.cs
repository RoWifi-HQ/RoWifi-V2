using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using RoWifi_Alpha.Addons.Interactive;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;

namespace RoWifi_Alpha.Commands
{
    public class UserAdmin : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }
        public LoggerService Logger { get; set; }

        [Command("verify", RunMode = RunMode.Async), RequireContext(ContextType.Guild)]
        [Summary("Command to link Roblox Account to Discord Account")]
        public async Task<RuntimeResult> VerifyAsync([Summary("The Roblox Username to bind to the Discord Account")]string RobloxName = "")
        {
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user != null)
                return RoWifiResult.FromError("User Already Verified", "To change your verified account, use `reverify`. To get your roles, use `update`");

            if (RobloxName.Length == 0)
            {
                await ReplyAsync("Enter your Roblox Name.\nSay `cancel` if you wish to cancel this command");
                SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
                if (response == null)
                    return RoWifiResult.FromError("Verification Failed", "Command timed out. Please try again");
                RobloxName = response.Content;
            }

            int? RobloxId;
            try
            {
                RobloxId = await Roblox.GetIdFromUsername(RobloxName);
                if (RobloxId == null)
                    return RoWifiResult.FromError("Verification Failed", "Invalid Roblox Username. Please try again");
            }
            catch (Exception)  { return RoWifiResult.FromRobloxError("Verification Failed"); }

            string Code = Miscellanous.GenerateCode();
            embed.AddField("Verification Process", "Enter the following code in your Roblox status/description")
                .AddField("Code", Code)
                .AddField("Further Instructions", "After doing so, reply to me saying 'done'.");
            await ReplyAsync(embed: embed.Build());

            var criterion = new Criteria<SocketMessage>()
                .AddCriterion(new EnsureSourceUserCriterion())
                .AddCriterion(new EnsureContentCriterion("done", "cancel"));
            SocketMessage response2 = await NextMessageAsync(criterion, TimeSpan.FromMinutes(5));
            if(response2 == null)
                return RoWifiResult.FromError("Verification Failed", "Command timed out. Please try again");
            bool Present;
            try
            {
                Present = await Roblox.CheckCode(RobloxId.Value, Code);
            }
            catch(Exception) { return RoWifiResult.FromRobloxError("Verification Failed"); }

            if(Present)
            {
                RoUser newUser = new RoUser { DiscordId = Context.User.Id, RobloxId = RobloxId.Value };
                await Database.AddUser(newUser);
                return RoWifiResult.FromSuccess("Verification Successful", "To get your roles, run `update`. To change your linked Roblox Account, use `reverify`");
            }
            else
                return RoWifiResult.FromError("Verification Failed", $"`{Code}` was not found in the profile. Please try again.");
        }

        [Command("reverify"), RequireContext(ContextType.Guild)]
        [Summary("Command to change linked Roblox Account")]
        public async Task<RuntimeResult> ReverifyAsync([Summary("The Roblox Username to bind to the Discord Account")]string RobloxName = "")
        {
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user != null)
                return RoWifiResult.FromError("User Not Verified", "To verify your account, use `verify`");

            if (RobloxName.Length == 0)
            {
                await ReplyAsync("Enter your Roblox Name.\nSay `cancel` if you wish to cancel this command");
                SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
                if (response == null)
                    return RoWifiResult.FromError("Verification Failed", "Command timed out. Please try again");
                RobloxName = response.Content;
            }

            int? RobloxId;
            try
            {
                RobloxId = await Roblox.GetIdFromUsername(RobloxName);
                if (RobloxId == null)
                    return RoWifiResult.FromError("Verification Failed", "Invalid Roblox Username. Please try again");
            }
            catch (Exception) { return RoWifiResult.FromRobloxError("Verification Failed"); }

            string Code = Miscellanous.GenerateCode();
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Verification Process", "Enter the following code in your Roblox status/description")
                .AddField("Code", Code)
                .AddField("Further Instructions", "After doing so, reply to me saying 'done'.");
            await ReplyAsync(embed: embed.Build());

            var criterion = new Criteria<SocketMessage>()
                .AddCriterion(new EnsureSourceUserCriterion())
                .AddCriterion(new EnsureContentCriterion("done", "cancel"));
            SocketMessage response2 = await NextMessageAsync(criterion, TimeSpan.FromMinutes(5));
            if (response2 == null)
                return RoWifiResult.FromError("Verification Failed", "Command timed out. Please try again");

            bool Present;
            try
            {
                Present = await Roblox.CheckCode(RobloxId.Value, Code);
            }
            catch (Exception) { return RoWifiResult.FromRobloxError("Verification Failed"); }

            if (Present)
            {
                RoUser newUser = new RoUser { DiscordId = Context.User.Id, RobloxId = RobloxId.Value };
                await Database.AddUser(newUser, false);
                return RoWifiResult.FromSuccess("Verification Successful", "To get your roles, run `update`. To change your linked Roblox Account, use `reverify`");
            }
            else
                return RoWifiResult.FromError("Verification Failed", $"`{Code}` was not found in the profile. Please try again.");
        }

        [Command("update"), RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.ManageNicknames, 
            ErrorMessage = "I cannot update users as I need the following permissions: Manage Roles, Manage Nicknames.")]
        [Summary("Command to update a user's roles")]
        public async Task<RuntimeResult> UpdateAsync([Summary("The User to be updated")]IGuildUser member = null)
        {
            if (member == null)
                member = (IGuildUser)Context.User;
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            RoUser user = await Database.GetUserAsync(member.Id);
            if(user == null)
                return RoWifiResult.FromError("Update Failed", "User was not verified. Please ask the user to verify.");
            if((member as SocketGuildUser).Roles.Where(r => r != null).Any(r => r.Name == "RoWifi Bypass"))
                return RoWifiResult.FromError("Update Skipped", "`RoWifi Bypass` was found in the user roles. Update may not be performed in this user.");
            if (Context.Guild.OwnerId == member.Id)
                return RoWifiResult.FromError("Update Skipped", "Due to Discord limitations, we are unable to update the server owner");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Update Failed", "Server was not setup. Please ask the server owner to set up this server.");
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

                var fields = new List<EmbedFieldBuilder>() 
                {
                    new EmbedFieldBuilder().WithName("Nickname").WithValue(DiscNick),
                    new EmbedFieldBuilder().WithName("Added Roles").WithValue(AddStr),
                    new EmbedFieldBuilder().WithName("Removed Roles").WithValue(RemoveStr)
                };

                embed.WithFields(fields).WithColor(Color.Green).WithTitle("Update");
                await ReplyAsync(embed: embed.Build());
                await Logger.LogServer(Context.Guild, embed.Build());
                return RoWifiResult.FromSuccess();
            }
            catch(HttpException)
            {
                return RoWifiResult.FromError("Update Failed", "We were unable to give you one or more roles since they seem to present above the bot's role"); ;
            }
            catch(RobloxException)
            {
                return RoWifiResult.FromRobloxError("Update Failed");
            }
            catch(BlacklistException)
            {
                return RoWifiResult.FromError("Update Failed", "User was found on the server blacklist");
            }
        }

        [Command("userinfo"), RequireContext(ContextType.Guild)]
        public async Task<RuntimeResult> UserInfoAsync([Summary("The User whose info is to be viewed")]IGuildUser member = null)
        {
            if (member == null)
                member = Context.User as IGuildUser;

            RoUser user = await Database.GetUserAsync(member.Id);
            if (user == null)
                return RoWifiResult.FromError("User Information Failed", "User was not verified. Please ask the user to verify.");

            string Username = await Roblox.GetUsernameFromId(user.RobloxId);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle(member.Username)
                .WithDescription("Profile Information")
                .WithThumbnailUrl($"http://www.roblox.com/Thumbs/Avatar.ashx?x=150&y=150&Format=Png&username={Username}")
                .AddField("Username", Username)
                .AddField("Roblox Id", user.RobloxId)
                .AddField("Discord Id", user.DiscordId);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
