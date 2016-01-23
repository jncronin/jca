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
 
module debounce(
    clk,
    PB,
    PB_state
);
	input clk;
	input PB;
	output PB_state;

reg init_state = 1'b0;
reg PB_state = 1'b0;

// Next declare a 16-bits counter
reg [11:0] PB_cnt = 12'd0;
wire PB_cnt_max = &PB_cnt;
wire PB_idle = (PB_cnt == 12'd0);
wire PB_changed = (PB != init_state);

always @(posedge clk)
if(PB_idle & PB_changed)
begin
	init_state = PB;
	PB_cnt = 12'd1;
end
else if(PB_cnt_max)
begin
	PB_state = init_state;
	PB_cnt = 12'd0;
end
else if(~PB_idle)
	PB_cnt = PB_cnt + 12'd1;

endmodule