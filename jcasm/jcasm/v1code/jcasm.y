%namespace jcasm

%visibility internal
%start file
%partial

%token	COMMA COLON ASSIGN SEMICOLON
%token	DB DW DD
	
%union {
		public int intval;
		public string strval;
		public Statement stmtval;
		public Operand operval;
		public List<DataItem> dilist;
		public DataItem di;
		public DataDirective.DDType ddtype;
	}
	
%token <intval>	INT
%token <strval> STRING LABEL

%type <stmtval> stmtlist stmt instruction directive line_label
%type <operval> operand
%type <dilist> data_list
%type <ddtype> data_directive
%type <di> data_item

%%

file			:									{ output = new StatementList(); }			/* empty */
			|	stmtlist							{ output = $1; }
			;

stmtlist		:	stmt								{ StatementList sl = new StatementList(); sl.list = new List<Statement>(); sl.list.Add($1); $$ = sl; }
			|	stmtlist stmt					{ ((StatementList)$1).list.Add($2); $$ = $1; }
			;

stmt			:	directive SEMICOLON				{ $$ = $1; }
			|	line_label						{ $$ = $1; }
			|	instruction	SEMICOLON			{ $$ = $1; }
			|	SEMICOLON						{ $$ = null; }
			;

directive	:	data_directive data_list			{ $$ = new DataDirective { directive = $1, data = $2 }; }
			;

line_label	:	LABEL COLON						{ $$ = new LineLabel { name = $1 }; }
			;

instruction	:	LABEL operand					{ $$ = new Instruction { op = $1, srca = $2 }; }
			|	LABEL operand COMMA operand		{ $$ = new Instruction { op = $1, srca = $2, srcb = $4 }; }
			|	LABEL operand ASSIGN operand		{ $$ = new Instruction { op = $1, srca = $2, dest = $4 }; }
			|	LABEL operand COMMA operand ASSIGN operand	{ $$ = new Instruction { op = $1, srca = $2, srcb = $4, dest = $6 }; }
			|	LABEL							{ $$ = new Instruction { op = $1 }; }
			;

operand		:	INT								{ $$ = new IntegerOperand { val = $1 }; }
			|	LABEL							{ $$ = new RegisterOperand { val = $1 }; }
			;

data_directive:	DB								{ $$ = DataDirective.DDType.Byte; }
			|	DW								{ $$ = DataDirective.DDType.Word; }
			|	DD								{ $$ = DataDirective.DDType.DWord; }
			;

data_list	:									{ $$ = new List<DataItem>(); }
			|	data_item						{ $$ = new List<DataItem> { $1 }; }
			|	data_item COMMA data_list		{ $$ = new List<DataItem>(); $$.Add($1); $$.AddRange($3); }
			;

data_item	:	INT								{ $$ = new IntegerDataItem { val = $1 }; }
			|	STRING							{ $$ = new StringDataItem { val = $1 }; }
			|	LABEL							{ $$ = new LabelDataItem { val = $1 }; }
			;
			
%%

internal Statement output;
