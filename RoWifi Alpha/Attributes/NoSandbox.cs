using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NoSandbox : CheckBaseAttribute
    {
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            if (ctx.Guild == null)
                return Task.FromResult(false);

            if (ctx.Guild.Id != 721656828475342880)
                return Task.FromResult(true);

            if (ctx.Guild.Owner.Id == ctx.User.Id)
                return Task.FromResult(true);

            if (ctx.Client.CurrentApplication.Owners.Contains(ctx.User))
                return Task.FromResult(true);

            if (ctx.Member.Roles.Where(r => r.CheckPermission(Permissions.Administrator) == PermissionLevel.Allowed).Any())
                return Task.FromResult(true);

            return Task.FromResult(false);
        }
    }
}
