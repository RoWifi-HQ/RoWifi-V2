using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using RoWifi_Alpha.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using PremiumType = RoWifi_Alpha.Models.PremiumType;

namespace RoWifi_Alpha.Commands
{
    [Group("backup"), Aliases("backups")]
    [RequireBotPermissions(Permissions.EmbedLinks)]
    [Description("Module to save and restore server binds in the database")]
    public class Backups : BaseCommandModule
    {
        public DatabaseService Database { get; set; }

        [GroupCommand, RequireGuild, RequireRoWifiAdmin]
        [Description("Command to view the saved backups")]
        public async Task GroupCommand(CommandContext Context)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null || premium.PType != PremiumType.Beta)
                throw new CommandException("Backup Failed", "You must be a Beta Tier member to use this command");

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed().WithTitle("Backups");
            List<RoBackup> Backups = await Database.GetBackups(Context.User.Id);
            
            foreach(RoBackup backup in Backups)
            {
                embed.AddField($"Name: {backup.Name}", $"Prefix: {backup.CommandPrefix}\nVerification: {backup.VerificationRole}\n" +
                    $"Verified: {backup.VerifiedRole}\nRankbinds: {backup.Rankbinds.Count}\nGroupbinds: {backup.Groupbinds.Count}\n" +
                    $"Custombinds: {backup.Custombinds?.Count ?? 0}\n Assetbinds: {backup.Assetbinds?.Count ?? 0}");
            }
            await Context.RespondAsync(embed: embed);
        }

        [Command("new"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to add a new backup to the database")]
        public async Task NewBackupAsync(CommandContext Context, [Description("The keyword to associate the backup to")]string Name)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null || premium.PType != PremiumType.Beta)
                throw new CommandException("Backup Failed", "You must be a Beta Tier member to use this command");
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Backup Failed", "Please ask the server owner to set up this server.");

            RoBackup backup = new RoBackup(Context.User.Id, Name, guild, Context.Guild);
            await Database.AddBackup(backup, Name);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Backup successful").WithDescription($"Succesfully saved the settings of {Context.Guild.Name} in the database");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("restore"), RequireGuild, RequireRoWifiAdmin]
        [RequireBotPermissions(Permissions.ManageRoles)]
        [Description("Command to load the saved backup into the server")]
        public async Task RestoreAsync(CommandContext Context, [Description("The keyword to associate the backup to")]string Name)
        {
            Premium premium = await Database.GetPremium(Context.User.Id);
            if (premium == null)
                throw new CommandException("Restore Failed", "You must be a Beta Tier member to use this command");

            RoBackup backup = await Database.GetBackup(Context.User.Id, Name);
            if (backup == null)
                throw new CommandException("Restore Failed", "There is no backup associated with this name");

            RoGuild guild = await backup.RestoreAsync(Context.Guild);
            await Database.AddGuild(guild, await Database.GetGuild(Context.Guild.Id) == null);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithTitle("Restore Succesful").WithDescription("Binds were successfully transferred into this server");
            await Context.RespondAsync(embed: embed.Build());
        }
    }
}
