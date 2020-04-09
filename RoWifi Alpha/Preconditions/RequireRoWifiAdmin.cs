using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Preconditions
{
    public class RequireRoWifiAdmin :  PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if(context.User is SocketGuildUser gUser)
            {
                if (gUser.GuildPermissions.Administrator)
                    return Task.FromResult(PreconditionResult.FromSuccess());
                if (context.Guild.OwnerId == gUser.Id)
                    return Task.FromResult(PreconditionResult.FromSuccess());
                if (gUser.Roles.Any(r => r.Name == "RoWifi Admin"))
                    return Task.FromResult(PreconditionResult.FromSuccess());
                if (gUser.Id == 311395138133950465)
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError("You are not authorized to use this command"));
        }
    }
}
