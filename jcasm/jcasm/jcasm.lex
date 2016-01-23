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
"("				return (int)Tokens.LPAREN;
")"				return (int)Tokens.RPAREN;
"."				return (int)Tokens.DOT;
"@"				return (int)Tokens.AT;
"||"				return (int)Tokens.LOR;
"&&"				return (int)Tokens.LAND;
"|"				return (int)Tokens.OR;
"&"				return (int)Tokens.AND;
"=="				return (int)Tokens.EQUALS;
"!="				return (int)Tokens.NOTEQUAL;
"<"				return (int)Tokens.LT;
">"				return (int)Tokens.GT;
"<="				return (int)Tokens.LEQUAL;
">="				return (int)Tokens.GEQUAL;
"+"				return (int)Tokens.PLUS;
"-"				return (int)Tokens.MINUS;
"*"				return (int)Tokens.MUL;
"~"				return (int)Tokens.NOT;
"!"				return (int)Tokens.LNOT;

db				return (int)Tokens.DB;
dw				return (int)Tokens.DW;
dd				return (int)Tokens.DD;

".text"			return (int)Tokens.TEXT;
".data"			return (int)Tokens.DATA;
".rodata"		return (int)Tokens.RODATA;
".bss"			return (int)Tokens.BSS;
".file"			return (int)Tokens.FILE;
".globl"			return (int)Tokens.GLOBL;
".extern"		return (int)Tokens.EXTERN;
".comm"			return (int)Tokens.COMM;
".align"			return (int)Tokens.ALIGN;
".type"			return (int)Tokens.TYPE;
".size"			return (int)Tokens.SIZE;
".ident"			return (int)Tokens.IDENT;
".section"		return (int)Tokens.SECTION;
".local"			return (int)Tokens.LOCAL;

a				yylval.strval = yytext; return (int)Tokens.CC;
always			yylval.strval = yytext; return (int)Tokens.CC;
never			yylval.strval = yytext; return (int)Tokens.CC;
e				yylval.strval = yytext; return (int)Tokens.CC;
eq				yylval.strval = yytext; return (int)Tokens.CC;
z				yylval.strval = yytext; return (int)Tokens.CC;
ne				yylval.strval = yytext; return (int)Tokens.CC;
neq				yylval.strval = yytext; return (int)Tokens.CC;
nz				yylval.strval = yytext; return (int)Tokens.CC;
p				yylval.strval = yytext; return (int)Tokens.CC;
pos				yylval.strval = yytext; return (int)Tokens.CC;
n				yylval.strval = yytext; return (int)Tokens.CC;
neg				yylval.strval = yytext; return (int)Tokens.CC;
poseq			yylval.strval = yytext; return (int)Tokens.CC;
negeq			yylval.strval = yytext; return (int)Tokens.CC;
so				yylval.strval = yytext; return (int)Tokens.CC;
nso				yylval.strval = yytext; return (int)Tokens.CC;
uo				yylval.strval = yytext; return (int)Tokens.CC;
c				yylval.strval = yytext; return (int)Tokens.CC;
nuo				yylval.strval = yytext; return (int)Tokens.CC;
nc				yylval.strval = yytext; return (int)Tokens.CC;

r0				yylval.strval = yytext; return (int)Tokens.REG;
r1				yylval.strval = yytext; return (int)Tokens.REG;
r2				yylval.strval = yytext; return (int)Tokens.REG;
r3				yylval.strval = yytext; return (int)Tokens.REG;
r4				yylval.strval = yytext; return (int)Tokens.REG;
r5				yylval.strval = yytext; return (int)Tokens.REG;
r6				yylval.strval = yytext; return (int)Tokens.REG;
r7				yylval.strval = yytext; return (int)Tokens.REG;
r8				yylval.strval = yytext; return (int)Tokens.REG;
r9				yylval.strval = yytext; return (int)Tokens.REG;
r10				yylval.strval = yytext; return (int)Tokens.REG;
r11				yylval.strval = yytext; return (int)Tokens.REG;
r12				yylval.strval = yytext; return (int)Tokens.REG;
r13				yylval.strval = yytext; return (int)Tokens.REG;
r14				yylval.strval = yytext; return (int)Tokens.REG;
r15				yylval.strval = yytext; return (int)Tokens.REG;
pc				yylval.strval = yytext; return (int)Tokens.REG;
lr				yylval.strval = yytext; return (int)Tokens.REG;
sp				yylval.strval = yytext; return (int)Tokens.REG;
R0				yylval.strval = yytext; return (int)Tokens.REG;
R1				yylval.strval = yytext; return (int)Tokens.REG;
R2				yylval.strval = yytext; return (int)Tokens.REG;
R3				yylval.strval = yytext; return (int)Tokens.REG;
R4				yylval.strval = yytext; return (int)Tokens.REG;
R5				yylval.strval = yytext; return (int)Tokens.REG;
R6				yylval.strval = yytext; return (int)Tokens.REG;
R7				yylval.strval = yytext; return (int)Tokens.REG;
R8				yylval.strval = yytext; return (int)Tokens.REG;
R9				yylval.strval = yytext; return (int)Tokens.REG;
R10				yylval.strval = yytext; return (int)Tokens.REG;
R11				yylval.strval = yytext; return (int)Tokens.REG;
R12				yylval.strval = yytext; return (int)Tokens.REG;
R13				yylval.strval = yytext; return (int)Tokens.REG;
R14				yylval.strval = yytext; return (int)Tokens.REG;
R15				yylval.strval = yytext; return (int)Tokens.REG;
PC				yylval.strval = yytext; return (int)Tokens.REG;
LR				yylval.strval = yytext; return (int)Tokens.REG;
SP				yylval.strval = yytext; return (int)Tokens.REG;


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
     
		if ( result > 0xff )		/* error, constant is out-of-bounds */
			throw new Exception("Bad escape sequence: " + yytext);
     
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

"\n"		return (int)Tokens.NEWLINE;
\.[a-zA-Z_][a-zA-Z0-9_\#`\.]*		yylval.strval = yytext; return (int)Tokens.LOCLABEL;
[a-zA-Z_][a-zA-Z0-9_\#`\.]*		yylval.strval = yytext; return (int)Tokens.LABEL;

