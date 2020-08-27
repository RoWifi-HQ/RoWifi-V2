using DSharpPlus.Entities;
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

        public async Task<(List<ulong>, List<ulong>, string)> UpdateAsync(RobloxService Roblox, DiscordGuild server, RoGuild guild, DiscordMember member, string reason = "Update")
        {
            List<DiscordRole> Roles = member.Roles.ToList() ?? new List<DiscordRole>();
            bool VSuccess = server.Roles.TryGetValue(guild.VerificationRole, out DiscordRole VerificationRole);
            if (VSuccess && Roles.Contains(VerificationRole))
                await member.RevokeRoleAsync(VerificationRole, reason);

            VSuccess = server.Roles.TryGetValue(guild.VerifiedRole, out DiscordRole VerifiedRole);
            if (VSuccess && !Roles.Contains(VerifiedRole))
                await member.GrantRoleAsync(VerifiedRole, reason);

            Dictionary<int, int> userRoleIds = await Roblox.GetUserRoles(RobloxId);
            string RobloxName = await Roblox.GetUsernameFromId(RobloxId);
            List<RankBind> RankBindsToAdd = new List<RankBind>();
            List<GroupBind> GroupBindsToAdd = new List<GroupBind>();
            List<CustomBind> CustomBindsToAdd = new List<CustomBind>();
            List<AssetBind> AssetBindsToAdd = new List<AssetBind>();

            RoCommandUser CommandUser = new RoCommandUser(this, member, userRoleIds, RobloxName);

            if (guild.Blacklists != null && guild.Blacklists.Count > 0)
            {
                RoBlacklist Success = guild.Blacklists.Where(b => b.Evaluate(CommandUser)).FirstOrDefault();
                if (Success != null)
                {
                    try
                    {
                        if (reason == "Update")
                        {
                            DiscordDmChannel Channel = await member.CreateDmChannelAsync();
                            await Channel.SendMessageAsync($"You were found on a server blacklist. Reason: {Success.Reason}");
                        }
                    }
                    catch (Exception) { }
                    try 
                    {
                        if (guild.Settings.BlacklistAction == BlacklistActionType.Ban)
                            await server.BanMemberAsync(member, reason: "User was found on the server blacklist");
                        else if (guild.Settings.BlacklistAction == BlacklistActionType.Kick)
                            await member.RemoveAsync("User was found on the server blacklist");
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

            if (guild.AssetBinds != null)
            {
                foreach (AssetBind bind in guild.AssetBinds)
                {
                    bool Success = await Roblox.HasAsset(RobloxId, bind.Id, bind.Type.ToString());
                    if (Success)
                        AssetBindsToAdd.Add(bind);
                }
            }

            (List<ulong> AddedRoles, List<ulong> RemovedRoles) = await UpdateBindRolesAsync(member, server, guild, RankBindsToAdd, GroupBindsToAdd, CustomBindsToAdd, AssetBindsToAdd, reason);
            string DiscNick = member.DisplayName;
            if (!Roles.Where(r => r != null).Any(r => r.Name == "RoWifi Nickname Bypass"))
                DiscNick = await UpdateNicknameAsync(RobloxName, member, RankBindsToAdd, CustomBindsToAdd, reason);

            return (AddedRoles, RemovedRoles, DiscNick);
        }

        private async Task<string> UpdateNicknameAsync(string RobloxName, DiscordMember member, List<RankBind> RankBindsToAdd, List<CustomBind> CustomBindsToAdd, string reason = "Update")
        {
            RankBind nickBind = RankBindsToAdd.OrderByDescending(b => b.Priority).FirstOrDefault();
            CustomBind custom = CustomBindsToAdd.OrderByDescending(b => b.Priority).FirstOrDefault();

            string Prefix = "N/A";
            if (nickBind == null && custom == null)
                Prefix = "N/A";
            else if (nickBind == null)
                Prefix = custom.Prefix;
            else if (custom == null)
                Prefix = nickBind.Prefix;
            else
                Prefix = custom.Priority > nickBind.Priority ? custom.Prefix : nickBind.Prefix;

            string DiscNick = member.DisplayName;
            if (Prefix.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                DiscNick = RobloxName;
            else if (Prefix.Equals("Disable", StringComparison.OrdinalIgnoreCase))
                return DiscNick;
            else
                DiscNick = Prefix + " " + RobloxName;
            if (DiscNick != null && DiscNick.Length > 32)
                throw new CommandException("Update Failed", $"Your supposed nickname `{DiscNick}` was found out to be more than 32 characters");
            if (DiscNick != null && member.DisplayName != DiscNick)
                await member.ModifyAsync(m => { m.Nickname = DiscNick; m.AuditLogReason = reason; });
            return DiscNick;
        }

        private async Task<(List<ulong> AddedRoles, List<ulong> RemovedRoles)> UpdateBindRolesAsync(DiscordMember member, DiscordGuild server,
            RoGuild guild, List<RankBind> RankBindsToAdd, List<GroupBind> GroupBindsToAdd, 
            List<CustomBind> CustomBindsToAdd, List<AssetBind> AssetBindsToAdd, string reason)
        {
            List<DiscordRole> AddedRoles = new List<DiscordRole>();
            List<DiscordRole> RemovedRoles = new List<DiscordRole>();
            List<ulong> RolesToAdd = new List<ulong>();
            RolesToAdd.AddRange(RankBindsToAdd.SelectMany(r => r.DiscordRoles));
            RolesToAdd.AddRange(GroupBindsToAdd.SelectMany(r => r.DiscordRoles));
            RolesToAdd.AddRange(CustomBindsToAdd.SelectMany(r => r.DiscordRoles));
            RolesToAdd.AddRange(AssetBindsToAdd.SelectMany(r => r.DiscordRoles));

            List<DiscordRole> CurrentRoles = member.Roles.ToList() ?? new List<DiscordRole>();

            foreach (ulong BindRole in guild.GetUniqueRoles())
            {
                bool Success = server.Roles.TryGetValue(BindRole, out DiscordRole Role);
                if (Success)
                {
                    if (RolesToAdd.Contains(BindRole) && !CurrentRoles.Contains(Role))
                    {
                        await member.GrantRoleAsync(Role, reason);
                        AddedRoles.Add(Role);
                    }
                    else if (!RolesToAdd.Contains(BindRole) && CurrentRoles.Contains(Role))
                    {
                        await member.RevokeRoleAsync(Role, reason);
                        RemovedRoles.Add(Role);
                    }
                }
            }
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

    public class QueueUser
    {
        [BsonId]
        [BsonElement("RobloxId")]
        public int RobloxId { get; set; }
        
        [BsonElement("DiscordId")]
        public ulong DiscordId { get; set; }

        /// <summary>
        /// This field gives infomation whether the user is already verified or not
        /// </summary>
        [BsonElement("Verified")]
        public bool Verified { get; set; }
    }
}
