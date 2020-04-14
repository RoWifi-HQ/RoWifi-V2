using RoWifi_Alpha.Models;
using System.Collections.Generic;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public class Intepreter
    {
        public Expr expr;

        public Intepreter(Expr expr)
        {
            this.expr = expr;
        }

        public bool Evaluate(RoCommandUser user)
        {
            bool operation = (bool)expr.EvaluateAsync(user);
            return operation;
        }
    }
}
