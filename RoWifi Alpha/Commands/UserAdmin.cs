using System;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using RoWifi_Alpha.Criterion;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;

namespace RoWifi_Alpha.Commands
{
    public class UserAdmin : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }

        [Command("verify", RunMode = RunMode.Async), RequireContext(ContextType.Guild)]
        [Summary("Command to link Roblox Account to Discord Account")]
        public async Task<RuntimeResult> VerifyAsync(string RobloxName = "")
        {
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user != null)
            {
                embed.WithTitle("User Already Verified")
                    .WithDescription("To change your verified account, use `reverify`. To get your roles, use `update`");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            if (RobloxName.Length == 0)
            {
                await ReplyAsync("Enter your Roblox Name.\nSay `cancel` if you wish to cancel this command");
                SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
                if (response == null)
                {
                    embed.WithTitle("Verification Failed").WithDescription("Command timed out. Please try again");
                    await ReplyAsync(embed: embed.Build());
                    return RoWifiResult.FromSuccess();
                }
                RobloxName = response.Content;
            }

            int? RobloxId;
            try
            {
                RobloxId = await Roblox.GetIdFromUsername(RobloxName);
                if (RobloxId == null)
                {
                    embed.WithTitle("Verification Failed").WithDescription("Invalid Roblox Username. Please try again");
                    await ReplyAsync(embed: embed.Build());
                    return RoWifiResult.FromSuccess();
                }
            }
            catch (Exception)  { return RoWifiResult.FromRobloxError(); }

            string Code = Miscellanous.GenerateCode();
            embed.AddField("Verification Process", "Enter the following code in your Roblox status/description")
                .AddField("Code", Code)
                .AddField("Further Instructions", "After doing so, reply to me saying 'done'.");
            await ReplyAsync(embed: embed.Build());

            embed = Miscellanous.GetDefaultEmbed();
            var criterion = new Criteria<SocketMessage>()
                .AddCriterion(new EnsureSourceUserCriterion())
                .AddCriterion(new EnsureContentCriterion("done", "cancel"));
            SocketMessage response2 = await NextMessageAsync(criterion, TimeSpan.FromMinutes(5));
            if(response2 == null)
            {  
                embed.WithTitle("Verification Failed").WithDescription("Command timed out. Please try again");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }
            bool Present;
            try
            {
                Present = await Roblox.CheckCode(RobloxId.Value, Code);
            }
            catch(Exception) { return RoWifiResult.FromRobloxError(); }

            if(Present)
            {
                RoUser newUser = new RoUser { DiscordId = Context.User.Id, RobloxId = RobloxId.Value };
                bool Success = await Database.AddUser(newUser);
                if (Success)
                {
                    embed.WithTitle("Verification Successful").WithDescription("To get your roles, run `update`. To change your linked Roblox Account, use `reverify`");
                    await ReplyAsync(embed: embed.Build());
                    return RoWifiResult.FromSuccess();
                }
                else
                    return RoWifiResult.FromMongoError();
            }
            else
            {
                embed.WithTitle("Verification Failed").WithDescription($"`{Code}` was not found in the profile. Please try again.");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }
        }
    }
}
