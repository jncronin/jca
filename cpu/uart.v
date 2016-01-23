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
 
module uart(clk, TxD, TxD_data, TxD_start, TxD_busy, RxD, RxD_data, RxD_ready, RxD_read);
	input clk;
	input [7:0] TxD_data;
	input TxD_start;
	output TxD;
	output TxD_busy;
	input RxD;
	output [7:0] RxD_data;
	output RxD_ready;
	input RxD_read;
	
parameter Counter = 1302;
localparam Counter8 = Counter >> 3;	// 1/8th of a counter tick
	
reg [7:0] chr = 8'h48;
reg [7:0] state = 0;

reg [12:0] count = 0;

// Transmit circuit
always @(posedge clk)
	if(count == 0)
		case (state)
			8'b00000000:	if(TxD_start) { chr, state, count } <= { TxD_data, 8'b11000000, 13'd1 }; else { state, count } <= { 8'd0, 13'd0 };
			8'b11000000:	{ state, count } <= { 8'b10100000, 13'd1 };
			8'b10100000:	{ state, count } <= { 8'b10100001, 13'd1 };
			8'b10100001:	{ state, count } <= { 8'b10100010, 13'd1 };
			8'b10100010:	{ state, count } <= { 8'b10100011, 13'd1 };
			8'b10100011:	{ state, count } <= { 8'b10100100, 13'd1 };
			8'b10100100:	{ state, count } <= { 8'b10100101, 13'd1 };
			8'b10100101:	{ state, count } <= { 8'b10100110, 13'd1 };
			8'b10100110:	{ state, count } <= { 8'b10100111, 13'd1 };
			8'b10100111:	{ state, count } <= { 8'b10010000, 13'd1 };
			8'b10010000:	{ state, count } <= { 8'b10010001, 13'd1 };
			default:			{ state, count } <= { 8'b00000000, 13'd0 };
		endcase
	else
		if(count == Counter)
			count <= 13'd0;
		else
			count <= count + 13'd1;
			
assign TxD = (state[6:5] == 0) | (state[5] & chr[state[2:0]]);
assign TxD_busy = state != 0;

// Receive circuit
reg [7:0] RxD_data = 8'd0;
reg [3:0] RxD_state = 4'd0;
reg [3:0] RxD_filter = 4'd0;
reg RxD_filtered = 1'b0;
reg [2:0] RxD_filter_idx = 3'd0;
reg [12:0] RxD_count = 13'd0;
reg stop_bit = 1'b0;

/* Filter the incoming data by sampling it 8 times a baud tick and choosing the
		middle 4 bits to actually sample
	We start our reading once a start bit is read */
	
// 2 bits set = logic 1 else logic 0
always @(posedge clk)
	case(RxD_filter)
		4'b0000, 4'b0001, 4'b0010, 4'b0100, 4'b1000:	RxD_filtered <= 1'b0;
		default:	RxD_filtered <= 1'b1;
	endcase
			
always @(posedge clk)
	if(~|RxD_state) begin	// state 0
		if(RxD_read)
			stop_bit = 1'b0;		// do no further reads when RxD held high
		
		if(~RxD)
			{ RxD_state, RxD_count } = { 4'd1, 13'd0 };		// reset count when start bit detected
		else
			{ RxD_state, RxD_count } = { 4'd0, 13'd0 };
	end
	else
		if(RxD_count == Counter8) begin
			{ RxD_count, RxD_filter_idx } = { 13'd0, RxD_filter_idx + 3'd1 };
			
			if(RxD_filter_idx == 3'd0)		// one baud tick has passed
				case(RxD_state)
					4'd1:		// start bit
						if(RxD_filtered)
							RxD_state = 4'd0;	// invalid start bit
						else
							RxD_state = 4'd2;
					4'd2:	{ RxD_data[0], RxD_state } = { RxD_filtered, 4'd3 };
					4'd3:	{ RxD_data[1], RxD_state } = { RxD_filtered, 4'd4 };
					4'd4:	{ RxD_data[2], RxD_state } = { RxD_filtered, 4'd5 };
					4'd5:	{ RxD_data[3], RxD_state } = { RxD_filtered, 4'd6 };
					4'd6:	{ RxD_data[4], RxD_state } = { RxD_filtered, 4'd7 };
					4'd7:	{ RxD_data[5], RxD_state } = { RxD_filtered, 4'd8 };
					4'd8:	{ RxD_data[6], RxD_state } = { RxD_filtered, 4'd9 };
					4'd9:	{ RxD_data[7], RxD_state } = { RxD_filtered, 4'd10 };
					4'd10: { stop_bit, RxD_state } = { RxD_filtered, 4'd0 };
					default: { stop_bit, RxD_state } = { 1'b0, 4'd0 };
				endcase
			else if(RxD_filter_idx == 3'd2) RxD_filter[0] = RxD;
			else if(RxD_filter_idx == 3'd3) RxD_filter[1] = RxD;
			else if(RxD_filter_idx == 3'd4) RxD_filter[2] = RxD;
			else if(RxD_filter_idx == 3'd5) RxD_filter[3] = RxD;
		end
		else
			RxD_count = RxD_count + 13'd1;
			
assign RxD_ready = (~|RxD_state & stop_bit);		
					
endmodule

/* Encapsulates the uart class in a MMIO device

	Responds to 4-byte aligned accesses only

	Registers:
		0 - state (RO)
			Bit 0 -		If set then cannot accept data
			Other bits reserved
			
		4 - char buffer (WO)
			Write next character to send - will send when able
*/

module uart_mmio(clk, rst_, TxD, data, addr, cs_, oe_, we_);
	input clk;
	input rst_;
	output TxD;
	inout [7:0] data;
	input [7:0] addr;
	input cs_;
	input oe_;
	input we_;
	
parameter Counter = 2604;
localparam REGS = 8;

reg [7:0] r[0:REGS - 1];
reg TxD_start = 0;
wire TxD_busy;

integer i;

assign data = (~cs_ & ~oe_) ? r[addr] : 8'bzzzzzzzz;

uart #(Counter) u(.clk(clk), .TxD(TxD), .TxD_busy(TxD_busy), .TxD_data(r[4]), .TxD_start(TxD_start));

always @(posedge clk)
	r[0] = { 7'b0, TxD_busy };
	
`ifndef USE_RST_LOGIC
initial
	for(i = 0; i < REGS; i=i+1) r[i] = 8'd0;
`endif
	
	

//always @(posedge clk or negedge rst_)
always @(posedge clk)
`ifdef USE_RST_LOGIC
	if(~rst_)
		{ r[1], r[2], r[3], r[4], r[5], r[6], r[7], TxD_start } <= { 8'b0, 8'b0, 8'b0, 8'b0, 8'b0, 8'b0, 8'b0, 1'b0 };
	else
`endif
		if(~cs_ & ~we_)
			case(addr)
				8'd4:
					if(~TxD_busy)
						{ r[4], TxD_start } <= { data, 1'b1 };
					else
						TxD_start <= 1'b0;
				
				default:
					TxD_start <= 1'b0;
			endcase
		else
			TxD_start <= 1'b0;
	
  //begin
  	//r[0] = { 7'b0, TxD_busy };
	/*r[1] = 8'b0;
	r[2] = 8'b0;
	r[3] = 8'b0;
	r[5] = 8'b0;
	r[6] = 8'b0;
	r[7] = 8'b0;*/
  	/*TxD_start = 1'b0;
  	if(~cs_ & ~we_)
  		case(addr)
  			8'd4:
  				if(~TxD_busy)
  					{ r[4], TxD_start } = { data, 1'b1 };
  		endcase
  end*/				
	
endmodule
	
