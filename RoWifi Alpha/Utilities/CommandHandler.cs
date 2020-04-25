using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Addons.Help;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class CommandHandler : InitializedService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly DatabaseService _database;
        private readonly LoggerService _logger;

        private Dictionary<ulong, string> Prefixes;

        public CommandHandler(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _services = services;
            _database = services.GetRequiredService<DatabaseService>();
            _logger = services.GetRequiredService<LoggerService>();
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            _client.MessageReceived += HandleCommandAsync;
            _commands.CommandExecuted += OnCommandExecutedAsync;
            _commands.Log += LogAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            Prefixes = await _database.GetPrefixes();
        }

        private async Task HandleCommandAsync(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message)) return;

            SocketCommandContext context = new SocketCommandContext(_client, message);

            int argPos = 0;
            if (!(message.HasStringPrefix(GetPrefix(context.Guild.Id), ref argPos) 
                    || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) || message.Author.IsBot)
                return;

            if(context.Guild != null)
            {
                RoGuild guild = await _database.GetGuild(context.Guild.Id);
                if (guild.DisabledChannels != null && guild.DisabledChannels.Contains(context.Channel.Id) 
                    && !(message.Content.Contains("enable-commands") || message.Content.Contains("enable-cmds")))
                    return;
            }

            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result is RoWifiResult res)
            {
                if (res.embed != null)
                    await context.Channel.SendMessageAsync(embed: res.embed);
            }
            else if (result.Error.HasValue)
            {
                var err = result.Error.Value;
                switch(err)
                {
                    case CommandError.BadArgCount:
                    {
                        var help  = context.Message.Content.Replace(GetPrefix(context.Guild.Id), "");
                        var helpEmbed = _commands.GetDefaultEmbed(help);
                        await context.Channel.SendMessageAsync("Invalid Command Usage. Activating help...", embed: helpEmbed);
                        break;
                    }
                    case CommandError.Exception:
                    case CommandError.UnknownCommand:
                        break;
                    default:
                        await context.Channel.SendMessageAsync(result.ErrorReason);
                        break;
                }
            }
        }

        private async Task LogAsync(LogMessage logMessage)
        {
            if (logMessage.Exception is CommandException cmdException)
            {
                if (cmdException.InnerException is RoMongoException)
                    await cmdException.Context.Channel.SendMessageAsync("There was a problem in saving to the database. Pleas contact @Gautam.A#9539 immediately.");
                else if (cmdException.InnerException is RobloxException)
                    await cmdException.Context.Channel.SendMessageAsync("There seems to be an error while interacting with the Roblox API. Please try again in a few minutes");
                else
                {
                    await cmdException.Context.Channel.SendMessageAsync("Something went catastrophically wrong! Please contact our support server immediately");
                    await _logger.LogDebug($"`Message: {cmdException.InnerException.Message}\n" +
                        $"Command: {cmdException.Context.Message.Content}\n" +
                        $"Source: {cmdException.InnerException.Source}\n" +
                        $"Stack Trace: {cmdException.InnerException.StackTrace}");
                }
            }
        }

        public string GetPrefix(ulong GuildId)
        {
            bool Success = Prefixes.TryGetValue(GuildId, out string Prefix);
            if (!Success) Prefix = "!";
            return Prefix;
        }

        public void SetPrefix(ulong GuildId, string Prefix)
        {
            Prefixes[GuildId] = Prefix;
        }
    }
}
