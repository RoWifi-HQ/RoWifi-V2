using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Criterion
{
    public class EnsureContentCriterion : ICriterion<SocketMessage>
    {
        private readonly string[] _content;

        public EnsureContentCriterion(params string[] content) { _content = content; }
        public Task<bool> JudgeAsync(SocketCommandContext Context, SocketMessage parameter)
        {
            bool ok = _content.Any(c => c.Equals(parameter.Content, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(ok);
        }
    }
}
