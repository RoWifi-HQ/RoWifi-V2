using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoWifi_Alpha.Utilities
{
    public class HelpFormatter : BaseHelpFormatter
    {
        public DiscordEmbedBuilder EmbedBuilder { get; }
        private Command Command { get; set; }

        public HelpFormatter(CommandContext Context) : base(Context)
        {
            EmbedBuilder = new DiscordEmbedBuilder()
               .WithTitle("Help")
               .WithColor(0x007FFF);
        }

        public override CommandHelpMessage Build()
        {
            if (Command == null)
                EmbedBuilder.WithDescription("Listing all top-level commands and groups. Specify a command to see more information.");
            return new CommandHelpMessage(embed: EmbedBuilder.Build());
        }

        public override BaseHelpFormatter WithCommand(Command command)
        {
            Command = command;
            EmbedBuilder.WithDescription($"{Formatter.InlineCode(command.Name)}: {command.Description ?? "No description provided."}");
            if (command is CommandGroup cgroup && cgroup.IsExecutableWithoutSubcommands)
                EmbedBuilder.WithDescription($"{EmbedBuilder.Description}\n\nThis group can be executed as a standalone command.");
            if (command.Aliases?.Any() == true)
                EmbedBuilder.AddField("Aliases", string.Join(", ", command.Aliases.Select(Formatter.InlineCode)), false);

            if (command.Overloads?.Any() == true)
            {
                var sb = new StringBuilder();
                var ovl = command.Overloads.OrderByDescending(x => x.Priority).FirstOrDefault();
                if (ovl != null)
                {
                    sb.Append('`').Append(command.QualifiedName);
                    foreach (var arg in ovl.Arguments)
                        sb.Append(arg.IsOptional || arg.IsCatchAll ? " [" : " <").Append(arg.Name).Append(arg.IsCatchAll ? "..." : "").Append(arg.IsOptional || arg.IsCatchAll ? ']' : '>');
                    sb.Append("`\n");
                    foreach (var arg in ovl.Arguments)
                        sb.Append('`').Append(arg.Name).Append(" (").Append(CommandsNext.GetUserFriendlyTypeName(arg.Type)).Append(")`: ").Append(arg.Description ?? "No description provided.").Append('\n');
                    sb.Append('\n');
                }
                EmbedBuilder.AddField("Arguments", sb.ToString().Trim(), false);
            }

            return this;
        }

        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
        {
            EmbedBuilder.AddField(Command != null ? "Subcommands" : "Commands", string.Join(", ", subcommands.Select(x => Formatter.InlineCode(x.Name))), false);
            return this;
        }
    }
}
