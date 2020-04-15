using Discord;
using Discord.Commands;
using MongoDB.Driver;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    [Group("premium")]
    public class PremiumAdmin : ModuleBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public PatreonService Patreon { get; set; }

        [Command("redeem"), RequireContext(ContextType.Guild)]
        public async Task<RoWifiResult> RedeemAsync()
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
            {
                (string PatreonId, int? Tier) = await Patreon.GetPatron(Context.User.Id.ToString());
                if (PatreonId == "None")
                    return RoWifiResult.FromError("Patreon Linking Failed", "Patreon Account was not found for this Discord Account. Please make sure your Discord Account" +
                            " is linked to your Patreon Account");
                if (Tier == null)
                    return RoWifiResult.FromError("Patreon Linking Failed", "Must be a patron of Alpha or Beta Tier to redeem");
                if (Tier == 4014582)
                {
                    premium = new Premium { DiscordId = Context.User.Id, PatreonId = ulong.Parse(PatreonId), DiscordServers = new List<ulong>(), PType = PremiumType.Alpha };
                }
                else if (Tier == 4656839)
                {
                    premium = new Premium { DiscordId = Context.User.Id, PatreonId = ulong.Parse(PatreonId), DiscordServers = new List<ulong>(), PType = PremiumType.Beta };
                }
                else
                    return RoWifiResult.FromSuccess();
                await Database.AddPremium(premium);
                EmbedBuilder embed2 = Miscellanous.GetDefaultEmbed();
                embed2.WithTitle("Patreon Linking Successful").WithDescription("Patreon Account was found for this account successfully");
                await ReplyAsync(embed: embed2.Build());
            }

            if (Context.Guild.OwnerId != Context.User.Id)
                return RoWifiResult.FromError("Redeem Failed", "You must be the server owner to use this command");
            if (premium.DiscordServers.Contains(Context.Guild.Id))
                return RoWifiResult.FromError("Redeem Failed", "Premium is already redeemed on this server");
            if (premium.PType == PremiumType.Alpha && premium.DiscordServers.Count > 0)
                return RoWifiResult.FromError("Redeem Failed", "You may only redeem premium on one server");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Redeem Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, true).Set(g => g.Settings.Type, (GuildType)premium.PType);
            await Database.ModifyGuild(Context.Guild.Id, update);
            UpdateDefinition<Premium> update2 = Builders<Premium>.Update.Push(u => u.DiscordServers, Context.Guild.Id);
            await Database.ModifyPremium(Context.User.Id, update2);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Redeem Successful").WithDescription($"Added Premium features to {Context.Guild.Name}");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("remove"), RequireContext(ContextType.Guild)]
        public async Task<RuntimeResult> RemoveAsync()
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
                return RoWifiResult.FromError("Premium Disable Failed", "You must be a premium member to use this command");
            if (premium.DiscordServers.Contains(Context.Guild.Id))
                return RoWifiResult.FromError("Premium Disable Failed", "This server either does not have premium enabled or the premium is owned by an another member");

            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Premium Disable Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.Settings.AutoDetection, false)
                                                    .Set(g => g.Settings.Type, GuildType.Normal)
                                                    .Set(g => g.CustomBinds, null);
            await Database.ModifyGuild(Context.Guild.Id, update);
            UpdateDefinition<Premium> update2 = Builders<Premium>.Update.Pull(u => u.DiscordServers, Context.Guild.Id);
            await Database.ModifyPremium(Context.User.Id, update2);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Premium Disable Successful").WithDescription($"Removed premium features from {Context.Guild.Name}");
            return RoWifiResult.FromSuccess();
        }
    }
}
