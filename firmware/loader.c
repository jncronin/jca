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
#include <stddef.h>
#include "spi.h"
#include "uart.h"

#define EEPROM_CSID	1
#define EEPROM_COUNT	4
#define EEPROM_SIZE	0x20000

// load up data from eeprom
static void load_eeprom(int eeprom_addr, size_t len, void *dest)
{
	/* eeprom is divided into EEPROM_COUNT devices, each of
	 * EEPROM_SIZE bytes in length.  They support continued
	 * reading for as long as required, based on a particular
	 * start address */

	uint8_t *b = (uint8_t *)dest;

	while(len)
	{
		int cur_eeprom_addr = eeprom_addr & ~EEPROM_SIZE;
		int cur_eeprom_idx = eeprom_addr / EEPROM_SIZE;
		if(cur_eeprom_idx > EEPROM_COUNT)
			return;
		cur_eeprom_idx += EEPROM_CSID;

		// deselect old device if necessary
		spi_devdesel();

		// select new device
		spi_devsel(cur_eeprom_idx);

		// send read command
		spi_tfer(0x03);
		spi_tfer((cur_eeprom_addr >> 16) & 0xff);
		spi_tfer((cur_eeprom_addr >> 8) & 0xff);
		spi_tfer(cur_eeprom_addr & 0xff);

		// read bytes
		while(cur_eeprom_addr < EEPROM_SIZE && len)
		{
			*b++ = (uint8_t)spi_tfer(0xff);
			len--;
			cur_eeprom_addr++;
			eeprom_addr++;
		}

		// deselect
		spi_devdesel();
	}
}

struct Elf32_Phdr
{
	uint32_t p_type;
	uint32_t p_offset;
	uint32_t p_vaddr;
	uint32_t p_paddr;
	uint32_t p_filesz;
	uint32_t p_memsz;
	uint32_t p_flags;
	uint32_t p_align;
};

void main()
{
	uint32_t e_phoff;
	uint32_t e_phentsize = 0;
	uint32_t e_phnum = 0;
	uint32_t e_entry = 0;

	load_eeprom(28, 4, &e_phoff);
	load_eeprom(42, 2, &e_phentsize);
	load_eeprom(44, 2, &e_phnum);
	load_eeprom(24, 4, &e_entry);

	for(uint32_t ph_idx = 0; ph_idx < e_phnum; ph_idx++)
	{
		struct Elf32_Phdr ph;
		load_eeprom(e_phoff + ph_idx * e_phentsize,
				sizeof(struct Elf32_Phdr),
				&ph);

		if(ph.p_type == 1)
		{
			// PT_LOAD
			uintptr_t addr = ph.p_vaddr;
			
			if(ph.p_filesz)
			{
				load_eeprom(ph.p_offset, ph.p_filesz,
						(void *)addr);
				addr += ph.p_filesz;
			}
			for(int i = 0; i < ph.p_memsz - ph.p_filesz; i++, addr++)
				*(uint8_t *)addr = 0;
		}
	}

	((void (*)())e_entry)();



	while(1);
}

