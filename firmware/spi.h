#ifndef SPI_H
#define SPI_H

#include <stdint.h>

void spi_clkdiv(unsigned int d);
void spi_devsel(int n);
void spi_devdesel();
char spi_tfer(char v);

#endif

