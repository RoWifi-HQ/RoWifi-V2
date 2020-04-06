using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Utilities
{
    public class RoWifiResult : RuntimeResult
    {
        public RoWifiResult(CommandError? error, string reason) : base(error, reason)
        {
        }

        public static RoWifiResult FromError(string reason) =>
            new RoWifiResult(CommandError.Unsuccessful, reason);
        public static RoWifiResult FromSuccess(string reason = null) =>
            new RoWifiResult(null, reason);

        public static RoWifiResult FromRobloxError() =>
            new RoWifiResult(CommandError.Exception, "There seems to be an error while interacting with the Roblox API. Please try again in a few minutes");

        public static RoWifiResult FromMongoError() =>
            new RoWifiResult(CommandError.Exception, "There was a problem in saving to the database. Pleas contact @Gautam.A#9539 immediately.");
    }
}
