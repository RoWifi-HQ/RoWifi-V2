using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Utilities
{
    public static class Miscellanous
    {
        private static readonly string[] Codes = new string[] { "cat", "dog", "sun", "rain", "snow", "alcazar", "dight", "night", "morning", "eyewater", "flaws", "physics", "chemistry", "history", "martlet", "nagware", "coffee", "tea", "red", "blue", "green", "orange", "pink" };
        public static string GenerateCode()
        {
            Random rand = new Random();
            string code1 = Codes[rand.Next(0, Codes.Length)];
            string code2 = Codes[rand.Next(0, Codes.Length)];
            string code3 = Codes[rand.Next(0, Codes.Length)];
            return code1 + " " + code2 + " " + code3;
        }

        public static EmbedBuilder GetDefaultEmbed()
        {
            EmbedBuilder embed = new EmbedBuilder
            {
                Timestamp = DateTime.Now,
                Color = Color.Blue,
            };
            embed.WithFooter("RoWifi");
            return embed;
        }
    }
}
