﻿using MongoDB.Bson.Serialization.Attributes;

namespace RoWifi_Alpha.Models
{
    public class RankBind
    {
        public int GroupId { get; set; }
        public ulong[] DiscordRoles { get; set; }
        /// <summary>
        /// The ID inside the group (0-255)
        /// </summary>
        public int RbxRankId { get; set; }
        /// <summary>
        /// The Global ID given by Roblox
        /// </summary>
        public int RbxGrpRoleId { get; set; }
        public string Prefix { get; set; }
        public int Priority { get; set; }
    }

    public class GroupBind
    {
        public int GroupId { get; set; }
        public ulong[] DiscordRoles { get; set; }
    }

    public class CustomBind
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Prefix { get; set; }
        public int Priority { get; set; }
        public ulong[] DiscordRoles { get; set; }

        [BsonIgnore]
        public RoCommand Cmd;

        [BsonConstructor]
        public CustomBind(int Id, string Code, string Prefix, int Priority, ulong[] DiscordRoles)
        {
            this.Id = Id;
            this.Code = Code;
            this.Prefix = Prefix;
            this.Priority = Priority;
            this.DiscordRoles = DiscordRoles;
            this.Cmd = new RoCommand(Code);
        }
    }

    public class AssetBind
    {
        public ulong Id { get; set; }

        public AssetType Type { get; set; }

        public ulong[] DiscordRoles { get; set; }
    }

    public enum AssetType
    {
        Asset, Badge, Gamepass
    }
}
