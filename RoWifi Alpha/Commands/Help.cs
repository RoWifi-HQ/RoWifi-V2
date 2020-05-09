using Discord;
using Discord.Commands;
using RoWifi_Alpha.Addons.Help;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    public class Help : ModuleBase<SocketCommandContext>
    {
        public CommandService commandService { get; set; }

        public DatabaseService Database { get; set; }

        [Command("help")]
        [RequireBotPermission(ChannelPermission.EmbedLinks, ErrorMessage = "Looks like I'm missing the Embed Links Permission")]
        public async Task HelpAsync([Remainder] string Command = null)
        {
            var helpEmbed = commandService.GetDefaultEmbed(Command);
            await ReplyAsync(embed: helpEmbed);
        }

        [Command("support"), Alias("invite")]
        [RequireBotPermission(ChannelPermission.EmbedLinks, ErrorMessage = "Looks like I'm missing the Embed Links Permission")]
        public async Task SupportAsync()
        {
            string DiscLink = "https://www.discord.gg/h4BGGyR";
            string InviteLink = "https://discordapp.com/oauth2/authorize?client_id=508968886998269962&scope=bot&permissions=402672704";
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Support Server", $"To know more about announcements, updates and other stuff: [Click Here]({DiscLink})")
                .AddField("Invite Link", $"To invite the bot into your server: [Click Here]({InviteLink})");
            await ReplyAsync(embed: embed.Build());
        }

        [Command("partner-add"), RequireOwner]
        public async Task AddPartnerAsync(IGuildUser user)
        {
            Premium premium = new Premium { DiscordId = user.Id, PatreonId = 0, DiscordServers = new List<ulong>(), PType = PremiumType.Beta };
            bool Success = await Database.AddPremium(premium);
            await ReplyAsync(Success ? "Success" : "Failure");
        }
    }
}
