using Discord;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Models
{
    public class RoBackup
    {
        [BsonId]
        [BsonElement("UserId")]
        public ulong UserId { get; set; }

        [BsonElement("Settings")]
        public GuildSettings Settings { get; set; }

        [BsonElement("VerificationRole")]
        public string VerificationRole { get; set; }

        [BsonElement("VerifiedRole")]
        public string VerifiedRole { get; set; }

        [BsonElement("Rankbinds")]
        public List<BRankBind> Rankbinds { get; set; }

        [BsonElement("Groupbinds")]
        public List<BGroupBind> Groupbinds { get; set; }

        [BsonElement("Custombinds")]
        public List<BCustomBind> Custombinds { get; set; }

        [BsonElement("Blacklists")]
        public List<RoBlacklist> Blacklists { get; set; }
    }

    public class BRankBind
    {
        public int GroupId { get; set; }
        public string[] DiscordRoles { get; set; }
        public int RbxRankId { get; set; }
        public int RbxGrpRoleId { get; set; }
        public string Prefix { get; set; }
        public int Priority { get; set; }

        public BRankBind(RankBind bind, IGuild server)
        {
            GroupId = bind.GroupId;
            RbxGrpRoleId = bind.RbxGrpRoleId;
            RbxRankId = bind.RbxRankId;
            Prefix = bind.Prefix;
            Priority = bind.Priority;
            DiscordRoles = server.Roles.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<RankBind> RestoreAsync(IGuild server)
        {
            RankBind bind = new RankBind
            {
                GroupId = GroupId,
                RbxRankId = RbxRankId,
                RbxGrpRoleId = RbxGrpRoleId,
                Prefix = Prefix,
                Priority = Priority
            };
            List<IRole> roles = new List<IRole>();
            foreach (string RoleName in DiscordRoles)
            {
                IRole role = server.Roles.Where(r => r != null).Where(r => r.Name == RoleName).FirstOrDefault();
                if (role != null)
                    await server.CreateRoleAsync(RoleName, isMentionable: false);
                roles.Add(role);
            }
            bind.DiscordRoles = roles.Select(r => r.Id).ToArray();
            return bind;
        }
    }

    public class BGroupBind
    {
        public int GroupId { get; set; }
        public string[] DiscordRoles { get; set; }

        public BGroupBind(GroupBind bind, IGuild server)
        {
            GroupId = bind.GroupId;
            DiscordRoles = server.Roles.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<GroupBind> RestoreAsync(IGuild server)
        {
            GroupBind bind = new GroupBind { GroupId = GroupId };
            List<IRole> roles = new List<IRole>();
            foreach (string roleName in DiscordRoles)
            {
                IRole role = server.Roles.Where(r => r != null).Where(r => r.Name == roleName).FirstOrDefault();
                if (role == null)
                    role = await server.CreateRoleAsync(roleName, isMentionable: false);
                roles.Add(role);
            }
            bind.DiscordRoles = roles.Select(r => r.Id).ToArray();
            return bind;
        }
    }

    public class BCustomBind
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Prefix { get; set; }
        public int Priority { get; set; }
        public string[] DiscordRoles { get; set; }

        public BCustomBind(CustomBind bind, IGuild server)
        {
            Id = bind.Id;
            Code = bind.Code;
            Prefix = bind.Prefix;
            Priority = bind.Priority;
            DiscordRoles = server.Roles.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<CustomBind> RestoreAsync(IGuild server)
        {
            List<IRole> roles = new List<IRole>();
            foreach (string roleName in DiscordRoles)
            {
                IRole role = server.Roles.Where(r => r != null).Where(r => r.Name == roleName).FirstOrDefault();
                if (role == null)
                    role = await server.CreateRoleAsync(roleName, isMentionable: false);
                roles.Add(role);
            }
            CustomBind bind = new CustomBind(Id, Code, Prefix, Priority, roles.Select(r => r.Id).ToArray());
            return bind;
        }
    }
}
