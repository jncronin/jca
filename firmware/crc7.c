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

/* Simple CRC7 routines for interfacing with SD card.
 *
 * Usage:
 * 	char crc = 0;
 * 	crc = crc7(crc, b0);
 * 	crc = crc7(crc, b1);
 * 		...
 * 	crc = crc7(crc, bn);
 * 	crc = crc7end(crc);
 */

static char shift_bit(char cur_val, char next_val)
{
	/* MSB is shifted out and examined. Next bit is added.
	 * If bit shifted out was 1, XOR val with 0x09
	 */

	char old_val = cur_val;
	cur_val <<= 1;
	cur_val |= next_val;
	if(old_val & 0x40)
		cur_val ^= 0x09;
	return cur_val;
}

char crc7(char cur_val, char next_val)
{
	int i;
	/* Bits are shifted in MSB first */
	for(i = 7; i >= 0; i--)
		cur_val = shift_bit(cur_val, (next_val >> i) & 0x1);
	return cur_val & 0x7f;
}

char crc7end(char cur_val)
{
	int i;
	/* Terminated by shifting in seven zeros */
	for(i = 0; i < 7; i++)
		cur_val = shift_bit(cur_val, 0);
	return cur_val & 0x7f;
}

/*void main()
{
	char crc1 = 0;
	crc1 = crc7(crc1, 0x12);
	crc1 = crc7(crc1, 0x34);
	crc1 = crc7(crc1, 0x56);
	crc1 = crc7(crc1, 0x78);
	crc1 = crc7(crc1, 0x9a);
	crc1 = crc7end(crc1);

	printf("%x\n\n", crc1);
} */


