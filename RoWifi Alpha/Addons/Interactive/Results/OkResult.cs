using Discord.Commands;

namespace RoWifi_Alpha.Addons.Interactive
{
    public class OkResult : RuntimeResult
    {
        public OkResult(string reason = null) : base(null, reason) { }
    }
}
