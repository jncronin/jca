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
 
// SPI interface

module spi(clk, clk_div, data_in, data_out, send_data, data_ready, spi_sclk, spi_mosi, spi_miso, mode);
	input clk;
	input [31:0] clk_div;
	output [7:0] data_in;
	input [7:0] data_out;
	input send_data;
	output data_ready;
	output spi_sclk;
	output spi_mosi;
	input spi_miso;
	input [1:0] mode;
	
reg [7:0] xmit_state = 8'hff;		// rests at 0xff, counts 7 through 0 during transmission (bytes sent msb first)
reg [7:0] next_xmit_state = 8'h00;
reg spi_sclk = 0;
reg [31:0] cd = 32'd0;
reg [7:0] data_in = 8'd0;

reg spi_mosi = 0;
reg latch_buf = 0;

assign data_ready = &xmit_state;

always @(posedge clk)
begin
	begin
		cd = cd + 32'd2;
		if(cd >= clk_div) begin
			if(~&xmit_state) begin
				// Clock has ticked whilst sending
							
				// state decrements on low in modes 0/1, on high in modes 2/3
				if(spi_sclk & mode[1])
					xmit_state = xmit_state - 8'h01;
				else if(~spi_sclk & ~mode[1])
					xmit_state = xmit_state - 8'h01;
				
				// some modes (0 and 2) don't shift the current state but the next state
				next_xmit_state = xmit_state - 8'h1;
				if(&next_xmit_state)
					next_xmit_state = 8'h0;
					
				spi_sclk = ~spi_sclk;
				
				// if xmit_state has reached 0xff, set clk to CPOL
				if(&xmit_state)
					spi_sclk = mode[1];
				else begin
					// else transfer some data
					
					// modes 0 and 2 latch before shifting, therefore we need to buffer the data recived
					//  so it doesn't overwrite what we're about to shift out
								
					// if clock has gone positive, latch in modes 0 and 3, shift in 1 and 2
					// if negative, latch in 1/2, shift in 0/3
					if(spi_sclk)
						case(mode)
							2'd0:
								latch_buf = spi_miso;
							2'd1:
								spi_mosi = data_out[xmit_state[2:0]];
							2'd2:
								begin
									spi_mosi = data_out[next_xmit_state[2:0]];
									data_in[xmit_state[2:0]] = latch_buf;
								end
							2'd3:
								data_in[xmit_state[2:0]] = spi_miso;
						endcase
					else
						case(mode)
							2'd0:
								begin
									spi_mosi = data_out[next_xmit_state[2:0]];
									data_in[xmit_state[2:0]] = latch_buf;
								end
							2'd1:
								data_in[xmit_state[2:0]] = spi_miso;
							2'd2:
								latch_buf = spi_miso;
							2'd3:
								spi_mosi = data_out[xmit_state[2:0]];
						endcase
				end
			end
			
			// reset clk counter
			cd = 32'd0;			
		end			
	end
						
	if(&xmit_state)
		spi_sclk = mode[1];
						
	if(send_data & &xmit_state)
	begin
		xmit_state = 8'd8;
		spi_mosi = ~mode[1] & data_out[7];	// shift the first byte out before clock starts cycling in modes 0 + 2
	end
end

endmodule
