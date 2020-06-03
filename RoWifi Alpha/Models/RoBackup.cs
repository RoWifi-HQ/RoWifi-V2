using DSharpPlus.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Models
{
    public class RoBackup
    {
        public ObjectId Id { get; set; }

        [BsonElement("UserId")]
        public ulong UserId { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; }

        [BsonElement("Prefix")]
        public string CommandPrefix { get; set; }

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

        public RoBackup(ulong userId, string Name, RoGuild guild, DiscordGuild server)
        {
            Id = new ObjectId();
            UserId = userId;
            this.Name = Name;
            CommandPrefix = guild.CommandPrefix;
            Settings = guild.Settings;
            VerificationRole = server.Roles.Values.Where(r => r != null).Where(r => r.Id == guild.VerificationRole).Select(r => r.Name).FirstOrDefault();
            VerifiedRole = server.Roles.Values.Where(r => r != null).Where(r => r.Id == guild.VerifiedRole).Select(r => r.Name).FirstOrDefault();
            Rankbinds = guild.RankBinds.Select(r => new BRankBind(r, server)).ToList();
            Groupbinds = guild.GroupBinds.Select(g => new BGroupBind(g, server)).ToList();
            if (guild.CustomBinds != null)
                Custombinds = guild.CustomBinds.Select(g => new BCustomBind(g, server)).ToList();
            if (guild.Blacklists != null)
                Blacklists = guild.Blacklists;
        }

        public async Task<RoGuild> RestoreAsync(DiscordGuild server)
        {
            RoGuild guild = new RoGuild(server.Id);
            //Restore Verification Role
            DiscordRole role = server.Roles.Values.Where(r => r != null).Where(r => r.Name == VerificationRole).FirstOrDefault();
            if (role == null)
                role = await server.CreateRoleAsync(VerificationRole, mentionable: false);
            guild.VerificationRole = role.Id;
            //Restore Verified Role
            DiscordRole role2 = server.Roles.Values.Where(r => r != null).Where(r => r.Name == VerifiedRole).FirstOrDefault();
            if (role2 == null)
                role2 = await server.CreateRoleAsync(VerifiedRole, mentionable: false);
            guild.VerifiedRole = role2.Id;
            //Restore Rankbinds
            foreach (BRankBind bind in Rankbinds)
                guild.RankBinds.Add(await bind.RestoreAsync(server));
            //Restore Groupbinds
            foreach (BGroupBind bind in Groupbinds)
                guild.GroupBinds.Add(await bind.RestoreAsync(server));
            foreach (BCustomBind bind in Custombinds)
                guild.CustomBinds.Add(await bind.RestoreAsync(server));
            guild.Blacklists = Blacklists;
            guild.Settings = Settings;
            guild.CommandPrefix = CommandPrefix;
            return guild;
        }
    }

    public class BRankBind
    {
        public int GroupId { get; set; }
        public string[] DiscordRoles { get; set; }
        public int RbxRankId { get; set; }
        public int RbxGrpRoleId { get; set; }
        public string Prefix { get; set; }
        public int Priority { get; set; }

        public BRankBind(RankBind bind, DiscordGuild server)
        {
            GroupId = bind.GroupId;
            RbxGrpRoleId = bind.RbxGrpRoleId;
            RbxRankId = bind.RbxRankId;
            Prefix = bind.Prefix;
            Priority = bind.Priority;
            DiscordRoles = server.Roles.Values.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<RankBind> RestoreAsync(DiscordGuild server)
        {
            RankBind bind = new RankBind
            {
                GroupId = GroupId,
                RbxRankId = RbxRankId,
                RbxGrpRoleId = RbxGrpRoleId,
                Prefix = Prefix,
                Priority = Priority
            };
            List<DiscordRole> roles = new List<DiscordRole>();
            foreach (string RoleName in DiscordRoles)
            {
                DiscordRole role = server.Roles.Values.Where(r => r != null).Where(r => r.Name == RoleName).FirstOrDefault();
                if (role == null)
                    await server.CreateRoleAsync(RoleName, mentionable: false);
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

        public BGroupBind(GroupBind bind, DiscordGuild server)
        {
            GroupId = bind.GroupId;
            DiscordRoles = server.Roles.Values.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<GroupBind> RestoreAsync(DiscordGuild server)
        {
            GroupBind bind = new GroupBind { GroupId = GroupId };
            List<DiscordRole> roles = new List<DiscordRole>();
            foreach (string roleName in DiscordRoles)
            {
                DiscordRole role = server.Roles.Values.Where(r => r != null).Where(r => r.Name == roleName).FirstOrDefault();
                if (role == null)
                    role = await server.CreateRoleAsync(roleName, mentionable: false);
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

        public BCustomBind(CustomBind bind, DiscordGuild server)
        {
            Id = bind.Id;
            Code = bind.Code;
            Prefix = bind.Prefix;
            Priority = bind.Priority;
            DiscordRoles = server.Roles.Values.Where(r => r != null).Where(r => bind.DiscordRoles.Contains(r.Id)).Select(r => r.Name).ToArray();
        }

        public async Task<CustomBind> RestoreAsync(DiscordGuild server)
        {
            List<DiscordRole> roles = new List<DiscordRole>();
            foreach (string roleName in DiscordRoles)
            {
                DiscordRole role = server.Roles.Values.Where(r => r != null).Where(r => r.Name == roleName).FirstOrDefault();
                if (role == null)
                    role = await server.CreateRoleAsync(roleName, mentionable: false);
                roles.Add(role);
            }
            CustomBind bind = new CustomBind(Id, Code, Prefix, Priority, roles.Select(r => r.Id).ToArray());
            return bind;
        }
    }
}
