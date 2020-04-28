using Discord;
using Discord.Commands;
using MongoDB.Driver;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("settings")]
    [Summary("Command to access settings of a server")]
    public class Settings : ModuleBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public CommandHandler Handler { get; set; }
        public LoggerService Logger { get; set; }

        [Command, RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to view settings of a server")]
        public async Task<RuntimeResult> GroupCommand()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            string Tier = "Normal";
            if (guild.Settings.Type == GuildType.Alpha) Tier = "Alpha";
            if (guild.Settings.Type == GuildType.Beta) Tier = "Beta";

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Guild Id", $"{Context.Guild.Id}", true)
                .AddField("Member Count", $"{Context.Guild.MemberCount}", true)
                .AddField("Shard Id", $"{Context.Client.ShardId}", true)
                .AddField("Settings", $"Tier: {Tier}\nAutoDetection: {guild.Settings.AutoDetection}", true)
                .AddField("Prefix", $"{guild.CommandPrefix ?? "!"}", true)
                .AddField("Verification Role", $"<@&{guild.VerificationRole}>", true)
                .AddField("Verified Role", $"<@&{guild.VerifiedRole}>", true)
                .AddField("Rankbinds", $"{guild.RankBinds.Count}", true)
                .AddField("Groupbinds", $"{guild.GroupBinds.Count}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("verification"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to set the unverified role of the server")]
        public async Task<RuntimeResult> VerificationAsync([Summary("The role to set as the unverified role")]IRole Role)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Modificatione Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.VerificationRole, Role.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Settings Modification Successful")
                .AddField($"Verification Role", $"<@&{Role.Id}>", true);
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Settings Modification", "New Verification Role", $" <@&{Role.Id}> ");
            return RoWifiResult.FromSuccess();
        }

        [Command("verified"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to set the verified role of the server")]
        public async Task<RuntimeResult> VerifiedAsync([Summary("The Role to set as the Verified Role")]IRole Role)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.VerifiedRole, Role.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Settings Modification Successful")
                .AddField($"Verified Role", $"<@&{Role.Id}>", true);
            await ReplyAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Settings Modification", "New Verified Role", $" <@&{Role.Id}> ");
            return RoWifiResult.FromSuccess();
        }

        [Command("disable-commands"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Alias("disable-cmds")]
        [Summary("Command to disable commands in a channel")]
        public async Task<RuntimeResult> DisableCommandsAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.DisabledChannels != null && guild.DisabledChannels.Contains(Context.Channel.Id))
                return RoWifiResult.FromError("Settings Modification Failed", "Commands have already been disabled in this channel");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.DisabledChannels, Context.Channel.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Settings Modification Successful").WithDescription("Commands have been disabled in this channel successfully");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("enable-commands"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Alias("enable-cmds")]
        [Summary("Command to disable commands in a server")]
        public async Task<RuntimeResult> EnableCommandsAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            if (guild.DisabledChannels == null || !guild.DisabledChannels.Contains(Context.Channel.Id))
                return RoWifiResult.FromError("Settings Modification Failed", "Commands have not been enabled in this channel");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.DisabledChannels, Context.Channel.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Settings Modification Successful").WithDescription("Commands have been disabled in this channel successfully");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("prefix"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        [Summary("Command to change the command prefix of the bot")]
        public async Task<RuntimeResult> SetPrefixAsync([Summary("The key to set as the command prefix")]string Prefix)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.CommandPrefix, Prefix);
            await Database.ModifyGuild(Context.Guild.Id, update);
            Handler.SetPrefix(Context.Guild.Id, Prefix); 

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Settings Modification Successful").WithDescription($"The prefix was successfully changed to {Prefix}");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
