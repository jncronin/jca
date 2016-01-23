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
 
// interrupt controller

module irq_ctrl(clk, rst_, data, addr, cs_, oe_, we_, irpts, cpu_int, cpu_int_ack);
	input clk;
	input rst_;
	inout [7:0] data;
	input [7:0] addr;
	input cs_;
	input oe_;
	input we_;
	input [31:0] irpts;
	output reg cpu_int;
	input cpu_int_ack;
	

reg [31:0] irq_mask = 32'd0;
reg [4:0] sirq_num = 5'd0;
reg sirq = 0;

// Read register support
assign data = (~cs_ & ~oe_) ? 
					((addr == 0) ? { sirq, 2'b0, sirq_num } :
					((addr == 4) ? irq_mask[7:0] :
					((addr == 5) ? irq_mask[15:8] :
					((addr == 6) ? irq_mask[23:16] :
					((addr == 7) ? irq_mask[31:24] : 8'b0))))) : 8'bzzzzzzzz;

// Registers
// 0 - in progress
//			bits 4:0	-		signalled interrupt number
//			bit 6:5	-		reserved
//			bit 8		-		interrupt signalled
// 7:4 - interrupt mask (0 = disabled, 1 = enabled)
// 8 - EOI
//			write anything to address 8 to signal EOI

always @(posedge clk)
	if(~sirq)
		casez (irpts & irq_mask)
			32'b???????????????????????????????1:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd0 };
			32'b??????????????????????????????10:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd1 };
			32'b?????????????????????????????100:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd2 };
			32'b????????????????????????????1000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd3 };
			32'b???????????????????????????10000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd4 };
			32'b??????????????????????????100000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd5 };
			32'b?????????????????????????1000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd6 };
			32'b????????????????????????10000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd7 };
			32'b???????????????????????100000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd8 };
			32'b??????????????????????1000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd9 };
			32'b?????????????????????10000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd10 };
			32'b????????????????????100000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd11 };
			32'b???????????????????1000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd12 };
			32'b??????????????????10000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd13 };
			32'b?????????????????100000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd14 };
			32'b????????????????1000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd15 };
			32'b???????????????10000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd16 };
			32'b??????????????100000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd17 };
			32'b?????????????1000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd18 };
			32'b????????????10000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd19 };
			32'b???????????100000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd20 };
			32'b??????????1000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd21 };
			32'b?????????10000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd22 };
			32'b????????100000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd23 };
			32'b???????1000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd24 };
			32'b??????10000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd25 };
			32'b?????100000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd26 };
			32'b????1000000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd27 };
			32'b???10000000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd28 };
			32'b??100000000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd29 };
			32'b?1000000000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd30 };
			32'b10000000000000000000000000000000:	{ cpu_int, sirq, sirq_num } <= { 1'b1, 1'b1, 5'd31 };
			default:											{ cpu_int, sirq, sirq_num } <= { 1'b0, 1'b0, 5'd0 };
		endcase
	else if(cpu_int_ack)	cpu_int <= 0;
	else if(~cs_ & ~we_ & addr == 8'd8)	{ cpu_int, sirq } <= { 1'b0, 1'b0 };
	else sirq <= 1'b1;
	
always @(posedge clk)
	if(~cs_ & ~we_)
		case (addr)
			8'd4:		irq_mask[7:0] <= data;
			8'd5:		irq_mask[15:8] <= data;
			8'd6:		irq_mask[23:16] <= data;
			8'd7:		irq_mask[31:24] <= data;
		endcase
	
endmodule
