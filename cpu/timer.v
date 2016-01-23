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
 
// Timer MMIO interface

module timer(clk, rst_, data, addr, cs_, oe_, we_, interrupt);
	input clk;
	input rst_;
	inout [7:0] data;
	input [7:0] addr;
	input cs_;
	input oe_;
	input we_;
	output interrupt;
	
reg [7:0] r[0:7];
// Provide read support of memory
assign data = (~cs_ & ~oe_) ? r[addr] : 8'bzzzzzzzz;

`ifndef USE_RST_LOGIC
initial begin
	{ r[3], r[2], r[1], r[0] } = 32'd0;
	{ r[7], r[6], r[5], r[4] } = 32'd0;
end
`endif

always @(posedge clk)
`ifdef USE_RST_LOGIC
	if(~rst_) begin
		{ r[3], r[2], r[1], r[0] } = 32'd0;
		{ r[7], r[6], r[5], r[4] } = 32'd0;
	end
 	else
`endif
	if(~cs_ & ~we_)
		r[addr] <= data;
	else
		{ r[3], r[2], r[1], r[0] } <= { r[3], r[2], r[1], r[0] } + 32'd1;
		
assign interrupt = ( { r[7], r[6], r[5], r[4] } == 32'd0 ) ? 1'b0 :
					(( { r[3], r[2], r[1], r[0] } >= { r[7], r[6], r[5], r[4] } ) ? 1'b1 : 1'b0);
		
endmodule
