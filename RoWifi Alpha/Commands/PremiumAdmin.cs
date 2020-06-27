using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MongoDB.Driver;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    [Group("premium")]
    [RequireBotPermissions(Permissions.EmbedLinks)]
    [Description("Module to access premium of servers")]
    public class PremiumAdmin : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public PatreonService Patreon { get; set; }

        [GroupCommand, RequireGuild, Description("Command to view premium status")]
        public async Task PremiumAsync(CommandContext Context)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed()
                .WithTitle(Context.User.Username + "#" + Context.User.Discriminator);
            if (premium == null)
            {
                embed.AddField("Tier", "Normal")
                    .AddField("Premium Features", "None");
            }
            else if (premium.PType == PremiumType.Alpha)
                embed.AddField("Tier", "Alpha")
                    .AddField("Premium Features", "Auto Detection for 1 server\nUpdate All (6 hours cooldown)");
            else if (premium.PType == PremiumType.Beta)
                embed.AddField("Tier", "Beta")
                    .AddField("Premium Features", "Auto Detection for all owned servers\nUpdate All (6 hours cooldown)\nCustombinds\nBackups");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("redeem"), RequireGuild]
        [Description("Command to enable premium features of a server")]
        public async Task RedeemAsync(CommandContext Context)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
            {
                (string PatreonId, int? Tier) = await Patreon.GetPatron(Context.User.Id.ToString());
                if (PatreonId == "None")
                    throw new CommandException("Patreon Linking Failed", "Patreon Account was not found for this Discord Account. Please make sure your Discord Account" +
                            " is linked to your Patreon Account");
                if (Tier == null)
                    throw new CommandException("Patreon Linking Failed", "Must be a patron of Alpha or Beta Tier to redeem");
                if (Tier == 4014582)
                {
                    premium = new Premium { DiscordId = Context.User.Id, PatreonId = ulong.Parse(PatreonId), DiscordServers = new List<ulong>(), PType = PremiumType.Alpha };
                }
                else if (Tier == 4656839)
                {
                    premium = new Premium { DiscordId = Context.User.Id, PatreonId = ulong.Parse(PatreonId), DiscordServers = new List<ulong>(), PType = PremiumType.Beta };
                }
                else
                    return;
                await Database.AddPremium(premium);
                DiscordEmbedBuilder embed2 = Miscellanous.GetDefaultEmbed();
                embed2.WithTitle("Patreon Linking Successful").WithDescription("Patreon Account was found for this account successfully");
                await Context.RespondAsync(embed: embed2.Build());
            }

            if (!Context.Member.IsOwner && Context.User.Id != 311395138133950465)
                throw new CommandException("Redeem Failed", "You must be the server owner to use this command");
            if (premium.PType == PremiumType.Alpha && premium.DiscordServers.Count > 0)
                throw new CommandException("Redeem Failed", "You may only redeem premium on one server");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Redeem Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, true).Set(g => g.Settings.Type, (GuildType)premium.PType);
            await Database.ModifyGuild(Context.Guild.Id, update);
            UpdateDefinition<Premium> update2 = Builders<Premium>.Update.Push(u => u.DiscordServers, Context.Guild.Id);

            if (!premium.DiscordServers.Contains(Context.Guild.Id))
                await Database.ModifyPremium(Context.User.Id, update2);

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Redeem Successful").WithDescription($"Added Premium features to {Context.Guild.Name}");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("remove"), RequireGuild]
        [Description("Command to revoke premium features from a server")]
        public async Task RemoveAsync(CommandContext Context)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
                throw new CommandException("Premium Disable Failed", "You must be a premium member to use this command");
            if (!premium.DiscordServers.Contains(Context.Guild.Id))
                throw new CommandException("Premium Disable Failed", "This server either does not have premium enabled or the premium is owned by an another member");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Premium Disable Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, false)
                                                    .Set(g => g.Settings.Type, GuildType.Normal)
                                                    .Set(g => g.CustomBinds, new List<CustomBind>());
            await Database.ModifyGuild(Context.Guild.Id, update);
            UpdateDefinition<Premium> update2 = Builders<Premium>.Update.Pull(u => u.DiscordServers, Context.Guild.Id);
            await Database.ModifyPremium(Context.User.Id, update2);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Premium Disable Successful").WithDescription($"Removed premium features from {Context.Guild.Name}");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("check"), RequireGuild, RequireOwner, Hidden]
        public async Task CheckAsync(CommandContext Context)
        {
            List<Premium> AllPremium = await Database.GetAllPremium();
            foreach (Premium p in AllPremium)
            {
                (string PatreonId, int? Tier) = await Patreon.GetPatron(p.DiscordId.ToString());
                if (Tier == null)
                {
                    await Context.RespondAsync($"{p.DiscordId} {p.PatreonId} {p.PType} Deleted");
                    foreach(ulong s in p.DiscordServers)
                    {
                        UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, false)
                                                    .Set(g => g.Settings.Type, GuildType.Normal)
                                                    .Set(g => g.CustomBinds, new List<CustomBind>());
                        await Database.ModifyGuild(s, update);
                    }
                    await Database.DeletePremium(p.DiscordId);
                } 
            }
        }

        [Command("add"), RequireGuild, RequireOwner, Hidden]
        public async Task AddAsync(CommandContext Context, ulong Id, int Type)
        {
            Premium premium = new Premium { DiscordId = Id, PatreonId = 1, PType = (PremiumType)Type, DiscordServers = new List<ulong>() };
            try
            {
                await Database.AddPremium(premium);
                await Context.RespondAsync($"Added {premium.PType} to {Id}");
            }
            catch(Exception)
            {
                await Context.RespondAsync($"Id already has premium");
            } 
        }

        [Command("delete"), RequireGuild, RequireOwner, Hidden]
        public async Task DeleteAsync(CommandContext Context, ulong Id)
        {
            Premium premium = await Database.GetPremium(Id);
            try
            {
                foreach (ulong s in premium.DiscordServers)
                {
                    UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, false)
                                                .Set(g => g.Settings.Type, GuildType.Normal)
                                                .Set(g => g.CustomBinds, new List<CustomBind>());
                    await Database.ModifyGuild(s, update);
                }
                await Database.DeletePremium(Id);
                await Context.RespondAsync($"Removed premium from {Id}");
            }
            catch (Exception)
            {
                await Context.RespondAsync($"Id does not have premium");
            }
        }
    }
}
