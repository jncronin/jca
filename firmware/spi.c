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

#include <stdint.h>

#define SPI_BASE 		0x1400000
#define SPI_CMD			SPI_BASE
#define SPI_CLKDIV 		(SPI_BASE + 4)
#define SPI_DATA		(SPI_BASE + 8)

void spi_clkdiv(unsigned int d)
{
	*(volatile uint32_t *)SPI_CLKDIV = d;
}

void spi_devsel(int n)
{
	*(volatile uint32_t *)SPI_CMD = 0x80 | ((n & 0x7) << 4);
}

void spi_devdesel()
{
	*(volatile uint32_t *)SPI_CMD &= 0x0f;
}

char spi_tfer(char v)
{
	while(*(volatile uint32_t *)SPI_CMD & 0x2);
	*(volatile uint32_t *)SPI_DATA = v;
	*(volatile uint32_t *)SPI_CMD |= 0x2;
	while(*(volatile uint32_t *)SPI_CMD & 0x2);
	return (char)(*(volatile uint32_t *)SPI_DATA & 0xff);
}

