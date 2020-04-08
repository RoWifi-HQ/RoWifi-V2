using RoWifi_Alpha.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public class Intepreter
    {
        public Expr expr;

        public Intepreter(Expr expr)
        {
            this.expr = expr;
        }

        public bool Evaluate(RoUser user, Dictionary<int, int> Ranks)
        {
            bool operation = (bool)expr.EvaluateAsync(user, Ranks);
            return operation;
        }
    }
}
