/* Copyright (C) 2015-2016 by John Cronin
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
 
module cpu(clk, rst_, addr, data, dbg, cs_, oe_, we_, cpu_int, cpu_int_ack);
	input clk;
	input rst_;
	inout [7:0] data;
	output [31:0] addr;
	output [31:0] dbg;
	output we_;
	output cs_;
	output oe_;
	input cpu_int;
	output cpu_int_ack;

// uses around 75% of CycloneII with REGS 16, ~50% with REGS 6 */
parameter REGS = 16;					// Total number of registers (also change REGS_MAX_BIT if changing this)
parameter REGS_MAX_BIT = 3;		// Number of bits to encode registers minus one
											// 2 = 8 registers
											// 3 = 16 registers
											// 4 = 32 registers
parameter PC = 0;
parameter LIT_REG = 1;
parameter SP_REG = 7;
localparam CUR_INST = REGS;

`ifdef USE_DBG
reg [31:0] dbg = 31'd0;
`else
assign dbg = 31'd0;
`endif

reg [31:0] addr = 31'd0;
wire [7:0] data;

reg we_ = 1;
reg cs_ = 1;
reg oe_ = 1;
reg cpu_int_ack = 0;

reg [32:0] r[0:REGS];

reg [7:0] state = 8'd0;
reg [2:0] memory_state = 3'd0;
reg [2:0] bytes_to_xmit = 3'd0;
reg [1:0] reg_part = 2'd0;

reg [4:0] mem_reg = 5'd0;
reg [7:0] next_state = 8'd0;

reg [7:0] data_out = 8'd0;

reg [31:0] srca = 32'd0;
reg [31:0] srcb = 32'd0;
reg [31:0] srcab = 32'd0;
reg [31:0] srcbcond = 32'd0;
reg [31:0] srcabcond = 32'd0;

reg [31:0] irpt_val = 32'd0;

reg ab_is_imm = 1'b0;
reg abcond_is_imm = 1'b0;

reg [4:0] dest_idx = 5'd0;
reg [32:0] cond_reg = 33'd0;

integer i;

assign data = we_ ? 8'bZ : data_out;

//`define DEBUG
`ifdef DEBUG
wire start_inst;
assign start_inst = state == 8'd0;
wire [3:0] opcode = r[CUR_INST][29:26];
wire [3:0] cond_code = r[CUR_INST][25:22];
wire [4:0] dest_idx_w = r[CUR_INST][21:17];
wire [4:0] cond_reg_idx = r[CUR_INST][16:12];
wire [5:0] srcb_idx = r[CUR_INST][11:6];
wire [5:0] srca_idx = r[CUR_INST][5:0];

wire [32:0] dest_reg_dbg = r[dest_idx_w];
wire [32:0] cond_reg_dbg = r[cond_reg_idx];
wire [32:0] srca_reg_dbg = r[srca_idx[4:0]];
wire [32:0] srcb_reg_dbg = r[srcb_idx[4:0]];
`endif

`ifndef USE_RST_LOGIC
initial
	for(i = 0; i <= REGS; i=i+1) r[i] = 33'd0;
`endif

//`define COALESCE_REG_WRITES
`ifdef COALESCE_REG_WRITES
`define DEST_REG r[dest_idx]
`define POST_PROC_STATE 8'd0
`else
reg [32:0] dreg = 33'd0;
`define DEST_REG dreg
`define POST_PROC_STATE 8'd7
`endif

always @(posedge clk)
`ifdef USE_RST_LOGIC
	if(~rst_)
	begin
		we_ = 1;
		cs_ = 1;
		oe_ = 1;
		for(i = 0; i <= REGS; i=i+1) r[i] = 33'd0;
`ifdef USE_DBG
		dbg = 32'hffffffff;
`endif
		srca = 32'd0;
		srcb = 32'd0;
		srcab = 32'd0;
		srcbcond = 32'd0;
		srcabcond = 32'd0;
		cond_reg = 33'd0;
		dest_idx = 5'd0;
		ab_is_imm = 1'b0;
		abcond_is_imm = 1'b0;
		
		state = 8'hf0;		// introduce a delay post reset
	end
	else
`endif
	casez (state)
		8'd0: // start of instruction - read 4x bytes to instruction buffer from PC, store to CUR_INST
			if(cpu_int)
				{ state, irpt_val, cpu_int_ack } <= { 8'd8, 32'd0, 1'b0 };
			else	
				{ state, memory_state, bytes_to_xmit, mem_reg, addr, reg_part, next_state, cpu_int_ack } <= { 8'd1, 3'd1, 3'd4, CUR_INST[4:0], r[PC][31:0], 2'd0, 8'd3, 1'b0 };
			
		8'd1:	// memory read to mem_reg
			case(memory_state)
				3'd0:	// zero destination (in case of reads < 4 bytes - skipped by PC reads)
					{ memory_state, r[mem_reg] } <= { 3'd1, 33'd0 };
				3'd1:	// set cs, oe
					{ memory_state, cs_, oe_ } <= { 3'd2, 1'b0, 1'b0 };
				3'd2:	// pause for cselect circuitry to work
					{ memory_state } <= { 3'd3 };
				3'd3:	// pause for memory chips to respond
					{ memory_state } <= { 3'd4 };
				3'd4:	// pause 2 for memory chips to respond
					{ memory_state } <= { 3'd5 };
				3'd5: // read memory, increment addr, decrement bytes_to_xmit, clear cs, oe, increment reg_part
					case(reg_part)
						2'd0:
							{ memory_state, r[mem_reg][7:0], addr, bytes_to_xmit, cs_, oe_, reg_part } <= { 3'd6, data, addr + 32'd1, bytes_to_xmit - 3'd1, 1'b1, 1'b1, reg_part + 2'd1 };
						2'd1:
							{ memory_state, r[mem_reg][15:8], addr, bytes_to_xmit, cs_, oe_, reg_part } <= { 3'd6, data, addr + 32'd1, bytes_to_xmit - 3'd1, 1'b1, 1'b1, reg_part + 2'd1 };
						2'd2:
							{ memory_state, r[mem_reg][23:16], addr, bytes_to_xmit, cs_, oe_, reg_part } <= { 3'd6, data, addr + 32'd1, bytes_to_xmit - 3'd1, 1'b1, 1'b1, reg_part + 2'd1 };
						2'd3:	// clear carry bit in this step
							{ memory_state, r[mem_reg][32], r[mem_reg][31:24], addr, bytes_to_xmit, cs_, oe_, reg_part } <= { 3'd6, 1'b0, data, addr + 32'd1, bytes_to_xmit - 3'd1, 1'b1, 1'b1, reg_part + 2'd1 };
					endcase
				3'd6: // loop to next byte if required (this could be included in previous state, but is done here for simplicity)
					if(|bytes_to_xmit)
						{ memory_state } <= { 3'd1 };		// skip clearing destination 
					else
						{ state } <= { next_state };
			endcase
			
		8'd2: // memory write from mem_reg
			case(memory_state)
				3'd0:	// set data, we (in preparation for cs next step - so addr and data are valid before cs is set)
					case(reg_part)
						2'd0:
							{ memory_state, data_out, we_, reg_part, bytes_to_xmit } <= { 3'd1, srca[7:0], 1'b0, reg_part + 2'd1, bytes_to_xmit - 3'd1 };
						2'd1:
							{ memory_state, data_out, we_, reg_part, bytes_to_xmit } <= { 3'd1, srca[15:8], 1'b0, reg_part + 2'd1, bytes_to_xmit - 3'd1 };	
						2'd2:
							{ memory_state, data_out, we_, reg_part, bytes_to_xmit } <= { 3'd1, srca[23:16], 1'b0, reg_part + 2'd1, bytes_to_xmit - 3'd1 };
						2'd3:
							{ memory_state, data_out, we_, reg_part, bytes_to_xmit } <= { 3'd1, srca[31:24], 1'b0, reg_part + 2'd1, bytes_to_xmit - 3'd1 };
					endcase
				3'd1: // set cs
					{ memory_state, cs_ } <= { 3'd2, 1'b0 };
				3'd2: // pause 1
					memory_state <= 3'd3;
				3'd3: // pause 2
					memory_state <= 3'd4;
				3'd4:	// assume write has completed, clear cs, we, increment addr
					{ memory_state, cs_, we_, addr } <= { 3'd5, 1'b1, 1'b1, addr + 32'd1 };
				3'd5: // loop to next byte if required (this could be included in previous state, but is done here for simplicity)
					if(|bytes_to_xmit)
						{ memory_state } <= { 3'd0 };
					else
						{ state } <= { next_state };
			endcase
			
			
		8'd3:	// decode inst type, handle literal instructions, increment pc
			case(r[CUR_INST][31:30])
				2'b00:		{ state, r[PC][31:0], dest_idx } <= { 8'd4, r[PC][31:0] + 32'd4, r[CUR_INST][21:17] };
				2'b01:		{ state, r[PC][31:0], r[r[CUR_INST][29:25]] } <= { 8'd0, r[PC][31:0] + 32'd4, 8'b0, r[CUR_INST][24:0] };
				default:		{ state, r[PC][31:0], r[LIT_REG] } <= { 8'd0, r[PC][31:0] + 32'd4, 2'b0, r[CUR_INST][30:0] };
			endcase
			
		8'd4:	// source reg decode
			begin
				if(r[CUR_INST][5])	// is srca an immediate
				begin
					if(r[CUR_INST][4])	// is it negative?
						srca <= { 28'hfffffff, r[CUR_INST][3:0] };
					else
						srca <= { 28'h0, r[CUR_INST][3:0] };
				end
				else
					srca <= r[r[CUR_INST][0 + REGS_MAX_BIT:0]][31:0];
					
				if(r[CUR_INST][11])	// is srcb an immediate?
				begin
					if(r[CUR_INST][10])	// is it negative?
						{ srcb, srcab, ab_is_imm } <= { 28'hfffffff, r[CUR_INST][9:6], 22'h3fffff, r[CUR_INST][9:0], 1'b1 };
					else
						{ srcb, srcab, ab_is_imm } <= { 28'h0, r[CUR_INST][9:6], 22'h0, r[CUR_INST][9:0], 1'b1 };
				end
				else
					{ srcb, srcab, ab_is_imm } <= { r[r[CUR_INST][6 + REGS_MAX_BIT:6]][31:0], r[r[CUR_INST][0 + REGS_MAX_BIT:0]][31:0], 1'b0 };
					
				if(r[CUR_INST][16])	// is condreg an immediate?
				begin
					if(r[CUR_INST][15])	// is it negative?
						{ srcbcond, srcabcond, abcond_is_imm } <= { 23'h7fffff, r[CUR_INST][14:6], 17'h1ffff, r[CUR_INST][14:0], 1'b1 };
					else
						{ srcbcond, srcabcond, abcond_is_imm } <= { 23'h0, r[CUR_INST][14:6], 17'h0, r[CUR_INST][14:0], 1'b1 };
				end
				else
					{ srcbcond, srcabcond, abcond_is_imm } <= { r[r[CUR_INST][6 + REGS_MAX_BIT:6]][31:0], r[r[CUR_INST][0 + REGS_MAX_BIT:0]][31:0], 1'b0 };

				cond_reg <= r[r[CUR_INST][12 + REGS_MAX_BIT:12]];
				state <= 8'd5;
			end
			
		8'd5:	// cond decode
			case(r[CUR_INST][25:22])
				4'd0:	// Never
					state <= 8'd0;
				4'd1:	// Equals (zero)
					state <= (~|cond_reg[31:0]) ? 8'd6 : 8'd0;
				4'd2:	// Not equals (not zero)
					state <= (|cond_reg[31:0]) ? 8'd6 : 8'd0;
				4'd3:	// Positive (sign bit zero, not zero)
					state <= ((~cond_reg[31]) & (|cond_reg[31:0])) ? 8'd6 : 8'd0;
				4'd4:	// Negative (sign bit one)
					state <= cond_reg[31] ? 8'd6 : 8'd0;
				4'd5:	// Positive or equal (sign bit zero)
					state <= (~cond_reg[31]) ? 8'd6 : 8'd0;
				4'd6:	// Negative or equal (sign bit one or zero)
					state <= (cond_reg[31] | (~|cond_reg[31:0])) ? 8'd6 : 8'd0;
				4'd7:	// Signed overflow TODO
					state <= 8'd0;
				4'd8:	// Not signed overflow TODO
					state <= 8'd0;
				4'd9:	// Unsigned overflow
					state <= cond_reg[31] ? 8'd6 : 8'd0;
				4'd10:	// Not unsigned overflow
					state <= ~cond_reg[31] ? 8'd6 : 8'd0;
				4'd15:	// Always - srcb/srcab expands into condreg
					{ state, srcb, srcab, ab_is_imm } <= { 8'd6, srcbcond, srcabcond, abcond_is_imm };
				default:
					state <= 8'd0;
			endcase
			
		8'd6:	// instruction decode and execute
				case(r[CUR_INST][29:26])
					4'd0:		// LOAD
						{ state, memory_state, bytes_to_xmit, mem_reg, addr, reg_part, next_state } <= { 8'd1, 3'd0, srcb[2:0], dest_idx[4:0], srca, 2'd0, 8'd0 };
					4'd1:		// STORE
						{ state, memory_state, bytes_to_xmit, addr, reg_part, next_state } <= { 8'd2, 3'd0, srcb[2:0], r[dest_idx][31:0], 2'd0, 8'd0 };
					4'd2:		// MOVE
						if(~|dest_idx)
							if(ab_is_imm)	// immediate move to PC is an add instruction
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[31:0] <= r[PC][31:0] + srcab;
									`DEST_REG[32] <= 1'b0;
								end
							else						// register move to PC is standard move
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[31:0] <= srcab;
									`DEST_REG[32] <= 1'b0;
								end
						else							// all else is also standard move
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[31:0] <= srcab;
									`DEST_REG[32] <= 1'b0;
								end
						
					4'd3:		// ADD
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG <= srca + srcb;
						end
					4'd4:		// SUB
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG <= srca - srcb;
						end
					4'd5:		// SEXT
						case(srcb[2:0])
							2'd1:
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[32:0] <= { srca[7] ? 25'h1ffffff : 25'h0, srca[7:0] };
								end					
							2'd2:
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[32:0] <= { srca[15] ? 17'h1ffff : 17'h0, srca[15:0] };
								end	
							default:
								begin
									state <= `POST_PROC_STATE;
									`DEST_REG[32:0] <= { 1'b0, srca[31:0] };
								end					
						endcase
					4'd6:		// MUL
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG <= srca * srcb;
						end
					4'd7:		// IRET = pop PC (load SP, 4 -> PC; add SP, 4 -> SP;)
						{ state, memory_state, bytes_to_xmit, mem_reg, addr, reg_part, next_state } <= { 8'd1, 3'd1, 3'd4, PC[4:0], r[SP_REG][31:0], 2'd0, 8'd13 };						
					4'd8:	// NOT
						{ state, `DEST_REG } <= { `POST_PROC_STATE, 1'b0, ~srca };
					4'd9:	// AND
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srca & srcb;
							`DEST_REG[32] <= 1'b0;
						end
					4'd10:	// OR
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srca | srcb;
							`DEST_REG[32] <= 1'b0;
						end					
					4'd11:	// XOR
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srca ^ srcb;
							`DEST_REG[32] <= 1'b0;
						end		
					4'd12:	// XNOR
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srca ~^ srcb;
							`DEST_REG[32] <= 1'b0;
						end		
					4'd13:	// LSH
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srca << srcb[4:0];
							`DEST_REG[32] <= 1'b0;
						end		
					4'd14:	// RSH
						begin
							state <= `POST_PROC_STATE;
							`DEST_REG[31:0] <= srcb[31] ? (srca >>> srcb[4:0]) : (srca >> srcb[4:0]);
							`DEST_REG[32] <= 1'b0;
						end		
					
`ifdef USE_DBG
					4'd15:	// DBG
						{ state, dbg } <= { 8'd0, srca };
`else
					4'd15:	// DBG - if not enabled, treat as NOP
						state <= 8'd0;
`endif
						
					default:	// signal unknown opcode exception
						{ state, irpt_val } <= { 8'd8, 32'h80000000 };
						
				endcase

`ifndef COALESCE_REG_WRITES			
		8'd7:		// move dreg to actual destination if coalesced register assignment is off
			{ state, r[dest_idx] } <= { 8'd0, dreg };
`endif

		8'd8:		// interrupt.  sub SP, 4 -> SP
			{ state, r[SP_REG], cpu_int_ack } <= { 8'd9, r[SP_REG] - 33'd4, 1'b1 };
		8'd9:		// store PC -> SP
			{ state, memory_state, bytes_to_xmit, addr, reg_part, next_state, srca } <= { 8'd2, 3'd1, 3'd4, r[SP_REG][31:0], 2'd0, 8'd10, r[PC][31:0] };
		8'd10:		// sub SP, 4 -> SP
			{ state, r[SP_REG] } <= { 8'd11, r[SP_REG] - 33'd4 };
		8'd11:		// store irpt_val -> SP
			{ state, memory_state, bytes_to_xmit, addr, reg_part, next_state, srca } <= { 8'd2, 3'd1, 3'd4, r[SP_REG][31:0], 2'd0, 8'd12, irpt_val };
		8'd12:	// load 4 -> PC
			{ state, memory_state, bytes_to_xmit, mem_reg, addr, reg_part, next_state } <= { 8'd1, 3'd1, 3'd4, PC[4:0], 32'd4, 2'd0, 8'd0 };
			
		8'd13:	// part of IRET: add SP, 4 -> SP
			{ state, r[SP_REG] } <= { 8'd0, r[SP_REG] + 33'd4 };
			
		8'hf?:	// delay states before returning to 0
			{ state, cpu_int_ack } <= { state + 8'd1, 1'b0 };
		
	
		default:		{ state } <= { 8'd0 };
	endcase
	
endmodule