# jca
A soft processor core implemented in Verilog

This is an example soft processor core with supporting files including assembler, LLVM backend and linker.  It is not designed to be fast but merely educational.  Including peripherals, it fits in the smallest Altera Cyclone II development board.  On my test set-up, the external memory bus is connected to two 512 kiB SRAM modules (Alliance AS6C4008).  The SPI pins are connected to an SD card as device 0 and 4x 128 kiB EEPROM modules (Microchip 25AA1024) as devices 1-4.

Contents
  - cpu               The JCA system-on-chip, including cpu, vga output, serial UART,
                        external SRAM interface and SPI interface (for connecting to
                        SD cards and EEPROM chips)
  - spi_program       A separate design for loading into an FPGA that it used for
                        programming EEPROM chips.  It is controlled over a serial link
                        from a PC running spiprog (in the jcasm folder)
  - firmware          Various firmware and test programs for running on JCA
  - llvm              LLVM backend patch for JCA
  - jcasm             C# solution including:
                        - jcasm     the JCA assembler
                        - jcemu     an emulator for JCA
                        - spiprog   used for programming the EEPROM on SPI devices 1-4
                        - makefont  generates a font for the VGA controller
  - tl                A linker for JCA
