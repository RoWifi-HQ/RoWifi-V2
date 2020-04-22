namespace RoWifi_Alpha.Utilities.RoLang
{
    public enum TokenType
    {
        AND, OR, NOT, LEFT_PAREN, RIGHT_PAREN, COMMA, HAS_RANK, EOF, NUMBER, IS_IN_GROUP, HAS_ROLE, WITH_STRING, STRING
    }

    public class Token
    {
        public TokenType type;
        public object literal;
        public string lexeme;

        public Token(TokenType type, string lexeme, object literal)
        {
            this.type = type;
            this.lexeme = lexeme;
            this.literal = literal;
        }
    }
}
