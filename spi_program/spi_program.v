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
 
module spi_program(clk, uart_TxD, uart_RxD, spi_ncs, spi_sclk, spi_mosi, spi_miso);
	input clk;
	output uart_TxD;
	input uart_RxD;
	output [7:0] spi_ncs;
	output spi_sclk;
	output spi_mosi;
	input spi_miso;
	
parameter DEFAULT_CLK_DIV = 32'd250;		// 200 kHz
	
reg [7:0] u_td = 8'd0;
wire [7:0] u_rd;
reg u_td_start = 1'b0;
wire u_td_busy;
wire u_rd_ready;
reg u_rd_rcvd = 1'b0;

uart #(2604) u(.clk(clk), .TxD(uart_TxD), .TxD_data(u_td), .TxD_start(u_td_start), .TxD_busy(u_td_busy), .RxD(uart_RxD), .RxD_data(u_rd), .RxD_ready(u_rd_ready),
	.RxD_read(u_rd_rcvd));

wire [7:0] spi_data_in;
reg [7:0] spi_data_out = 8'd0;
wire spi_data_ready;
reg spi_send_data = 0;
reg [1:0] spi_mode = 2'd0;
reg [31:0] spi_clkdiv = DEFAULT_CLK_DIV;
reg [7:0] spi_ncs = 8'hff;

spi s(.clk(clk), .spi_sclk(spi_sclk), .spi_mosi(spi_mosi), .spi_miso(spi_miso), .data_in(spi_data_in), .data_out(spi_data_out), .data_ready(spi_data_ready),
	.send_data(spi_send_data), .mode(spi_mode), .clk_div(spi_clkdiv));
	
// uart recv ring buffer
reg [7:0] rb [0:7];
reg [2:0] rptr = 3'd0;
reg [2:0] wptr = 3'd0;

wire d_ready;
assign d_ready = (wptr != rptr);

/*always @(posedge clk)
	if(~u_td_busy)
		{ u_td, u_td_start } <= { 8'h48, 1'b1 };*/
		
reg [3:0] rcv_wait = 4'd0;
		
// receive messages to ring buffer
always @(posedge clk)
	if(|rcv_wait)
		rcv_wait = rcv_wait - 4'd1;
	else if(u_rd_ready) begin
		rb[wptr] = u_rd;
		wptr = wptr + 3'd1;
		u_rd_rcvd = 1'b1;
		rcv_wait = 4'd8;
	end else if(u_rd_rcvd)
		u_rd_rcvd = 1'b0;
		
// read from ring buffer to instruction buffer
reg [7:0] instrs [0:1];
reg [1:0] instr_idx = 2'd0;
reg [7:0] state = 8'd0;
reg [7:0] resp = 8'd0;
reg [3:0] cselect = 4'd0;

reg [15:0] wait_state = 16'd0;
reg [7:0] next_state = 8'd0;

// cselect
always @(posedge clk)
casez(cselect)
	{ 1'b1, 3'd0 }:	spi_ncs <= 8'b11111110;
	{ 1'b1, 3'd1 }:	spi_ncs <= 8'b11111101;
	{ 1'b1, 3'd2 }:	spi_ncs <= 8'b11111011;
	{ 1'b1, 3'd3 }:	spi_ncs <= 8'b11110111;
	{ 1'b1, 3'd4 }:	spi_ncs <= 8'b11101111;
	{ 1'b1, 3'd5 }:	spi_ncs <= 8'b11011111;
	{ 1'b1, 3'd6 }:	spi_ncs <= 8'b10111111;
	{ 1'b1, 3'd7 }:	spi_ncs <= 8'b01111111;
	{ 1'b0, 3'b??? }:	spi_ncs <= 8'b11111111;
endcase

always @(posedge clk)
	case(state)
		8'd0:
			// state 0 is filling instruction buffer
			if(d_ready) begin
				instrs[instr_idx] = rb[rptr];
				rptr = rptr + 3'd1;
				instr_idx = instr_idx +  2'd1;
				
				if(instr_idx == 2'd2)
					{ state, instr_idx } = { 8'd1, 2'd0 };
			end
			
		8'd1:
			// state 1 is interpreting the instruction
			case(instrs[0])
				8'd0:
					// cmd 0 is set mode
					{ state, resp, spi_mode } = { 8'd3, 8'd0, instrs[1][1:0] };
				8'd1:
					// cmd 1 is set msb of clkdiv
					{ state, resp, spi_clkdiv[31:24] } = { 8'd3, 8'd0, instrs[1] };
				8'd2:
					// cmd 2 is set next bit of clkdiv
					{ state, resp, spi_clkdiv[23:16] } = { 8'd3, 8'd0, instrs[1] };			
				8'd3:
					// cmd 3 is set next bit of clkdiv
					{ state, resp, spi_clkdiv[15:8] } = { 8'd3, 8'd0, instrs[1] };	
				8'd4:
					// cmd 4 is set lsb of clkdiv
					{ state, resp, spi_clkdiv[7:0] } = { 8'd3, 8'd0, instrs[1] };
				8'd5:
					// cmd 5 is set cs
					{ state, resp, cselect } = { 8'd3, 8'd0, instrs[1][3:0] };
				8'd6:
					// cmd 6 is transmit byte
					if(spi_data_ready)
						{ state, wait_state, next_state, spi_data_out, spi_send_data } = { 8'd5, 16'd8, 8'd2, instrs[1], 1'b1 };
					else
						state = 8'd1;	// spin waiting for spi to be ready
				
				default:
					// unknown command
					{ state, resp } = { 8'd3, 8'd3 };
			endcase
					
		8'd2:
			// state 2 is waiting for spi to finish
			if(spi_data_ready)
				{ state, resp, spi_send_data } = { 8'd3, spi_data_in, 1'b0 };
			else
				{ state, spi_send_data } = { 8'd2, 1'b0 };			// spin waiting for spi to return, reset send bit
			
		8'd3:
			// state 3 is sending the response
			if(u_td_busy)
				state = 8'd3;
			else
				{ state, wait_state, next_state, u_td, u_td_start } = { 8'd5, 16'd8, 8'd4, resp, 1'd1 };
				
		8'd4:
			// state 4 is toggling start bit back
				{ state, u_td_start } = { 8'd0, 1'b0 };
				
		8'd5:
			// state 5 introduces a wait state
			case(wait_state)
				16'd0:
					state = next_state;
				default:
					{ state, wait_state } = { 8'd5, wait_state - 16'd1 };
			endcase
	endcase
					

endmodule



module tb();

reg clk;
reg rxd;
wire txd;

initial begin
	clk <= 0;
	rxd <= 1;
	
	
	#80 rxd <= 0;
	#5208 rxd <= 1;
	#5208 rxd <= 0;
	#5208 rxd <= 1;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 1;
	
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 1;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 0;
	#5208 rxd <= 1;
	#5208 rxd <= 1;
end

always #1 clk <= ~clk;

spi_program s(.clk(clk), .uart_TxD(txd), .uart_RxD(rxd));

endmodule