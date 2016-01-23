// Internal RAM

module ram(clk, rst_, cs_, oe_, we_, addr, data);
	input clk;
	input rst_;
	input cs_;
	input oe_;
	input we_;
	input [15:0] addr;
	inout [7:0] data;
	
parameter ByteCount = 256;

reg [7:0] m [ByteCount - 1: 0];
integer i;
//reg [7:0] data;

// Output always occurs if cs and oe are low
assign data = (~cs_ & ~oe_) ? m[addr] : 8'bZ;

// Input only occurs on clock edges when cs and we are low
always @(posedge clk)
	if(~rst_)
	begin
		for (i = 0; i < ByteCount; i=i+1)	m[i] <= 8'b0;
	end
	else if(cs_ == 0)
		if(we_ == 0)
			m[addr] <= data;

endmodule

module ram_tb;

reg clk;
reg cs;
reg oe;
reg we;
reg [15:0] addr;
wire [7:0] data;

reg [7:0] d;

ram r(.clk(clk), .cs_(cs), .oe_(oe), .we_(we), .addr(addr), .data(data));

assign data = (we == 0) ? d : 8'bZ;

initial
begin
	clk = 0;
	cs = 1;
	oe = 1;
	we = 1;
	addr = 16'd0;
	d <= 8'd0;
	
	#50 addr = 16'd2;
	d = 8'hAA;
	
	#2 cs = 0;
	we = 0;
	#2 cs = 1;
	we = 1;
	
	addr = 16'd1;
	d = 8'h55;
	
	#2 cs = 0;
	we = 0;
	#2 cs = 1;
	we = 1;
	
	addr = 16'd2;
	#2 cs = 0;
	oe = 0;
end

always #1 clk = ~clk;

endmodule
