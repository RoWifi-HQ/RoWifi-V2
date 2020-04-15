using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MongoDB.Driver;
using RoWifi_Alpha.Addons.Interactive;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("blacklists")]
    public class Blacklists : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> ViewBlacklistAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Blacklist Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Blacklists == null || guild.Blacklists.Count == 0)
                return RoWifiResult.FromError("Blacklist Viewing Failed", "There are no blacklists associated with this server");

            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            var BlacklistList = guild.Blacklists.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;

            foreach (List<RoBlacklist> blacklists in BlacklistList)
            {
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Blacklists").WithDescription($"Page {Page}");
                foreach (RoBlacklist blacklist in guild.Blacklists)
                {
                    if (blacklist.Type == BlacklistType.Name)
                        embed.AddField($"Id: {blacklist.Id}", $"Type: Id\nReason: {blacklist.Reason}");
                    else if (blacklist.Type == BlacklistType.Group)
                        embed.AddField($"Id: {blacklist.Id}", $"Type: Group\nReason: {blacklist.Reason}");
                    else if (blacklist.Type == BlacklistType.Custom)
                        embed.AddField($"Code: {blacklist.Id}", $"Type: Custom\nReason: {blacklist.Reason}");
                }
                embeds.Add(embed);
            }
            await PagedReplyAsync(embeds);
            return RoWifiResult.FromSuccess();
        }

        [Command("name"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> BlacklistNameAsync(string Name, [Remainder] string Reason)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            int? RobloxId = await Roblox.GetIdFromUsername(Name);
            if (RobloxId == null)
                return RoWifiResult.FromError("Blacklist Addition Failed", "There was no Roblox Id found associated with this name");
            if (Reason.Length == 0)
                Reason = "N/A";

            RoBlacklist blacklist = new RoBlacklist { Id = RobloxId.ToString(), Reason = Reason, Type = BlacklistType.Name };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Id\nReason: {blacklist.Reason}");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("group"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> BlacklistGroupAsync(int Id, [Remainder] string Reason)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (Reason.Length == 0)
                Reason = "N/A";

            RoBlacklist blacklist = new RoBlacklist { Id = Id.ToString(), Reason = Reason, Type = BlacklistType.Group };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Group\nReason: {blacklist.Reason}");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("custom", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> BlacklistCustomAsync([Remainder] string Code)
        {
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user == null)
                return RoWifiResult.FromError("Blacklist Addition Failed", "You must be verified to use this feature");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Blacklist Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            try
            {
                RoCommand cmd = new RoCommand(Code);
                Dictionary<int, int> Ranks = await Roblox.GetUserRoles(user.RobloxId);
                string Username = await Roblox.GetUsernameFromId(user.RobloxId);
                RoCommandUser CommandUser = new RoCommandUser(user, Context.User as IGuildUser, Ranks, Username);
                cmd.Evaluate(CommandUser);
            }
            catch (Exception e)
            {
                return RoWifiResult.FromError("Blacklist Addition Failed", $"Command Error: {e.Message}");
            }

            await ReplyAsync("Enter the reason of this blacklist.\nSay `cancel` if you wish to cancel this command");
            SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Blacklist Addition Failed", "Command has been cancelled");
            string Reason = response.Content;

            RoBlacklist blacklist = new RoBlacklist { Id = Code, Reason = Reason, Type = BlacklistType.Custom };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.Blacklists, blacklist);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Id: {blacklist.Id}", $"Type: Custom\nReason: {blacklist.Reason}");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("remove"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> BlacklistDeleteAsync([Remainder] string Id)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Blacklist Removal Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Blacklists == null || guild.Blacklists.Count == 0)
                return RoWifiResult.FromError("Blacklist Removal Failed", "There are no blacklists associated with this server");
            RoBlacklist blacklist = guild.Blacklists.Where(b => b.Id == Id).FirstOrDefault();
            if (blacklist == null)
                return RoWifiResult.FromError("Blacklist Removal Failed", "There was no blacklist found associated with the given Id");
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.Blacklists, blacklist);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Blacklist Removeal Successful").WithDescription($"The blacklist with Id {Id} was successfully deleted");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
