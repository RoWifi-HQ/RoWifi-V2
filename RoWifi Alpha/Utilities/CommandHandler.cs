using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _services = services;
        }

        public async Task InitializeAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            _commands.CommandExecuted += OnCommandExecutedAsync;
            _commands.Log += LogAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleCommandAsync(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message)) return;
            //Add Disabled Channel and Blacklisted Users Handling
            int argPos = 0;
            if (!(message.HasCharPrefix('?', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) || message.Author.IsBot)
                return;

            SocketCommandContext context = new SocketCommandContext(_client, message);
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
    }
}
