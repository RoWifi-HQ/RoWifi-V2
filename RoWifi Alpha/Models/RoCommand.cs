using Discord;
using RoWifi_Alpha.Utilities.RoLang;
using System;
using System.Collections.Generic;

namespace RoWifi_Alpha.Models
{
    public class RoCommand
    {
        private string Code;
        private Expr expr;
        public RoCommand(string text)
        {
            Code = text;
            Tokenizer tokenizer = new Tokenizer(Code);
            List<Token> tokens = tokenizer.ScanTokens();
            Parser scanner = new Parser(tokens);
            expr = scanner.Parse();
        }

        public bool Evaluate(RoCommandUser user)
        {
            try
            {
                Intepreter intepreter = new Intepreter(expr);
                bool ans = intepreter.Evaluate(user);
                return ans;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }
    }

    public class RoCommandUser
    {
        public RoUser User;
        public IGuildUser Member;
        public Dictionary<int, int> Ranks;
        public string RobloxUsername;

        public RoCommandUser(RoUser User, IGuildUser Member, Dictionary<int, int> Ranks, string RobloxUsername)
        {
            this.User = User;
            this.Member = Member;
            this.Ranks = Ranks;
            this.RobloxUsername = RobloxUsername;
        }
    }
}
