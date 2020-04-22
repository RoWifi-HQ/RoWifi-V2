using System;
using System.Collections.Generic;

namespace RoWifi_Alpha.Utilities.RoLang
{
    public class Tokenizer
    {
        private readonly string source;
        private int start = 0;
        private int current = 0;
        private List<Token> tokens = new List<Token>();

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>()
        {
            {"and", TokenType.AND },
            {"or", TokenType.OR },
            {"HasRank", TokenType.HAS_RANK},
            {"not", TokenType.NOT },
            {"IsInGroup", TokenType.IS_IN_GROUP},
            {"HasRole", TokenType.HAS_ROLE},
            {"WithString", TokenType.WITH_STRING}
        };

        public Tokenizer(string code)
        {
            source = code;
        }

        public List<Token> ScanTokens()
        {
            while(!IsAtEnd())
            {
                start = current;
                ScanToken();
            }

            tokens.Add(new Token(TokenType.EOF, "", null));
            return tokens;
        }

        private void ScanToken()
        {
            char c = Advance();
            switch(c)
            {
                case '(': AddToken(TokenType.LEFT_PAREN); break;
                case ')': AddToken(TokenType.RIGHT_PAREN); break;
                //Ignore whitespace
                case '"': String(); break;
                case ' ': 
                case ',': 
                case '\r':
                case '\t': 
                case '\n': { break; }
                default:
                    if (IsDigit(c))
                        Number();
                    else if (IsAlpha(c))
                        Identifier();
                    else
                        throw new Exception("Unexpected character");
                    break;
            }
        }

        private bool IsAtEnd()
        {
            return current >= source.Length;
        }

        private char Advance()
        {
            return source[current++];
        }

        private void AddToken(TokenType type)
        {
            AddToken(type, null);
        }

        private void AddToken(TokenType type, object literal)
        {
            string text = source[start..current];
            tokens.Add(new Token(type, text, literal));
        }

        private char Peek()
        {
            if (IsAtEnd()) return '\0';
            return source[current];
        }

        private bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private bool IsAlpha(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private void Number()
        {
            while (IsDigit(Peek())) Advance();
            AddToken(TokenType.NUMBER, int.Parse(source[start..current]));
        }

        private void Identifier()
        {
            while (IsAlpha(Peek())) Advance();
            string text = source[start..current];
            bool Success = Keywords.TryGetValue(text, out TokenType type);
            if (!Success) throw new Exception("Expected Keyword");
            AddToken(type);
        }

        private void String()
        {
            while (Peek() != '"') Advance();
            if (IsAtEnd()) throw new Exception("Missing quotes");
            string text = source[(start + 2) ..current];
            Advance();
            AddToken(TokenType.STRING, text);
        }
    }
}