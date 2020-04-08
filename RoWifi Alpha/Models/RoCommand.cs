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

        public bool Evaluate(RoUser user, Dictionary<int, int> Ranks)
        {
            try
            {
                Intepreter intepreter = new Intepreter(expr);
                bool ans = intepreter.Evaluate(user, Ranks);
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
}
