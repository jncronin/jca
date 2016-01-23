CLANG=clang
LLC=llc
JCASM=/cygdrive/c/Users/jncro/Documents/Visual\ Studio\ 2015/Projects/jcasm/jcasm/bin/Debug/jcasm.exe
TL=~/tysos/tl/tl/bin/Debug/tl.exe

OPTFLAGS = -O2
CFLAGS = -emit-llvm --target=arm-none-elf-eabi -ffreestanding -S $(OPTFLAGS)
LLCFLAGS = -march=jca $(OPTFLAGS)

OBJFILES=pong.o int.o uart.o
LOADEROBJ = init.o loader.o spi.o
OUTPUT=prog

#this stops make using a built-in rule for .o files
.SUFFIXES:

all: $(OUTPUT).bin $(OUTPUT).elf $(OUTPUT).hex loader.bin loader.elf loader.hex

%.ll: %.c
	$(CLANG) $(CFLAGS) -o $@ $<

%.s: %.ll
	$(LLC) $(LLCFLAGS) -o $@ $<

%.o: %.s
	$(JCASM) -o $@ $<

$(OUTPUT).bin: $(OBJFILES)
	$(TL) $(TLFLAGS) --arch=jca-elf-prog -o $@ --oformat=binary $^

$(OUTPUT).elf: $(OBJFILES)
	$(TL) $(TLFLAGS) --arch=jca-elf-prog -o $@ --oformat=elf $^

$(OUTPUT).hex: $(OBJFILES)
	$(TL) $(TLFLAGS) --arch=jca-elf-prog -o $@ --oformat=hex $^

loader.bin: $(LOADEROBJ)
	$(TL) $(TLFLAGS) --arch=jca-elf-none -o $@ --oformat=binary $^

loader.elf: $(LOADEROBJ)
	$(TL) $(TLFLAGS) --arch=jca-elf-none -o $@ --oformat=elf $^

loader.hex: $(LOADEROBJ)
	$(TL) $(TLFLAGS) --arch=jca-elf-none -o $@ --oformat=hex $^

hello.hex: init.o hello.o
	$(TL) $(TLFLAGS) -o $@ --oformat=hex $^

hello.bin: init.o hello.o
	$(TL) $(TLFLAGS) -o $@ --oformat=binary $^

.PHONY: clean

clean:
	rm *.o *.elf *.bin *.hex

