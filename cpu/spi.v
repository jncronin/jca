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
 
// SPI MMIO interface

module spi(clk, rst_, data, addr, cs_, oe_, we_, spi_ncs, spi_sclk, spi_mosi, spi_miso);
	input clk;
	input rst_;
	inout [7:0] data;
	input [7:0] addr;
	input cs_;
	input oe_;
	input we_;
	output [7:0] spi_ncs;
	output spi_sclk;
	output spi_mosi;
	input spi_miso;
	
parameter DEFAULT_CLK_DIV = 32'd125;		// 400 kHz
	
reg [7:0] r[0:11];
// Provide read support of memory
assign data = (~cs_ & ~oe_) ? r[addr] : 8'bzzzzzzzz;
	
/* Registers:

	0				Control register
						Bit 0 - set to one to trigger software reset, cleared after reset
						Bit 1 - set to one to send a byte of data, cleared after data sent
						Bit 3:2 - SPI mode
								0 - 	positive pulse, latch then shift
								1 - 	positive pulse, shift then latch
								2 - 	negative pulse, latch then shift
								3 - 	negative pulse, shift then latch
						Bits 6:4 - CS select - select device to send data to (numbered 0 through 7)
						Bits 7 - CS out enable - enable output of a CS signal
						
	4				Clock divider
						Clock is divided by this 32 bit value
						
	8				Data in/out
	
	Method for sending/receiving a byte:
		Poll until reg0 bit 1 is cleared
		Set up clock divider in reg1
		Write data to reg2
		Set SPI mode, CS out enable and CS select in reg0
		Set reg0 bit 1 to initiate transfer
		Poll until reg0 bit 1 is cleared
		Read response from reg2
*/

reg [7:0] xmit_state = 8'hff;		// rests at 0xff, counts 7 through 0 during transmission (bytes sent msb first)
reg [7:0] next_xmit_state = 8'd0;
reg [31:0] clk_div = 32'd0;
reg spi_sclk = 0;
reg [7:0] spi_ncs = 8'hff;

reg spi_mosi = 0;
reg latch_buf = 0;

`ifndef USE_RST_LOGIC
initial begin
	{ r[3], r[2], r[1], r[0] } = 32'd0;
	{ r[7], r[6], r[5], r[4] } = DEFAULT_CLK_DIV;
	{ r[11], r[10], r[9], r[8] } = 32'd0;
end
`endif

always @(posedge clk)
casez(r[0][7:4])
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
begin
`ifdef USE_RST_LOGIC
	if(~rst_ | r[0][0]) begin
		xmit_state = 8'hff;
		{ r[3], r[2], r[1], r[0] } = 32'd0;
		{ r[7], r[6], r[5], r[4] } = DEFAULT_CLK_DIV;
		{ r[11], r[10], r[9], r[8] } = 32'd0;
		spi_mosi = 0;
		spi_sclk = 0;
		clk_div = 32'd0;
		latch_buf = 0;
	end else
`endif
	begin
		clk_div = clk_div + 32'd2;
		if(clk_div >= { r[7], r[6], r[5], r[4] }) begin
			if(~&xmit_state) begin
				// Clock has ticked whilst sending
							
				// state decrements on low in modes 0/1, on high in modes 2/3
				if(spi_sclk & r[0][3])
					xmit_state = xmit_state - 8'h01;
				else if(~spi_sclk & ~r[0][3])
					xmit_state = xmit_state - 8'h01;
				
				// some modes (0 and 2) don't shift the current state but the next state
				next_xmit_state = xmit_state - 8'h1;
				if(&next_xmit_state)
					next_xmit_state = 8'h0;
					
				spi_sclk = ~spi_sclk;
				
				// if xmit_state has reached 0xff, set sent bit to 0 and clk to CPOL
				if(&xmit_state) begin
					r[0][1] = 1'b0;
					spi_sclk = r[0][3];
				end else begin
					// else transfer some data
					
					// modes 0 and 2 latch before shifting, therefore we need to buffer the data recived
					//  so it doesn't overwrite what we're about to shift out
								
					// if clock has gone positive, latch in modes 0 and 3, shift in 1 and 2
					// if negative, latch in 1/2, shift in 0/3
					if(spi_sclk)
						case(r[0][3:2])
							2'd0:
								latch_buf = spi_miso;
							2'd1:
								spi_mosi = r[8][xmit_state[2:0]];
							2'd2:
								begin
									spi_mosi = r[8][next_xmit_state[2:0]];
									r[8][xmit_state[2:0]] = latch_buf;
								end
							2'd3:
								r[8][xmit_state[2:0]] = spi_miso;
						endcase
					else
						case(r[0][3:2])
							2'd0:
								begin
									spi_mosi = r[8][next_xmit_state[2:0]];
									r[8][xmit_state[2:0]] = latch_buf;
								end
							2'd1:
								r[8][xmit_state[2:0]] = spi_miso;
							2'd2:
								latch_buf = spi_miso;
							2'd3:
								spi_mosi = r[8][xmit_state[2:0]];
						endcase
				end
			end
			
			// reset clk counter
			clk_div = 32'd0;			
		end			
	end
	
	// update internal registers with a memory write
  	if(~cs_ & ~we_)
	begin
		r[addr] = data;
		
		// if the write changes CPOL, then change the resting state of the clock
		// if the write changes start bit, then set xmit_state to 8
		if(&xmit_state & ~|addr)
		begin
			spi_sclk = r[0][3];
			if(r[0][1])
			begin
				xmit_state = 8'd8;
				spi_mosi = ~r[0][2] & r[8][7];	// shift the first byte out before clock starts cycling in modes 0 + 2
			end
		end
	end
end

endmodule
