using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Addons.Interactive
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
