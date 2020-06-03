using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    public class Help : BaseCommandModule
    {
        public DatabaseService Database { get; set; }

        [Command("support"), Aliases("invite")]
        [RequireBotPermissions(Permissions.EmbedLinks)]
        public async Task SupportAsync(CommandContext Context)
        {
            string DiscLink = "https://www.discord.gg/h4BGGyR";
            string InviteLink = "https://discordapp.com/oauth2/authorize?client_id=508968886998269962&scope=bot&permissions=402672704";
            string Website = "https://rowifi.link";
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Support Server", $"To know more about announcements, updates and other stuff: [Click Here]({DiscLink})")
                .AddField("Invite Link", $"To invite the bot into your server: [Click Here]({InviteLink})")
                .AddField("Website", $"To check out our website: [Click Here]({Website})");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("partner-add"), RequireOwner, Hidden]
        public async Task AddPartnerAsync(CommandContext Context, DiscordUser user)
        {
            Premium premium = new Premium { DiscordId = user.Id, PatreonId = 0, DiscordServers = new List<ulong>(), PType = PremiumType.Beta };
            bool Success = await Database.AddPremium(premium);
            await Context.RespondAsync(Success ? "Success" : "Failure");
        }

        [Command("botinfo"), RequireBotPermissions(Permissions.EmbedLinks)]
        public async Task BotInfoAsync(CommandContext Context)
        {
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Name", Context.Client.CurrentUser.Username + "#" + Context.Client.CurrentUser.Discriminator, true)
                .AddField("Version", "2.2.0", true)
                .AddField("Language", "C#", true)
                .AddField("Shards", Environment.GetEnvironmentVariable("TOTAL_SHARDS"), true)
                .AddField("Shard Id", Context.Client.ShardId.ToString(), true)
                .AddField("Servers", Context.Client.Guilds.Count.ToString(), true)
                .AddField("Members", Context.Client.Guilds.Select(g => g.Value.MemberCount).Sum().ToString(), true);
            await Context.RespondAsync(embed: embed.Build());
        }
    }
}
