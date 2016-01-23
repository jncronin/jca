/* Copyright (C) 2016 by John Cronin
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
#include "spi.h"
#include "sd.h"
#include "uart.h"

static void putvga(int x, int y, char c)
{
	*(volatile uint8_t *)(0x1000000 + x + y * 128) = c;
}

static int vgax = 0;
static int vgay = 0;

static void putvgas(char *s)
{
	while(*s)
	{
		putvga(vgax, vgay, *s);
		vgax++;
		if(vgax == 80)
		{
			vgax = 0;
			vgay++;
		}
		if(vgay == 25)
		{
			vgay = 24;
		}

		s++;
	}
}

void irq();

void tick()
{
	puts("TICK ");
	putvgas("TICK ");

	// reset timer counter
	*(volatile uint32_t *)0x1800000 = 0;

	// send EOI
	*(volatile uint32_t *)0x1c00008 = 0;

}

void main()
{
	puts("Firmware starting... ");
	putvgas("Firmware starting... ");

	// set timer to tick every second
	*(volatile uint32_t *)0x1800004 = 50000000;

	// install interrupt handler
	*(volatile uint32_t *)0x4 = (uint32_t)irq;

	// unmask timer interrupt
	*(volatile uint32_t *)0x1c00004 = 0x1;

	while(1);
	//puts("Sending... ");
	//
	
	// send GO_IDLE_CMD, then keep sending 0xff until a response is received
	// 200 kHz
	spi_clkdiv(250);

	// send at least 74 cycles with CS high to give the card time to initialize
	for(int i = 0; i < 10; i++)
		spi_tfer(0xff);

	/*spi_devsel(0);
	spi_tfer(0x40);
	spi_tfer(0x00);		// arg
	spi_tfer(0x00);		// arg
	spi_tfer(0x00);		// arg
	spi_tfer(0x00);		// arg
	spi_tfer(0x95);		// crc

	char c;
	while((c = spi_tfer(0xff)) == 0xff);
	puthex(c);

	spi_devdesel(); */
	// CMD0
	puthex(sd_command(0, 0, (void *)0));
	// CMD8
	uint32_t cmd8;
	uint32_t cmd8_resp;
	cmd8_resp = sd_command(8, 0x000001aa, &cmd8);
	puthex(cmd8_resp);
	if(cmd8_resp == 0x1)
		putuint32(cmd8);

	uint32_t ocr;
	puthex(sd_command(58, 0x00000000, &ocr));
	putuint32(ocr);

	// ACMD41
	uint32_t acmd;
	do
	{
		puthex(sd_command(55, 0x00000000, (void*)0));
		acmd = sd_command(41, 0x00000000, (void*)0);
		puthex(acmd);
	} while(acmd == 0x1);

	if(acmd != 0x0)
		return;

	//puts("Sent");


	while(1);
}

