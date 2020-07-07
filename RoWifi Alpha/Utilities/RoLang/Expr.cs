using RoWifi_Alpha.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public abstract class Expr
    {
        public abstract object EvaluateAsync(RoCommandUser user);
    }

    public class Literal : Expr
    {
        public object value;

        public Literal(object value)
        {
            this.value = value;
        }

        public override object EvaluateAsync(RoCommandUser user)
        {
            return value;
        }
    }

    public class Func : Expr
    {
        public Token oper;
        public List<Literal> args;
        public bool Flip;

        public Func(Token oper, List<Literal> args, bool Flip)
        {
            if (oper.type == TokenType.HAS_RANK)
            {
                if (args.Count != 2)
                    throw new Exception("Expected only 2 arguments. {Group Id}, {Rank Id}");
                bool Success = int.TryParse(args[0].value.ToString(), out _);
                if (!Success) throw new Exception("Expected integer at arg Group Id");
                Success = int.TryParse(args[1].value.ToString(), out _);
                if (!Success) throw new Exception("Expected integer at arg Rank Id");
            }
            else if (oper.type == TokenType.IS_IN_GROUP)
            {
                if (args.Count != 1)
                    throw new Exception("Expected only 1 arguments. {Group Id}");
                bool Success = int.TryParse(args[0].value.ToString(), out _);
                if (!Success) throw new Exception("Expected integer at arg Group Id");
            }
            else if (oper.type == TokenType.WITH_STRING && args.Count != 1)
            {
                throw new Exception("Expected only 1 argument. {Keyword}");
            }
            else if (oper.type == TokenType.HAS_ROLE)
            {
                if (args.Count != 1)
                    throw new Exception("Expected only 1 argument. {Role Id}");
                bool Success = ulong.TryParse(args[0].value.ToString(), out _);
                if (!Success) throw new Exception("Expected integer at arg Role Id");
            }
            this.oper = oper;
            this.args = args;
            this.Flip = Flip;
        }

        public override object EvaluateAsync(RoCommandUser user)
        {
            if (oper.type == TokenType.HAS_RANK)
            {
                bool Success = user.Ranks.TryGetValue(Convert.ToInt32(args[0].value), out int rank);
                if (!Success) return Flip ^ Success;
                return Flip ^ (rank == Convert.ToInt32(args[1].value));
            }
            else if(oper.type == TokenType.IS_IN_GROUP)
            {
                bool Success = user.Ranks.ContainsKey(Convert.ToInt32(args[0].value));
                return Flip ^ Success;
            }
            else if(oper.type == TokenType.HAS_ROLE)
            {
                bool Success = user.Member.Roles.Select(r => r.Id).Contains((ulong)args[0].value);
                return Flip ^ Success;
            }
            else if(oper.type == TokenType.WITH_STRING)
            {
                bool Success = user.RobloxUsername.Contains((string)args[0].value);
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

        public override object EvaluateAsync(RoCommandUser user)
        {
            bool oper1 = (bool)left.EvaluateAsync(user);
            bool oper2 = (bool)right.EvaluateAsync(user);
            if (oper.type == TokenType.AND)
                return oper1 && oper2;
            else if (oper.type == TokenType.OR)
                return oper1 || oper2;
            throw new Exception("Invalid Operation");
        }
    }
}
