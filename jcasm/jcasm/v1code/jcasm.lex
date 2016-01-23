%namespace jcasm
%visibility internal

%x str
%x comment

%{
	StringBuilder sb;
	int comment_nesting = 0;
%}

%%

0x[0-9a-fA-F]+		yylval.intval = Int32.Parse(yytext.Substring(2), System.Globalization.NumberStyles.HexNumber); return (int)Tokens.INT;
[0-9]+b		yylval.intval = Util.ParseBinary(yytext.Substring(0, yytext.Length - 1)); return (int)Tokens.INT;
[0-9]+			yylval.intval = Int32.Parse(yytext); return (int)Tokens.INT;

"->"				return (int)Tokens.ASSIGN;
","				return (int)Tokens.COMMA;
":"				return (int)Tokens.COLON;
";"				return (int)Tokens.SEMICOLON;

db				return (int)Tokens.DB;
dw				return (int)Tokens.DW;
dd				return (int)Tokens.DD;

"/*"            BEGIN(comment); ++comment_nesting;
"//".*          /* // comments to end of line */

<comment>[^*/]* /* Eat non-comment delimiters */
<comment>"/*"   ++comment_nesting;
<comment>"*/"   if (--comment_nesting == 0) BEGIN(INITIAL);
<comment>[*/]   /* Eat a / or * if it doesn't match comment sequence */

\"      sb = new StringBuilder(); BEGIN(str);
     
<str>\"        { /* saw closing quote - all done */
        BEGIN(INITIAL);
        /* return string constant token type and
        * value to parser
        */
		yylval.strval = sb.ToString();
		return (int)Tokens.STRING;
        }
     
<str>\n        {
        /* error - unterminated string constant */
        /* generate error message */
		throw new Exception("Unterminated string constant: " + sb.ToString());
        }
     
<str>\\[0-7]{1,3} {
        /* octal escape sequence */
        int result;
     
		result = Convert.ToInt32(yytext.Substring(1), 8);
     
        if ( result > 0xff )
                /* error, constant is out-of-bounds */
     
        sb.Append((char)result);
        }
     
<str>\\[0-9]+ {
        /* generate error - bad escape sequence; something
        * like '\48' or '\0777777'
        */
		throw new Exception("Bad escape sequence: " + yytext);
        }
     
<str>\\n  sb.Append('\n');
<str>\\t  sb.Append('\t');
<str>\\r  sb.Append('\r');
<str>\\b  sb.Append('\b');
<str>\\f  sb.Append('\f');
     
<str>\\(.|\n)  sb.Append(yytext[1]);
     
<str>[^\\\n\"]+        {
		sb.Append(yytext);
        }


\.[a-zA-Z_][a-zA-Z0-9_\#`]*		yylval.strval = yytext; return (int)Tokens.LABEL;
[a-zA-Z_][a-zA-Z0-9_\#`]*		yylval.strval = yytext; return (int)Tokens.LABEL;

