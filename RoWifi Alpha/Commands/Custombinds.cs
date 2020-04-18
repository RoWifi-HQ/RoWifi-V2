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
    [Group("custombinds")]
    public class Custombinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> ViewCustombindsAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Settings.Type != GuildType.Beta)
                return RoWifiResult.FromError("Bind Viewing Failed", "This module may only be used in Beta Tier Servers");
            if (guild.CustomBinds.Count == 0)
                return RoWifiResult.FromError("Bind Viewing Failed", "There were no custombinds found associated with this server. Perhaps you meant to use `custombinds new`");
            
            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            var CustomBindsList = guild.CustomBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<CustomBind> CBS in CustomBindsList)
            {
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Rankbinds").WithDescription($"Page {Page}");
                foreach (CustomBind Bind in CBS)
                    embed.AddField($"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
                embeds.Add(embed);
                Page++;
            }
            await PagedReplyAsync(embeds);
            return RoWifiResult.FromSuccess();
        }

        [Command("new", RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> NewCustombindAsync([Remainder]string Code)
        {
            RoUser user = await Database.GetUserAsync(Context.User.Id);
            if (user == null)
                return RoWifiResult.FromError("Bind Addition Failed", "You must be verified to use this feature");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Settings.Type != GuildType.Beta)
                return RoWifiResult.FromError("Bind Addition Failed", "This module may only be used on Beta Tier servers");
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
                return RoWifiResult.FromError("Bind Addition Failed", $"Command Error: {e.Message}");
            }

            await ReplyAsync("Enter Prefix to use in the nickname. Enter `N/A` if you do not wish to set a prefix.\nSay `cancel` if you wish to cancel this command");
            SocketMessage response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled");
            string Prefix = response.Content;

            await ReplyAsync("Enter the priority of this bind\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled");
            bool Success = int.TryParse(response.Content, out int Priority);
            if (!Success)
                return RoWifiResult.FromError("Bind Addition Failed", "Priority was not found to be a valid number");

            await ReplyAsync("Ping the Discord Roles you wish to bind to this role. Enter `N/A` if you wish to not bind any role\nSay `cancel` if you wish to cancel this command");
            response = await NextMessageAsync(new EnsureSourceUserCriterion());
            if (response == null || response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                return RoWifiResult.FromError("Bind Addition Failed", "Command has been cancelled");
            SocketRole[] Roles = response.MentionedRoles.ToArray();

            int Id = 1;
            if (guild == null)
                Id = guild.CustomBinds.OrderBy(c => c.Id).Last().Id + 1;
            CustomBind Bind = new CustomBind(Id, Code, Prefix, Priority, Roles.Select(r => r.Id).ToArray());
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.CustomBinds, Bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Bind Id: {Bind.Id}", $"Code: {Bind.Code}\nPrefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("delete"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> DeleteCustombindAsync(int Id)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Deletion Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.Settings.Type != GuildType.Beta)
                return RoWifiResult.FromError("Bind Deletion Failed", "This module may only be used on Beta Tier servers");
            if (guild.CustomBinds == null || guild.CustomBinds.Count == 0)
                return RoWifiResult.FromError("Bind Deletion Failed", "This server has no custombinds to delete");

            CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
            if (bind == null)
                return RoWifiResult.FromError("Bind Deletion Failed", $"A bind with {Id} as Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.CustomBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Id {Id} was successfully deleted");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Group("modify")]
        public class ModifyCustombinds : ModuleBase<SocketCommandContext>
        {
            public DatabaseService Database { get; set; }
            public RobloxService Roblox { get; set; }

            [Command("prefix"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyPrefixAsync(int Id, string Prefix)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                if (guild.Settings.Type != GuildType.Beta)
                    return RoWifiResult.FromError("Bind Modification Failed", "This module may only be used on Beta Tier servers");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Prefix, Prefix);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The prefix was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("priority"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyPriorityAsync(int Id, int Priority)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                if (guild.Settings.Type != GuildType.Beta)
                    return RoWifiResult.FromError("Bind Modification Failed", "This module may only be used on Beta Tier servers");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Priority, Priority);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("code"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> ModifyCodeAsync(int Id, [Remainder] string Code)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                if (guild.Settings.Type != GuildType.Beta)
                    return RoWifiResult.FromError("Bind Modification Failed", "This module may only be used on Beta Tier servers");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Id does not exist");
                RoUser user = await Database.GetUserAsync(Context.User.Id);
                if (user == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "You must be verified to use this feature");

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
                    return RoWifiResult.FromError("Bind Modification Failed", $"Command Error: {e.Message}");
                }

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(r => r.CustomBinds[-1].Code, Code);
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The code was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-add"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> AddRolesAsync(int Id, params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                if (guild.Settings.Type != GuildType.Beta)
                    return RoWifiResult.FromError("Bind Modification Failed", "This module may only be used on Beta Tier servers");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PushEach(r => r.CustomBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }

            [Command("roles-remove"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
            public async Task<RuntimeResult> RemoveRolesAsync(int Id, params IRole[] Roles)
            {
                RoGuild guild = await Database.GetGuild(Context.Guild.Id);
                if (guild == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
                if (guild.Settings.Type != GuildType.Beta)
                    return RoWifiResult.FromError("Bind Modification Failed", "This module may only be used on Beta Tier servers");
                CustomBind bind = guild.CustomBinds.Where(c => c.Id == Id).FirstOrDefault();
                if (bind == null)
                    return RoWifiResult.FromError("Bind Modification Failed", "A bind with the given Id does not exist");

                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == Context.Guild.Id && g.CustomBinds.Any(r => r.Id == Id));
                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.PullAll(r => r.CustomBinds[-1].DiscordRoles, Roles.Select(r => r.Id));
                await Database.ModifyGuild(Context.Guild.Id, update, filter);
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithColor(Color.Green).WithTitle("Bind Modification Successful").WithDescription($"The priority was successfully modified");
                await ReplyAsync(embed: embed.Build());
                return RoWifiResult.FromSuccess();
            }
        }
    }
}