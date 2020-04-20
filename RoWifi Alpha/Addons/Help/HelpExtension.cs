using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoWifi_Alpha.Addons.Help
{
    public static class HelpExtension
    {
        public static Embed GetDefaultEmbed(this CommandService commandService, string command, string prefix)
        {
            var moduleMatch = commandService.SearchModule(command);
            EmbedBuilder helpEmbed = new EmbedBuilder();
            if (string.IsNullOrEmpty(command))
            {
                helpEmbed.WithTitle("Help").WithDescription("Listing all top-level commands and groups. Specify a command to see more information.");
                IEnumerable<ModuleInfo> modules = commandService.Modules.Where(m => !m.IsSubmodule);
                string cmdList = "";
                foreach (ModuleInfo module in modules)
                {   
                    if (module.Group == null)
                        foreach (var cmd in module.Commands)
                            cmdList += $"`{cmd.Name}` ";
                    else
                        cmdList += $"`{module.Name}` ";
                }
                helpEmbed.AddField("Commands", cmdList);
                return helpEmbed.Build();
            }
            else if(moduleMatch != null)
            {
                helpEmbed.WithTitle("Help").WithDescription($"`{moduleMatch.Name}`: {moduleMatch.Summary ?? "No description found"}");
                string Commands = "";
                foreach (var Cmd in moduleMatch.Commands)
                    if (Cmd.Name.Length > 0)
                        Commands += $"`{Cmd.Name}` ";
                if (Commands.Length > 0)
                    helpEmbed.AddField("Commands", Commands);

                string SubModules = "";
                foreach (var sub in moduleMatch.Submodules)
                    SubModules += $"`{sub.Name}` ";
                if (SubModules.Length > 0)
                    helpEmbed.AddField("Sub-Modules", SubModules);

                string Aliases = "";
                foreach (var Alias in moduleMatch.Aliases)
                    Aliases += $"`{Alias}` ";
                if (Aliases.Length > 0)
                    helpEmbed.AddField("Aliases", Aliases);

                return helpEmbed.Build();
            }
            else
            {
                SearchResult search = commandService.Search(command);
                if (search.IsSuccess)
                {
                    foreach (var c in search.Commands)
                        Console.WriteLine(c.Alias);
                    var cmd = search.Commands.Last().Command;
                    var cmdName = cmd.Name.Length == 0 ? cmd.Module.Name : cmd.Name;
                    helpEmbed.WithTitle("Help").WithDescription($"`{cmdName}`: {cmd.Summary ?? "No description found"}");
                    var CmdInfo = new CommandHelpInfo(cmd);
                    helpEmbed.WithFields(CmdInfo.BuildInfo());
                }
                return helpEmbed.Build();
            }
        }

        private static ModuleInfo SearchModule(this CommandService commandService, string name)
        {
            var modules = commandService.Modules;
            foreach(var mod in modules)
            {
                if (mod.Name == name)
                    return mod;
                else
                {
                    foreach (var Alias in mod.Aliases)
                        if (Alias.Equals(name, StringComparison.OrdinalIgnoreCase))
                            return mod;
                    foreach (var sub in mod.Submodules)
                        if (sub.Aliases.Contains(name))
                            return sub;
                }
            }
            return null;
        }
    }
}
