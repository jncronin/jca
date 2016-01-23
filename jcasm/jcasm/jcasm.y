%namespace jcasm

%visibility internal
%start file
%partial

%token  NEWLINE
%token	COMMA COLON ASSIGN SEMICOLON DOT LPAREN RPAREN
%token	TEXT DATA RODATA BSS FILE GLOBL LOCAL EXTERN COMM ALIGN TYPE SIZE IDENT SECTION
%token	DB DW DD AT
%token	LOR LAND OR AND EQUALS NOTEQUAL LT GT LEQUAL GEQUAL PLUS MINUS MUL NOT LNOT
	
%union {
		public int intval;
		public string strval;
		public Statement stmtval;
		public Condition condval;
		public List<Expression> dilist;
		public DataDirective.DDType ddtype;
		public Expression exprval;
	}
	
%token <intval>	INT
%token <strval> STRING LABEL LOCLABEL CC REG

%type <stmtval> stmtlist stmt instruction directive line_label
%type <condval> cond
%type <dilist> data_list
%type <ddtype> data_directive
%type <strval> anylabel sectionname
%type <exprval> expr expr2 expr3 expr4 expr5 expr6 expr7 expr8 expr9 expr10 expr11 operand2 dest


%%

file			:									{ output = new StatementList(); }			/* empty */
			|	stmtlist							{ output = $1; }
			;

stmtlist		:	stmt								{ StatementList sl = new StatementList(); sl.list = new List<Statement>(); sl.list.Add($1); $$ = sl; }
			|	stmtlist stmt					{ ((StatementList)$1).list.Add($2); $$ = $1; }
			;

stmt			:	directive SEMICOLON	NEWLINE		{ $$ = $1; }
			|	directive NEWLINE				{ $$ = $1; }
			|	line_label						{ $$ = $1; }
			|	instruction	SEMICOLON			{ $$ = $1; }
			|	instruction NEWLINE				{ $$ = $1; }
			|	SEMICOLON						
			|	NEWLINE
			;

directive	:	data_directive data_list			{ $$ = new DataDirective { directive = $1, data = $2 }; }
			|	TEXT								{ $$ = new SectionHeader { name = ".text" }; }
			|	DATA								{ $$ = new SectionHeader { name = ".data" }; }
			|	RODATA							{ $$ = new SectionHeader { name = ".rodata" }; }
			|	BSS								{ $$ = new SectionHeader { name = ".bss" }; }
			|	FILE expr						{ $$ = null; }
			|	GLOBL anylabel					{ $$ = null; Program.global_objs[$2] = null; }
			|	LOCAL anylabel					{ $$ = null; }
			|	EXTERN anylabel					{ $$ = null; Program.extern_objs.Add($2); }
			|	ALIGN expr						{ $$ = null; }
			|	TYPE anylabel COMMA AT LABEL		{ $$ = null; Program.obj_types[$2] = $5; }
			|	SIZE anylabel COMMA expr			{ $$ = null; Program.obj_sizes[$2] = $4; }
			|	IDENT expr						{ $$ = null; }
			|	COMM anylabel COMMA expr			{ $$ = null; Program.comm_objs[$2] = new CommonSymbol { Size = $4, Align = null }; }
			|	COMM anylabel COMMA expr	 COMMA expr		{ $$ = null; Program.comm_objs[$2] = new CommonSymbol { Size = $4, Align = $6 }; }
			|	SECTION sectionname				{ $$ = new SectionHeader { name = $2 }; Program.RegisterSection($2, null, null, null); }
			|	SECTION sectionname COMMA STRING		{ $$ = new SectionHeader { name = $2 }; Program.RegisterSection($2, $4, null, null); }
			|	SECTION sectionname COMMA STRING COMMA AT LABEL		{ $$ = new SectionHeader { name = $2 }; Program.RegisterSection($2, $4, $7, null); }
			|	SECTION sectionname	COMMA STRING COMMA AT LABEL COMMA LABEL { $$ = new SectionHeader { name = $2 }; Program.RegisterSection($2, $4, $7, $9); }
			|	SECTION sectionname	COMMA STRING COMMA AT LABEL COMMA INT { $$ = new SectionHeader { name = $2 }; Program.RegisterSection($2, $4, $7, $9.ToString()); }
			;

sectionname	:	anylabel							{ $$ = $1; }
			|	STRING							{ $$ = $1; }
			;

line_label	:	LABEL COLON						{ $$ = new LineLabel { name = $1 }; Program.cur_label = $1; }
			|	LOCLABEL COLON					{ $$ = new LineLabel { name = $1 }; }
			;

instruction	:	LABEL cond expr operand2 dest		{ $$ = new Instruction { op = $1, cond = $2, srca = $3, srcb = $4, dest = $5 }; }
			|	LABEL cond						{ $$ = new Instruction { op = $1, cond = $2, srca = null, srcb = null, dest = null }; }
			;

cond			:									{ $$ = new Condition { ctype = Condition.CType.Always}; }
			|	LPAREN CC REG RPAREN				{ $$ = new Condition($2, $3); }
			;

operand2		:									{ $$ = null; }
			|	COMMA expr					{ $$ = $2; }
			;

dest			:									{ $$ = null; }
			|	ASSIGN expr					{ $$ = $2; }
			;

data_directive:	DB								{ $$ = DataDirective.DDType.Byte; }
			|	DW								{ $$ = DataDirective.DDType.Word; }
			|	DD								{ $$ = DataDirective.DDType.DWord; }
			;

data_list	:									{ $$ = new List<Expression>(); }
			|	expr								{ $$ = new List<Expression> { $1 }; }
			|	expr COMMA data_list				{ $$ = new List<Expression>(); $$.Add($1); $$.AddRange($3); }
			;

anylabel		:	LABEL							{ $$ = $1; }
			|	LOCLABEL							{ $$ = $1; }
			|	REG								{ $$ = $1; }
			;

expr			:	LPAREN expr2 RPAREN				{ $$ = $2; }
			|	expr2							{ $$ = $1; }
			;

expr2		:	expr3 LOR expr2					{ $$ = new Expression { a = $1, b = $3, op = Tokens.LOR }; }
			|	expr3							{ $$ = $1; }
			;

expr3		:	expr4 LAND expr3					{ $$ = new Expression { a = $1, b = $3, op = Tokens.LAND }; }
			|	expr4							{ $$ = $1; }
			;

expr4		:	expr5 OR expr4					{ $$ = new Expression { a = $1, b = $3, op = Tokens.OR }; }
			|	expr5							{ $$ = $1; }
			;

expr5		:	expr6 AND expr5					{ $$ = new Expression { a = $1, b = $3, op = Tokens.AND }; }
			|	expr6							{ $$ = $1; }
			;

expr6		:	expr7 EQUALS expr6				{ $$ = new Expression { a = $1, b = $3, op = Tokens.EQUALS }; }
			|	expr7 NOTEQUAL expr6				{ $$ = new Expression { a = $1, b = $3, op = Tokens.NOTEQUAL }; }
			|	expr7							{ $$ = $1; }
			;

expr7		:	expr8 LT expr7					{ $$ = new Expression { a = $1, b = $3, op = Tokens.LT }; }
			|	expr8 GT expr7					{ $$ = new Expression { a = $1, b = $3, op = Tokens.GT }; }
			|	expr8 LEQUAL expr7				{ $$ = new Expression { a = $1, b = $3, op = Tokens.LEQUAL }; }
			|	expr8 GEQUAL expr7				{ $$ = new Expression { a = $1, b = $3, op = Tokens.GEQUAL }; }
			|	expr8							{ $$ = $1; }
			;

expr8		:	expr9 PLUS expr8					{ $$ = new Expression { a = $1, b = $3, op = Tokens.PLUS }; }
			|	expr9 MINUS expr8				{ $$ = new Expression { a = $1, b = $3, op = Tokens.MINUS }; }
			|	expr9							{ $$ = $1; }
			;

expr9		:	expr10 MUL expr9					{ $$ = new Expression { a = $1, b = $3, op = Tokens.MUL }; }
			|	expr10							{ $$ = $1; }
			;

expr10		:	NOT expr10						{ $$ = new Expression { a = $2, b = null, op = Tokens.NOT }; }
			|	LNOT expr10						{ $$ = new Expression { a = $2, b = null, op = Tokens.LNOT }; }
			|	MINUS expr10						{ $$ = new Expression { a = $2, b = null, op = Tokens.MINUS }; }
			|	expr11							{ $$ = $1; }
			;

expr11		:	STRING							{ $$ = new StringExpression { val = $1 }; }
			|	anylabel							{ $$ = new LabelExpression { val = $1, cur_outer_label = Program.cur_label }; }
			|	INT								{ $$ = new IntExpression { val = $1 }; }
			;

			
%%

internal Statement output;
