using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public class Parser
    {
        private List<Token> tokens;
        private int current = 0;

        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        public Expr Parse()
        {
            return Bind();
        }

        private Expr Bind()
        {
            Expr expr = Func();
            while(Match(TokenType.AND, TokenType.OR))
            {
                Token oper = Previous();
                Expr right = Func();
                expr = new Binar(expr, oper, right);
            }
            return expr;
        }

        private Expr Func()
        {
            bool Flip = Match(TokenType.NOT);
            List<Literal> args = new List<Literal>();
            if (Match(TokenType.HAS_RANK, TokenType.IS_IN_GROUP))
            {
                Token oper = Previous();
                Consume(TokenType.LEFT_PAREN, "Expected a ( after function");
                while (Match(TokenType.NUMBER))
                {
                    args.Add(new Literal(Previous().literal));
                }
                Consume(TokenType.RIGHT_PAREN, "Expected a ) after function");
                return new Func(oper, args, Flip);
            }
            throw new Exception("Unexpected function name");
        }

        private bool Match(params TokenType[] types)
        {
            foreach (TokenType type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return false;
            return Peek().type == type;
        }

        private Token Advance()
        {
            if (!IsAtEnd()) current++;
            return Previous();
        }

        private bool IsAtEnd()
        {
            return Peek().type == TokenType.EOF;
        }

        private Token Peek()
        {
            return tokens[current];
        }

        private Token Previous()
        {
            return tokens[current - 1];

        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw new Exception(message);
        }
    }
}
