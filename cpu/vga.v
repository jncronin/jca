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
 
module vga(clk, r, g, b, hs, vs, cs_, oe_, we_, addr, data);
	input clk;
	output reg r;
	output reg g;
	output reg b;
	output reg hs;
	output reg vs;
	input cs_;
	input oe_;
	input we_;
	input [11:0] addr;
	inout [7:0] data;
	
/* See http://martin.hinner.info/vga/vga.html

The VGA signal is composed of lines and frames.  The frame is:
	Active video -> front porch -> sync pulse (HS#) -> back porch
The lines are:
	Active video (certain number of frames) -> front porch -> sync pulse (VS#) -> back porch
	
Note that during sync pulses, the green output is equal to HS ^ VS, and all sync pulses are
active low.
	
The 640x480 mode has the following characteristics:
	VRefresh 			60 Hz
	HRefresh				31.5 kHz
	Pixel frequency	25.175 MHz
	
	Line:
		Active video	640 pixels
		Front porch		16 pixels
		Sync pulse		96 pixels
		Back porch		48 pixels
		
		Thus a total of 800 pixels
		
	Frame:
		Active video	480 lines
		Front porch		10 lines
		Sync pulse		2 lines
		Back porch		33 lines
		
		Total of 525 lines
		
	We assume modern monitors can cope with a slightly different frequency
	and use a pixel frequency of 25 MHz instead, with the same 800x525 window.
	
	This means that each pixel is displayed for a total of 2 clock cycles at
	a input frequency of 50 MHz.  This gives us three states per colour i.e.
	11, 10 or 01 (these will be displayed the same) and 00.  With 3 colours
	(RGB) we can thus output up to 27 different colours.  For any more we'd need
	to use a PLL to multiply our clock sufficiently.
*/

reg [10:0] counterX = 11'd0;		// count up to 1600 (2 counts per pixel)
reg [9:0] counterY = 10'd0;		// count up to 525

parameter Width = 640;
parameter Height = 480;
parameter LineFrontPorch = 16;
parameter LineSyncPulse = 96;
parameter LineBackPorch = 48;
parameter FrameFrontPorch = 11;
parameter FrameSyncPulse = 2;
parameter FrameBackPorch = 31;
parameter ClocksPerPixel = 2;

localparam TotalLine = (Width + LineFrontPorch + LineSyncPulse + LineBackPorch) * ClocksPerPixel;
localparam TotalFrame = Height + FrameFrontPorch + FrameSyncPulse + FrameBackPorch;

localparam LWVal = Width * ClocksPerPixel;
localparam LFPVal = (Width + LineFrontPorch) * ClocksPerPixel;
localparam LSPVal = (Width + LineFrontPorch + LineSyncPulse) * ClocksPerPixel;

localparam FFPVal = Height + FrameFrontPorch;
localparam FSPVal = Height + FrameFrontPorch + FrameSyncPulse;

wire counterXMaxed = (counterX == TotalLine);
wire counterYMaxed = (counterY == TotalFrame);

always @(posedge clk)
	if(counterXMaxed)
		counterX <= 11'd0;
	else
		counterX <= counterX + 11'd1;
		
always @(posedge clk)
	if(counterXMaxed)
		if(counterYMaxed)
			counterY <= 10'd0;
		else
			counterY <= counterY + 10'd1;
			
/* We implement a simple text based frame buffer with 80 x 25 characters.  For 640x480 this means
the characters are 8 pixels across and 16 characters high (we lose the last 80 lines).

The character values in the RAM framebuffer reference a font in the ROM.
We can determine the framebuffer address of a given character by xchar + ychar * 2^charsAcrossLog -
ie round up CharsAcross to a power of 2 then use bit indexes to get the appropriate value

*/

parameter CharsAcross = 80;
parameter CharsAcrossLog = 7;
parameter CharsDown = 25;
parameter CharWidth = 8;
parameter CharHeight = 16;
parameter AllCharWidth = CharsAcross * CharWidth * ClocksPerPixel;
parameter AllCharHeight = CharsDown * CharHeight;

wire [6:0] xchar = (counterX < AllCharWidth) ? counterX[10:4] : 7'h7f;
wire xcharvalid = ~&xchar;
wire [6:0] ychar = (counterY < AllCharHeight) ? { 1'b0, counterY[9:4] } : 7'h7f;
wire ycharvalid = ~&ychar;
wire charvalid = xcharvalid & ycharvalid;
wire [11:0] mem_addr = { ychar[4:0], xchar[6:0] };

// Framebuffer - side A is CPU, side B is video
wire [7:0] fbuf_out;
wire [7:0] fbuf_out_to_cpu;
assign data = (~cs_ & ~oe_) ? fbuf_out_to_cpu : 8'bzzzzzzzz;

vga_ram ram_fb(.address_a(addr), .address_b(mem_addr), .clock_a(clk), .clock_b(clk), .data_a(data), .q_a(fbuf_out_to_cpu), .q_b(fbuf_out), .wren_a(~cs_ & ~we_), .wren_b(1'b0));

// ROM contains fonts
wire [2:0] charxbit = counterX[3:1];
wire [3:0] charybit = counterY[3:0];
wire [7:0] font_out;

// change to wire [11:0] font_addr = { fbuf_out[7:0],... } to support all 256 characters (requires 4 kiB font memory)
wire [11:0] font_addr = { fbuf_out[7:0], charybit };
vga_font_rom font_rom(.clock(clk), .address(font_addr), .q(font_out));
wire font_bit = font_out[charxbit] & charvalid;
			
always @(posedge clk)
	if(counterY < Height)
	begin
		if(counterX < LWVal)
			{ r, g, b, hs, vs } <= { font_bit, font_bit, font_bit, 1'b1, 1'b1 };
		else if(counterX < LFPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
		else if(counterX < LSPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b0, 1'b1 };		
		else
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
	end
	else if(counterY < FFPVal)
	begin
		if(counterX < LFPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
		else if(counterX < LSPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b0, 1'b1 };
		else
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
	end
	else if(counterY < FSPVal)
	begin
		if(counterX < LFPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b0 };
		else if(counterX < LSPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b1, 1'b0, 1'b0, 1'b0 };			// note G also high here (VS^HS)
		else
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b0 };
	end
	else
	begin
		if(counterX < LFPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
		else if(counterX < LSPVal)
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b0, 1'b1 };
		else
			{ r, g, b, hs, vs } <= { 1'b0, 1'b0, 1'b0, 1'b1, 1'b1 };
	end

endmodule
		
