using RoWifi_Alpha.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public abstract class Expr
    {
        public abstract object EvaluateAsync(RoUser user, Dictionary<int, int> Ranks);
    }

    public class Literal : Expr
    {
        public object value;

        public Literal(object value)
        {
            this.value = value;
        }

        public override object EvaluateAsync(RoUser user, Dictionary<int, int> Ranks)
        {
            return this.value;
        }
    }

    public class Func : Expr
    {
        public Token oper;
        public List<Literal> args;
        public bool Flip;

        public Func(Token oper, List<Literal> args, bool Flip)
        {
            if(oper.type == TokenType.HAS_RANK && args.Count != 2)
            {
                throw new Exception("Expected only 2 arguments. {Group Id}, {Rank Id}");
            }
            else if(oper.type == TokenType.IS_IN_GROUP && args.Count != 1)
            {
                throw new Exception("Expected only 1 arguments. {Group Id}");
            }
            this.oper = oper;
            this.args = args;
            this.Flip = Flip;
        }

        public override object EvaluateAsync(RoUser user, Dictionary<int, int> Ranks)
        {
            if (oper.type == TokenType.HAS_RANK)
            {
                bool Success = Ranks.TryGetValue((int)args[0].value, out int rank);
                if (!Success) return Flip ^ Success;
                return Flip ^ (rank == (int)args[1].value);
            }
            else if(oper.type == TokenType.IS_IN_GROUP)
            {
                bool Success = Ranks.ContainsKey((int)args[0].value);
                return Flip ^ Success;
            }
            throw new Exception("Invalid Function");
        }
    }

    public class Binar : Expr
    {
        public Expr left;
        public Token oper;
        public Expr right;

        public Binar(Expr left, Token oper, Expr right)
        {
            this.left = left;
            this.oper = oper;
            this.right = right;
        }

        public override object EvaluateAsync(RoUser user, Dictionary<int, int> Ranks)
        {
            bool oper1 = (bool)left.EvaluateAsync(user, Ranks);
            bool oper2 = (bool)right.EvaluateAsync(user, Ranks);
            if (oper.type == TokenType.AND)
                return oper1 && oper2;
            else if (oper.type == TokenType.OR)
                return oper1 || oper2;
            throw new Exception("Invalid Operation");
        }
    }
}
