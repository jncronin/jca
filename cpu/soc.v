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
 
// Modelsim-ASE requires a timescale directive
`timescale 1 ps / 1 ps

module soc(inclk, rst_, addr, data, dbg, cs1_, cs2_, oe_, we_, uart_TxD, vga_r, vga_g, vga_b, vga_hs, vga_vs, spi_ncs, spi_sclk, spi_mosi, spi_miso);
	parameter DEBUG_MAX_PIN = 7;
	input inclk;
	input rst_;
	inout [7:0] data;
	output [31:0] addr;
	output [DEBUG_MAX_PIN:0] dbg;
	output we_;
	output cs1_;
	output cs2_;
	output oe_;
	output uart_TxD;
	output vga_r;
	output vga_g;
	output vga_b;
	output vga_hs;
	output vga_vs;
	output [7:0] spi_ncs;
	output spi_sclk;
	output spi_mosi;
	input spi_miso;



wire cs_;
wire cs0_;
wire cs3_;
wire cs4_;
wire cs5_;
wire cs6_;
wire cs7_;
wire [7:0] rom_data;

/* clock divider - for testing */
wire clk;	
assign clk = inclk;
//test_pll pll(.inclk0(inclk), .c0(clk));


/* Debounce reset switch */
wire new_rst_;
debounce d_rst(.clk(clk), .PB(rst_), .PB_state(new_rst_));

// connect together all modules into a single design
// cpu core
wire [31:0] cpudbg;
assign dbg = cpudbg[DEBUG_MAX_PIN:0];
wire cpu_int;
wire cpu_int_ack;
wire [31:0] irpts;
cpu c(.clk(clk), .rst_(new_rst_), .data(data), .addr(addr), .dbg(cpudbg), .cs_(cs_), .oe_(oe_), .we_(we_), .cpu_int(cpu_int), .cpu_int_ack(cpu_int_ack));



// interprets upper parts of address to discrete active low chip select signals
cselect cs(.addr(addr), .cs_(cs_), .cs0(cs0_), .cs1(cs1_), .cs2(cs2_), .cs3(cs3_), .cs4(cs4_), .cs5(cs5_), .cs6(cs6_), .cs7(cs7_));

// Altera ROM including firmware - starts at offset 0x0.  ROM output is not tristated, so a separate
//  rom_tristate module does this for us
rom r(.clock(clk), .address(addr[11:0]), .q(rom_data), .data(data), .wren(~cs0_ & ~we_));
rom_tristate r_ts(.cs_(cs0_), .oe_(oe_), .data_in(rom_data), .data_out(data));

// UART at 12MiB
uart_mmio #(2604) uart(.clk(clk), .rst_(new_rst_), .data(data), .addr(addr[7:0]), .cs_(cs3_), .oe_(oe_), .we_(we_), .TxD(uart_TxD));

// VGA at 16MiB
vga vga(.clk(clk), .data(data), .addr(addr[11:0]), .cs_(cs4_), .oe_(oe_), .we_(we_), .r(vga_r), .g(vga_g), .b(vga_b), .hs(vga_hs), .vs(vga_vs));

// SPI at 20MiB
spi spi(.clk(clk), .data(data), .rst_(new_rst_), .addr(addr[7:0]), .cs_(cs5_), .oe_(oe_), .we_(we_), .spi_ncs(spi_ncs), .spi_mosi(spi_mosi), .spi_miso(spi_miso), .spi_sclk(spi_sclk));

// Timer at 24 MiB
timer timer(.clk(clk), .data(data), .rst_(new_rst_), .addr(addr[7:0]), .cs_(cs6_), .oe_(oe_), .we_(we_), .interrupt(irpts[0]));

// interrupt controller at 28 MiB
irq_ctrl irq(.clk(clk), .rst_(new_rst_), .data(data), .addr(addr[7:0]), .cs_(cs7_), .oe_(oe_), .we_(we_), .irpts(irpts), .cpu_int(cpu_int), .cpu_int_ack(cpu_int_ack));

endmodule


// Testbench for use in ModelSim
module soc_tb;

reg inclk;
reg rst_;

wire [31:0] addr;
wire [7:0] data;
wire cs_1;
wire cs_2;
wire oe_;
wire we_;
wire clk;
reg spi_miso;

test_ram_if r1(.clk(inclk), .addr(addr[18:0]), .data(data), .cs_(cs_1), .oe_(oe_), .we_(we_));
test_ram_if r2(.clk(inclk), .addr(addr[18:0]), .data(data), .cs_(cs_2), .oe_(oe_), .we_(we_));

soc s(.inclk(inclk), .rst_(rst_), .addr(addr), .data(data), .cs1_(cs_1), .cs2_(cs_2), .oe_(oe_), .we_(we_), .spi_miso(spi_miso));

initial
begin
	inclk <= 0;
	rst_ <= 1;
	spi_miso <= 1;
	
	#5 rst_ <= 0;
	#2 rst_ <= 1;
end

always #1 inclk <= ~inclk;

endmodule

module test_ram_if(clk, addr, data, cs_, oe_, we_);
	input clk;
	input [18:0] addr;
	inout [7:0] data;
	input cs_;
	input oe_;
	input we_;
	
wire [7:0] data_out;
assign data = (~cs_ & ~oe_) ? data_out : 8'bzzzzzzzz;
	
ram_for_test r(.clock(clk), .address(addr), .data(data), .wren(~cs_ & ~we_), .q(data_out));

endmodule

