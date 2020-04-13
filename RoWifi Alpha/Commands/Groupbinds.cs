using Discord;
using Discord.Commands;
using MongoDB.Driver;
using RoWifi_Alpha.Addons.Interactive;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Preconditions;
using RoWifi_Alpha.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Commands
{
    [Group("groupbinds")]
    public class Groupbinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task ViewGroupbindsAsync() 
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild.RankBinds.Count == 0)
            {
                await ReplyAsync("There were no rankbinds found associated with this server. Perhaps you meant to use `rankbinds new`");
                return;
            }

            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            var GroupBindsList = guild.GroupBinds.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
            int Page = 1;
            foreach (List<GroupBind> GBS in GroupBindsList)
            {
                EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                embed.WithTitle("Rankbinds").WithDescription($"Page {Page}");
                foreach (GroupBind bind in GBS)
                    embed.AddField($"Group Id: {bind.GroupId}", $"Roles: { string.Concat(bind.DiscordRoles.Select(r => $"<@&{r}> "))}");
                embeds.Add(embed);
                Page++;
            }
            await PagedReplyAsync(embeds);
        }

        [Command("new"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> NewGroupbindAsync(int GroupId, params IRole[] Roles)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Please ask the server owner to set up this server.");
            if (guild.GroupBinds.Any(g => g.GroupId == GroupId))
                return RoWifiResult.FromError("Bind Addition Failed", $"A bind with {GroupId} as Group Id already exists");
            if (Roles.Length == 0)
                return RoWifiResult.FromError("Bind Addition Failed", "Atleast one role must be mentioned to create a groupbind");

            GroupBind bind = new GroupBind { GroupId = GroupId, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(g => g.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Group: {GroupId}", $"Roles: {string.Concat(bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }

        [Command("delete"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> DeleteAsync(int GroupId)
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Please ask the server owner to set up this server.");

            GroupBind bind = guild.GroupBinds.Where(r => r.GroupId == GroupId).FirstOrDefault();
            if (bind == null)
                return RoWifiResult.FromError("Bind Deletion Failed", $"A bind with {GroupId} as Group Id does not exist");

            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Pull(r => r.GroupBinds, bind);
            await Database.ModifyGuild(Context.Guild.Id, update);

            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Deletion Successful").WithDescription($"The bind with Group Id {GroupId} was successfully deleted");
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
