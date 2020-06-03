using DSharpPlus.Entities;
using RoWifi_Alpha.Utilities;
using System;

namespace RoWifi_Alpha.Exceptions
{
    public class CommandException : Exception
    {
        public DiscordEmbed Embed;
        public CommandException() { }
       
        public CommandException(string reason, string description) : base(reason)
        {
            if (reason != null && description != null)
            {
                Embed = Miscellanous.GetDefaultEmbed()
                    .WithTitle(reason)
                    .WithDescription(description)
                    .WithColor(DiscordColor.Red)
                    .Build();
            }
        }
    }
}
