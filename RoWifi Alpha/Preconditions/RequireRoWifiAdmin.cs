using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Preconditions
{
    public class RequireRoWifiAdmin :  PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if(context.User is SocketGuildUser gUser)
            {
                if (gUser.GuildPermissions.Administrator)
                    return PreconditionResult.FromSuccess();
                if (context.Guild.OwnerId == gUser.Id)
                    return PreconditionResult.FromSuccess();
                if (gUser.Roles.Any(r => r.Name == "RoWifi Admin"))
                    return PreconditionResult.FromSuccess();
                if (gUser.Id == 311395138133950465)
                    return PreconditionResult.FromSuccess();
            }
            return PreconditionResult.FromError("You are not authorized to use this command");
        }
    }
}
