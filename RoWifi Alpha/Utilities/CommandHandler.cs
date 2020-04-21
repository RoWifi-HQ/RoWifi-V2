using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoWifi_Alpha.Models;
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
        private Dictionary<ulong, string> Prefixes;

        public CommandHandler(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _services = services;
            _database = services.GetRequiredService<DatabaseService>();
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
                if (guild.DisabledChannels != null && guild.DisabledChannels.Contains(context.Channel.Id))
                    return;
            }

            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            switch (result)
            {
                case RoWifiResult res:
                    if (res.Error != null)
                        await context.Channel.SendMessageAsync(embed: res.embed);
                    break;
                default:
                    if (!string.IsNullOrEmpty(result.ErrorReason))
                        await context.Channel.SendMessageAsync(result.ErrorReason);
                    break;
            }
        }

        private async Task LogAsync(LogMessage logMessage)
        {
            if (logMessage.Exception is CommandException cmdException)
            {
                await cmdException.Context.Channel.SendMessageAsync("Something went catastrophically wrong!");
            }
        }

        public string GetPrefix(ulong GuildId)
        {
            bool Success = Prefixes.TryGetValue(GuildId, out string Prefix);
            if (!Success) Prefix = "?";
            return Prefix;
        }

        public void SetPrefix(ulong GuildId, string Prefix)
        {
            Prefixes[GuildId] = Prefix;
        }
    }
}
