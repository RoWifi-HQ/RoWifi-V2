using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Models
{
    public class RoUser
    {
        [BsonId]
        [BsonElement("DiscordId")]
        public ulong DiscordId { get; set; }

        [BsonElement("RobloxId")]
        public int RobloxId { get; set; }
    }
}
