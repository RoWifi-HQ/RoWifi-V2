using Discord;
using Discord.WebSocket;
using MongoDB.Bson.Serialization.Attributes;
using RoWifi_Alpha.Exceptions;
using RoWifi_Alpha.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Models
{
    public class RoUser
    {
        [BsonId]
        [BsonElement("DiscordId")]
        public ulong DiscordId { get; set; }

        [BsonElement("RobloxId")]
        public int RobloxId { get; set; }

        public async Task<(List<ulong>, List<ulong>, string)> UpdateAsync(RobloxService Roblox, IGuild server, RoGuild guild, IGuildUser member, string reason = "Update")
        {
            IRole VerificationRole = server.Roles.Where(r => r != null).Where(r => r.Id == guild.VerificationRole).FirstOrDefault();
            if (VerificationRole != null && member.RoleIds.Contains(VerificationRole.Id))
                await member.RemoveRoleAsync(VerificationRole, new RequestOptions { AuditLogReason = reason });

            IRole VerifiedRole = server.Roles.Where(r => r != null).Where(r => r.Id == guild.VerifiedRole).FirstOrDefault();
            if (VerifiedRole != null && !member.RoleIds.Contains(VerifiedRole.Id))
                await member.AddRoleAsync(VerifiedRole, new RequestOptions { AuditLogReason = reason });

            Dictionary<int, int> userRoleIds = await Roblox.GetUserRoles(RobloxId);
            string RobloxName = await Roblox.GetUsernameFromId(RobloxId);
            List<RankBind> RankBindsToAdd = new List<RankBind>();
            List<GroupBind> GroupBindsToAdd = new List<GroupBind>();
            List<CustomBind> CustomBindsToAdd = new List<CustomBind>();

            RoCommandUser CommandUser = new RoCommandUser(this, member, userRoleIds, RobloxName);

            if (guild.Blacklists != null && guild.Blacklists.Count > 0)
            {
                RoBlacklist Success = guild.Blacklists.Where(b => b.Evaluate(CommandUser)).FirstOrDefault();
                if (Success != null)
                {
                    try
                    {
                        IDMChannel Channel = await member.GetOrCreateDMChannelAsync();
                        await Channel.SendMessageAsync($"You were found on the server blacklist in {server.Name}. Reason: {Success.Reason}");
                    }
                    catch (Exception) { }
                    try 
                    {
                        if (guild.Settings.BlacklistAction == BlacklistActionType.Ban)
                            await server.AddBanAsync(member, reason: "User was found on the server blacklist");
                        else if (guild.Settings.BlacklistAction == BlacklistActionType.Kick)
                            await member.KickAsync("User was found on the server blacklist");
                    } 
                    catch(Exception) { }
                    throw new BlacklistException(Success.Reason);
                }
            }


            foreach (KeyValuePair<int, int> Rank in userRoleIds)
            {
                RankBind rBind = guild.RankBinds.Where(r => r.GroupId == Rank.Key && r.RbxRankId == Rank.Value).FirstOrDefault();
                if (rBind != null)
                    RankBindsToAdd.Add(rBind);
                GroupBind gBind = guild.GroupBinds.Where(g => g.GroupId == Rank.Key).FirstOrDefault();
                if (gBind != null)
                    GroupBindsToAdd.Add(gBind);
            }

            foreach (var Bind in guild.RankBinds.Where(r => r.RbxRankId == 0))
            {
                if (!userRoleIds.ContainsKey(Bind.GroupId))
                    RankBindsToAdd.Add(Bind);
            }

            if(guild.CustomBinds != null)
            {
                foreach (CustomBind bind in guild.CustomBinds)
                {
                    bool Success = bind.Cmd.Evaluate(CommandUser);
                    if (Success)
                        CustomBindsToAdd.Add(bind);
                }
            }

            (List<ulong> AddedRoles, List<ulong> RemovedRoles) = await UpdateBindRolesAsync(member, server, guild, RankBindsToAdd, GroupBindsToAdd, CustomBindsToAdd, reason);
            string DiscNick = member.Nickname ?? member.Username;
            if (!(member as SocketGuildUser).Roles.Where(r => r != null).Any(r => r.Name == "RoWifi Nickname Bypass"))
                DiscNick = await UpdateNicknameAsync(RobloxName, member, RankBindsToAdd, CustomBindsToAdd, reason);

            return (AddedRoles, RemovedRoles, DiscNick);
        }

        private async Task<string> UpdateNicknameAsync(string RobloxName, IGuildUser member, List<RankBind> RankBindsToAdd, List<CustomBind> CustomBindsToAdd, string reason)
        {
            string DiscNick;

            RankBind nickBind = RankBindsToAdd.OrderByDescending(b => b.Priority).FirstOrDefault();
            CustomBind custom = CustomBindsToAdd.OrderByDescending(b => b.Priority).FirstOrDefault();

            string Prefix;
            if (nickBind == null && custom == null)
                Prefix = "N/A";
            else if (nickBind == null)
                Prefix = custom.Prefix;
            else if (custom == null)
                Prefix = nickBind.Prefix;
            else
                Prefix = custom.Priority > nickBind.Priority ? custom.Prefix : nickBind.Prefix;

            if (Prefix.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                DiscNick = RobloxName;
            else if (Prefix.Equals("Disable", StringComparison.OrdinalIgnoreCase))
                DiscNick = member.Nickname;
            else
                DiscNick = Prefix + " " + RobloxName;   
            if (member.Nickname != DiscNick)
                await member.ModifyAsync(m => { m.Nickname = DiscNick; }, new RequestOptions { AuditLogReason = reason});
            return DiscNick;
        }

        private async Task<(List<ulong> AddedRoles, List<ulong> RemovedRoles)> UpdateBindRolesAsync(IGuildUser member, IGuild server, RoGuild guild, List<RankBind> RankBindsToAdd, List<GroupBind> GroupBindsToAdd, List<CustomBind> CustomBindsToAdd, string reason)
        {
            List<IRole> AddedRoles = new List<IRole>();
            List<IRole> RemovedRoles = new List<IRole>();
            List<ulong> RolesToAdd = new List<ulong>();
            RolesToAdd.AddRange(RankBindsToAdd.SelectMany(r => r.DiscordRoles));
            RolesToAdd.AddRange(GroupBindsToAdd.SelectMany(r => r.DiscordRoles));
            RolesToAdd.AddRange(CustomBindsToAdd.SelectMany(r => r.DiscordRoles));

            List<ulong> CurrentRoles = member.RoleIds.ToList();

            foreach (ulong BindRole in guild.GetUniqueRoles())
            {
                IRole Role = server.Roles.Where(r => r != null).Where(r => r.Id == BindRole).FirstOrDefault();
                if (Role != null)
                {
                    if (RolesToAdd.Contains(BindRole) && !CurrentRoles.Contains(BindRole))
                        AddedRoles.Add(Role);
                    else if (!RolesToAdd.Contains(BindRole) && CurrentRoles.Contains(BindRole))
                        RemovedRoles.Add(Role);
                }
            }
            if(AddedRoles.Count > 0)
                await member.AddRolesAsync(AddedRoles, new RequestOptions { AuditLogReason = reason });
            if (RemovedRoles.Count > 0)
            await member.RemoveRolesAsync(RemovedRoles, new RequestOptions { AuditLogReason = reason });

            return (AddedRoles.Select(r => r.Id).ToList(), RemovedRoles.Select(r => r.Id).ToList());
        }
    }

    public enum PremiumType
    {
        Alpha, Beta
    }

    public class Premium
    {
        [BsonId]
        [BsonElement("DiscordId")]
        public ulong DiscordId { get; set; }

        [BsonElement("Type")]
        public PremiumType PType { get; set; }

        [BsonElement("PatreonId")]
        public ulong PatreonId { get; set; }

        [BsonElement("Servers")]
        public List<ulong> DiscordServers { get; set; }
    }
}
