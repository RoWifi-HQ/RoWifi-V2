using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace RoWifi_Alpha.Models
{
    public class RoGuild
    {
        /// <summary>
        /// The ID of the Discord Guild linked
        /// </summary>
        [BsonId]
        [BsonElement("GuildId")]
        public ulong GuildId { get; set; }

        [BsonElement("Settings")]
        /// <summary>
        /// The settings of the guild
        /// </summary>
        public GuildSettings Settings { get; set; }

        [BsonElement("VerificationRole")]
        /// <summary>
        /// The ID of the Verification Role
        /// </summary>
        public ulong VerificationRole { get; set; }

        [BsonElement("VerifiedRole")]
        /// <summary>
        /// The ID of the Verified Role
        /// </summary>
        public ulong VerifiedRole { get; set; }

        /// <summary>
        /// The List of Rankbinds linked to this Guild
        /// </summary>
        [BsonElement("RankBinds")]
        public List<RankBind> RankBinds { get; set; }

        /// <summary>
        /// The List of GroupBinds linked to this Guild
        /// </summary>
        [BsonElement("GroupBinds")]
        public List<GroupBind> GroupBinds { get; set; }

        [BsonElement("CustomBinds")]
        public List<CustomBind> CustomBinds { get; set; }

        /// <summary>
        /// Object holding blacklists of the server
        /// </summary>
        [BsonElement("Blacklists")]
        public List<RoBlacklist> Blacklists { get; set; }

        /// <summary>
        /// List of Channels in which Commands are Disabled
        /// </summary>
        [BsonElement("DisabledChannels")]
        public List<ulong> DisabledChannels { get; set; }

        public List<ulong> GetUniqueRoles()
        {
            var AllBindsEnumerable = RankBinds.Select(r => r.DiscordRoles).ToList();
            AllBindsEnumerable.AddRange(GroupBinds.Select(r => r.DiscordRoles));
            if (CustomBinds != null)
                AllBindsEnumerable.AddRange(CustomBinds.Select(r => r.DiscordRoles));
            List<ulong> AllRoles = new List<ulong>();
            foreach (ulong[] Binds in AllBindsEnumerable)
                AllRoles.AddRange(Binds);
            return AllRoles.Distinct().ToList();
        }
    }

    public enum GuildType
    {
        Alpha, Beta, Normal
    }

    public class GuildSettings
    {
        public bool AutoDetection { get; set; }
        public GuildType Type { get; set; }
    }

    public enum BlacklistType
    {
        Roblox, Discord
    }
    public class RoBlacklist
    {
        public string Id { get; set; }
        public string Reason { get; set; }
        public BlacklistType Type { get; set; }
    }
}
