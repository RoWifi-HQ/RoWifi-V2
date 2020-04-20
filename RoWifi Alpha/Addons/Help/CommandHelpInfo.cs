using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Text;

namespace RoWifi_Alpha.Addons.Help
{
    public class CommandHelpInfo
    {
        public CommandInfo CommandInformation { get; set; }

        public CommandHelpInfo(CommandInfo C)
        {
            CommandInformation = C;
        }

        public List<EmbedFieldBuilder> BuildInfo()
        {
            List<EmbedFieldBuilder> Fields = new List<EmbedFieldBuilder>(); 
            Fields.Add(new EmbedFieldBuilder().WithName("Parameters").WithValue(BuildParameters()));

            if (CommandInformation.Module.Group != null && CommandInformation.Module.Name == CommandInformation.Aliases[0]) 
                Fields.Add(new EmbedFieldBuilder().WithName("Subcommands").WithValue(BuildSubCommands()));

            var Aliases = BuildAliases();
            if (Aliases.Length > 0)
                Fields.Add(new EmbedFieldBuilder().WithName("Aliases").WithValue(BuildAliases()));
            return Fields;
        }

        private string BuildParameters()
        {
            var Parameters = new StringBuilder();
            foreach (var param in CommandInformation.Parameters)
            {
                Parameters.AppendLine($"`{param.Name} ({param.Type.Name})`: " + $"{(param.IsOptional ? "[Optional]" : "")}" +
                    $"{param.Summary ?? "No description provided"}");
            }

            if (Parameters.Length == 0)
                Parameters = new StringBuilder($"`{CommandInformation.Module.Name}`");

            return Parameters.ToString();
        }

        private string BuildSubCommands()
        {
            string SubCommands = "";
            foreach (var sub in CommandInformation.Module.Commands)
            {
                if (sub.Name == CommandInformation.Name) continue;
                SubCommands += $"`{sub.Name}` ";
            }
            return SubCommands;
        }

        private string BuildAliases()
        {
            string Aliases = "";
            foreach (var sub in CommandInformation.Aliases)
            {
                Aliases += $"`{sub}` ";
            }
            return Aliases;
        }
    }
}
