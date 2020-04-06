namespace RoWifi_Alpha.Models
{
    public class RankBind
    {
        public int GroupId { get; set; }
        public ulong[] DiscordRoles { get; set; }
        public int RbxRankId { get; set; }
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
    }
}
