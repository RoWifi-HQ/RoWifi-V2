using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class CommandHandler : IHostedService
    {
        private readonly DiscordClient _client;
        private readonly CommandsNextExtension _commands;
        private readonly DatabaseService _database;
        private readonly LoggerService _logger;

        public static Dictionary<ulong, string> Prefixes;

        public CommandHandler(DiscordClient client, CommandsNextExtension commands, LoggerService logger)
        {
            _client = client;
            _commands = commands;
            _database = commands.Services.GetRequiredService<DatabaseService>();
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken token)
        {
            _client.MessageCreated += HandleCommandAsync;
            _commands.CommandErrored += CommandErrored;
            Prefixes = await _database.GetPrefixes();
        }

        private async Task HandleCommandAsync(MessageCreateEventArgs e)
        {
            if (e.Author.IsBot)
                return;

            var message = e.Message;
            var cmdStart = message.GetMentionPrefixLength(_client.CurrentUser);
            if (cmdStart == -1)
                cmdStart = message.GetStringPrefixLength(GetPrefix(e.Guild.Id));
            if (cmdStart == -1)
                return;

            var prefix = message.Content.Substring(0, cmdStart);
            var invocation = message.Content.Substring(cmdStart);

            var cmd = _commands.FindCommand(invocation, out var args);
            if (cmd == null)
                return;

            var context = _commands.CreateContext(message, prefix, cmd, args);

            if (context.Guild != null && message.Content != null && message.Content.Length > 0)
            {
                RoGuild guild = await _database.GetGuild(context.Guild.Id);
                if (guild != null && guild.DisabledChannels != null && guild.DisabledChannels.Contains(context.Channel.Id))
                {
                    if (!message.Content.Contains("commands on"))
                        return;
                }
            }

            _ = Task.Run(async () => await _commands.ExecuteCommandAsync(context));
        }

        public static string GetPrefix(ulong GuildId)
        {
            bool Success = Prefixes.TryGetValue(GuildId, out string Prefix);
            if (!Success) Prefix = "!";
            return Prefix;
        }

        public static void SetPrefix(ulong GuildId, string Prefix)
        {
            Prefixes[GuildId] = Prefix;
        }

        private async Task CommandErrored(CommandErrorEventArgs e)
        {
            if (e.Exception is ChecksFailedException ChecksFailed)
            {
                foreach (var check in ChecksFailed.FailedChecks)
                {
                    if (check is RequireBotPermissionsAttribute BotCheck)
                        await ChecksFailed.Context.RespondAsync($"I seem to be missing one or more of the following permissions: {BotCheck.Permissions.ToPermissionString()}");
                    else if (check is CooldownAttribute cooldown)
                        await ChecksFailed.Context.RespondAsync($"This command is under cooldown for {cooldown.GetRemainingCooldown(ChecksFailed.Context)}");
                }
            }
            else if (e.Exception is CommandException c)
            {
                _ = await e.Context.RespondAsync(embed: c.Embed);
            }
            else if (e.Exception is CommandNotFoundException)
            {
                var content = "help";
                await e.Context.RespondAsync("Invalid Command Usage. Activating help...");
                var cmd = _commands.FindCommand(content, out var args);
                var ctx = _commands.CreateFakeContext(e.Context.User, e.Context.Channel, content, e.Context.Prefix, cmd, args);
                await _commands.ExecuteCommandAsync(ctx);
            }
            else if (e.Exception.GetBaseException() is ArgumentException)
            {
                var content = "help " + e.Command.QualifiedName;
                await e.Context.RespondAsync("Invalid Command Usage. Activating help...");
                var cmd = _commands.FindCommand(content, out var args);
                var ctx = _commands.CreateFakeContext(e.Context.User, e.Context.Channel, content, e.Context.Prefix, cmd, args);
                await _commands.ExecuteCommandAsync(ctx);
            }
            else
            {
                Guid id = Guid.NewGuid();
                DiscordEmbedBuilder embed = Miscellanous.GetDefaultEmbed()
                    .WithColor(DiscordColor.Red)
                    .WithTitle($"Error Id: {id}")
                    .WithDescription("There was an error in running this command. Please contact our support server for further information.");
                await e.Context.RespondAsync(embed: embed.Build());
                var Exception = e.Exception.GetBaseException();
                await _logger.LogDebug($"```Error Id: {id}\nShard Id: {_client.ShardId}\nGuild: {e.Context.Guild.Id}\nCommand: {e.Context.Message}\nException:{Exception.GetType()}\nSource: {Exception.Source}\nMessage: {Exception.Message}\nStack Trace: {e.Exception.StackTrace}```");
            }
        }

        public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
