using Discord;
using Discord.Commands;
using RoWifi_Alpha.Addons.Interactive;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Utilities;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    [Group("backup")]
    [Summary("Module to save and restore server binds in the database")]
    public class Backups : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }

        [Command, RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to view the saved backups")]
        public async Task ViewBackupAsync()
        {
            await ReplyAsync("Command WIP");
        }

        [Command("new"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to add a new backup to the database")]
        public async Task<RuntimeResult> NewBackupAsync([Summary("The keyword to associate the backup to")]string Name)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null || premium.PType != PremiumType.Beta)
                return RoWifiResult.FromError("Backup Failed", "You must be a Beta Tier member to use this command");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Backup Failed", "Please ask the server owner to set up this server.");

            RoBackup backup = new RoBackup(Context.User.Id, Name, guild, Context.Guild);
            await Database.AddBackup(backup, Name);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Backup successful").WithDescription($"Succesfully saved the settings of {Context.Guild.Name} in the database");
            return RoWifiResult.FromSuccess();
        }

        [Command("restore"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [Summary("Command to load the saved backup into the server")]
        public async Task<RuntimeResult> RestoreAsync([Summary("The keyword to associate the backup to")]string Name)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
                return RoWifiResult.FromError("Restore Failed", "You must be a Beta Tier member to use this command");

            RoBackup backup = await Database.GetBackup(Context.User.Id, Name);
            if (backup == null)
                return RoWifiResult.FromError("Restore Failed", "There is no backup associated with this name");

            RoGuild guild = await backup.RestoreAsync(Context.Guild);
            await Database.AddGuild(guild, await Database.GetGuild(Context.Guild.Id) == null);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Restore Succesful").WithDescription("Binds were successfully transferred into this server");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
