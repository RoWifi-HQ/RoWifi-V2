using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MongoDB.Driver;
using RoWifi_Alpha.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("settings")]
    [RequireBotPermissions(Permissions.EmbedLinks)]
    [Description("Command to access settings of a server")]
    public class Settings : BaseCommandModule
    {
        public DatabaseService Database { get; set; }
        public LoggerService Logger { get; set; }

        [GroupCommand, RequireGuild, RequireRoWifiAdmin]
        [Description("Command to view settings of a server")]
        public async Task GroupCommand(CommandContext Context)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Viewing Failed", "Server was not setup. Please ask the server owner to set up this server.");
            string Tier = "Normal";
            if (guild.Settings.Type == GuildType.Alpha) Tier = "Alpha";
            if (guild.Settings.Type == GuildType.Beta) Tier = "Beta";

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.AddField("Tier", Tier, true)
                .AddField("Auto Detection", guild.Settings.AutoDetection.ToString(), true)
                .AddField("Blacklist Action", guild.Settings.BlacklistAction.ToString(), true)
                .AddField("Update On Join", guild.Settings.UpdateOnJoin.ToString(), true)
                .AddField("Update On Verify", guild.Settings.UpdateOnVerify.ToString(), true);
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("verification"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to set the unverified role of the server")]
        public async Task VerificationAsync(CommandContext Context, [Description("The role to set as the unverified role")]DiscordRole Role)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modificatione Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.VerificationRole, Role.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful")
                .AddField($"Verification Role", $"<@&{Role.Id}>", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Settings Modification", "New Verification Role", $" <@&{Role.Id}> ");
        }

        [Command("verified"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to set the verified role of the server")]
        public async Task VerifiedAsync(CommandContext Context, [Description("The Role to set as the Verified Role")]DiscordRole Role)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.VerifiedRole, Role.Id);
            await Database.ModifyGuild(Context.Guild.Id, update);
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful")
                .AddField($"Verified Role", $"<@&{Role.Id}>", true);
            await Context.RespondAsync(embed: embed.Build());
            await Logger.LogAction(Context.Guild, Context.User, "Settings Modification", "New Verified Role", $" <@&{Role.Id}> ");
        }

        [Command("commands"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to toggle enabling/disabling commands in a channel. Options: `on` `off`")]
        public async Task CommandsToggleAsync(CommandContext Context, [Description("The keyword to disable/enable commands")]string option)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            if (option.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                if (guild.DisabledChannels != null && guild.DisabledChannels.Contains(Context.Channel.Id))
                    throw new CommandException("Settings Modification Failed", "Commands have already been disabled in this channel");

                UpdateDefinition<RoGuild> update;
                if (guild.DisabledChannels == null)
                    update = Builders<RoGuild>.Update.Set(g => g.DisabledChannels, new List<ulong>() { Context.Channel.Id });
                else
                    update = Builders<RoGuild>.Update.Push(g => g.DisabledChannels, Context.Channel.Id);
                await Database.ModifyGuild(Context.Guild.Id, update);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Commands have been disabled in this channel successfully");
            }
            else if (option.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                if (guild.DisabledChannels == null || !guild.DisabledChannels.Contains(Context.Channel.Id))
                    throw new CommandException("Settings Modification Failed", "Commands have not been enabled in this channel");

                UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(g => g.DisabledChannels, Context.Channel.Id);
                await Database.ModifyGuild(Context.Guild.Id, update);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Commands have been enabled in this channel successfully");
            }
            else
                embed.WithColor(DiscordColor.Red).WithTitle("Settings Modification Failed").WithDescription("Invalid Option selected");
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("blacklist-action"), RequireGuild, RequireRoWifiAdmin]
        [Aliases("bl-action")]
        [Description("Command to set the blacklist action. Options: `None` `Kick` `Ban`")]
        public async Task SetBlacklistActionAsync(CommandContext Context, string option)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            UpdateDefinition<RoGuild> update = null;
            if (option.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.BlacklistAction, BlacklistActionType.None);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Blacklist Action was set to `None`");
            }
            else if (option.Equals("kick", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.BlacklistAction, BlacklistActionType.Kick);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Blacklist Action was set to `Kick`");
            }
            else if (option.Equals("ban", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.BlacklistAction, BlacklistActionType.Ban);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Blacklist Action was set to `Ban`");
            }
            else
                embed.WithColor(DiscordColor.Red).WithTitle("Settings Modification Failed").WithDescription("Invalid Option selected");
            if (update != null)
                await Database.ModifyGuild(Context.Guild.Id, update);
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("update-on-join"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to toggle the `Update On Join` setting")]
        public async Task SetUpdateOnJoinAsync(CommandContext Context, [Description("Choices: on/off")]string Option)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            UpdateDefinition<RoGuild> update = null;
            if (Option.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.UpdateOnJoin, true);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Update On Join was enabled");
            }
            else if (Option.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.UpdateOnJoin, false);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Update On Join was disabled");
            }
            else
                embed.WithColor(DiscordColor.Red).WithTitle("Settings Modification Failed").WithDescription("Invalid Option selected");
            if (update != null)
                await Database.ModifyGuild(Context.Guild.Id, update);
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("update-on-verify"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to toggle the `Update On Join` setting")]
        public async Task SetUpdateOnVerifyAsync(CommandContext Context, [Description("Choices: on/off")] string Option)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");
            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            UpdateDefinition<RoGuild> update = null;
            if (Option.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.UpdateOnVerify, true);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Update On Verify was enabled");
            }
            else if (Option.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                update = Builders<RoGuild>.Update.Set(g => g.Settings.UpdateOnVerify, false);
                embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription("Update On Verify was disabled");
            }
            else
                embed.WithColor(DiscordColor.Red).WithTitle("Settings Modification Failed").WithDescription("Invalid Option selected");
            if (update != null)
                await Database.ModifyGuild(Context.Guild.Id, update);
            await Context.RespondAsync(embed: embed.Build());
        }

        [Command("prefix"), RequireGuild, RequireRoWifiAdmin]
        [Description("Command to change the command prefix of the bot")]
        public async Task SetPrefixAsync(CommandContext Context, [Description("The key to set as the command prefix")]string Prefix)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                throw new CommandException("Settings Modification Failed", "Server was not setup. Please ask the server owner to set up this server.");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Set(g => g.CommandPrefix, Prefix);
            await Database.ModifyGuild(Context.Guild.Id, update);
            CommandHandler.SetPrefix(Context.Guild.Id, Prefix); 

            DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(DiscordColor.Green).WithTitle("Settings Modification Successful").WithDescription($"The prefix was successfully changed to {Prefix}");
            await Context.RespondAsync(embed: embed.Build());
        }
    }
}
