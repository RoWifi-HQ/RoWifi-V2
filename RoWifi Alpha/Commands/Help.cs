using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using RoWifi_Alpha.Utilities;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    public class Help : ModuleBase<SocketCommandContext>
    {
        public CommandService commandService { get; set; }

        [Command("help")]
        public async Task HelpAsync([Remainder] string Command = null)
        {
            string Prefix = "?";
            var helpEmbed = commandService.GetDefaultHelpEmbed(Command, Prefix);
            await ReplyAsync(embed: helpEmbed);
        }

        [Command("support")]
        public async Task SupportAsync()
        {
            string DiscLink = "https://www.discord.gg/h4BGGyR";
            string InviteLink = "https://discordapp.com/oauth2/authorize?client_id=508968886998269962&scope=bot&permissions=2080898303";
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Support Server", $"To know more about announcements, updates and other stuff: [Click Here]({DiscLink})")
                .AddField("Invite Link", $"To invite the bot into your server: [Click Here]({InviteLink})");
            await ReplyAsync(embed: embed.Build());
        }
    }
}
