using Discord;
using RoWifi_Alpha.Addons.Interactive;
using Discord.Commands;
using RoWifi_Alpha.Preconditions;
using System.Threading.Tasks;
using RoWifi_Alpha.Models;
using RoWifi_Alpha.Utilities;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using MongoDB.Driver;

namespace RoWifi_Alpha.Commands
{
    [Group("rankbinds")]
    public class Rankbinds : InteractiveBase<SocketCommandContext>
    {
        public DatabaseService Database { get; set; }
        public RobloxService Roblox { get; set; }

        [Command(RunMode = RunMode.Async), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task ViewRankbindsAsync()
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if (guild.RankBinds.Count == 0)
            {
                await ReplyAsync("There were no rankbinds found associated with this server. Perhaps you meant to use `rankbinds new`");
                return;
            }
            var UniqueGroups = guild.RankBinds.Select(r => r.GroupId).Distinct();
            List<EmbedBuilder> embeds = new List<EmbedBuilder>();
            foreach (int Group in UniqueGroups)
            {
                int Page = 1;
                var RankBinds = guild.RankBinds.Where(r => r.GroupId == Group).OrderBy(r => r.RbxRankId).Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 12).Select(x => x.Select(v => v.Value).ToList());
                foreach (List<RankBind> RBS in RankBinds)
                {
                    EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
                    embed.WithTitle("Rankbinds").WithDescription($"Group: {Group} - Page {Page}");
                    foreach (RankBind Bind in RBS)
                    {
                        embed.AddField($"Rank: {Bind.RbxRankId}", $"Prefix: {Bind.Prefix}\nPriority: {Bind.Priority}\nRoles: {string.Concat(Bind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
                    }
                    embeds.Add(embed);
                    Page++;
                }
            }
            await PagedReplyAsync(embeds); 
        }

        [Command("new"), RequireContext(ContextType.Guild), RequireRoWifiAdmin]
        public async Task<RuntimeResult> NewRankbindAsync(int GroupId, int RankId, string Prefix, int Priority, params IRole[] Roles) 
        {
            RoGuild guild = await Database.GetGuild(Context.Guild.Id);
            if(guild == null)
                return RoWifiResult.FromError("Bind Addition Failed", "Server was not setup. Please ask the server owner to set up this server.");

            if (guild.RankBinds.Exists(r => r.GroupId == GroupId && r.RbxRankId == RankId))
                return RoWifiResult.FromError("Bind Addition Failed", "A bind with the given Group and Rank already exists. Please use `rankbinds modify` to modify rankbinds");

            JToken RankInfo = await Roblox.GetGroupRank(GroupId, RankId);
            if (RankInfo == null)
                return RoWifiResult.FromError("Bind Addition Failed", $"The Rank {RankId} does not exist in Group {GroupId}");

            RankBind NewBind = new RankBind { GroupId = GroupId, RbxRankId = RankId, RbxGrpRoleId = (int)RankInfo["id"], Prefix = Prefix, Priority = Priority, DiscordRoles = Roles.Select(r => r.Id).ToArray() };
            UpdateDefinition<RoGuild> update = Builders<RoGuild>.Update.Push(r => r.RankBinds, NewBind);
            await Database.ModifyGuild(Context.Guild.Id, update);
            EmbedBuilder embed = Miscellanous.GetDefaultEmbed();
            embed.WithColor(Color.Green).WithTitle("Bind Addition Successful")
                .AddField($"Rank: {NewBind.RbxRankId}", $"Prefix: {NewBind.Prefix}\nPriority: {NewBind.Priority}\nRoles: {string.Concat(NewBind.DiscordRoles.Select(r => $" <@&{ r}> "))}", true);
            await ReplyAsync(embed: embed.Build());
            return RoWifiResult.FromSuccess();
        }
    }
}
