using Discord;
using Discord.Commands;

namespace RoWifi_Alpha.Utilities
{
    public class RoWifiResult : RuntimeResult
    {
        public Embed embed;
        public RoWifiResult(CommandError? error, string reason, string description, Color color)  : base(error, reason) 
        {
            if(reason != null && description != null)
            {
                embed = Miscellanous.GetDefaultEmbed().WithTitle(reason).WithDescription(description).WithColor(color).Build();
            }
        }

        public static RoWifiResult FromError(string reason, string description) =>
            new RoWifiResult(CommandError.Unsuccessful, reason, description, Color.DarkRed);
        public static RoWifiResult FromSuccess(string reason = null, string description = null) =>
            new RoWifiResult(null, reason, description, Color.Green);

        public static RoWifiResult FromRobloxError(string reason) =>
            new RoWifiResult(CommandError.Exception, reason, "There seems to be an error while interacting with the Roblox API. Please try again in a few minutes", Color.DarkRed);

        public static RoWifiResult FromMongoError(string reason) =>
            new RoWifiResult(CommandError.Exception, reason, "There was a problem in saving to the database. Pleas contact @Gautam.A#9539 immediately.", Color.DarkRed);
    }
}
