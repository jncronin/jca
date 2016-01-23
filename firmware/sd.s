	.text
	.file	"sd.ll"
	.globl	sd_command
	.align	16
	.type	sd_command,@function
sd_command:                             // @sd_command
// BB#0:
	lit 40 -> R1 ;
	m R1 -> R1 ;
	sub SP, R1 -> SP ;
	lit 36 -> R1 ;
	add R1, SP -> R1 ;
	store R8 -> R1 ;
	lit 32 -> R1 ;
	add R1, SP -> R1 ;
	store R9 -> R1 ;
	lit 28 -> R1 ;
	add R1, SP -> R1 ;
	store R10 -> R1 ;
	lit 24 -> R1 ;
	add R1, SP -> R1 ;
	store R11 -> R1 ;
	lit 20 -> R1 ;
	add R1, SP -> R1 ;
	store R12 -> R1 ;
	lit 16 -> R1 ;
	add R1, SP -> R1 ;
	store R13 -> R1 ;
	add SP, 12 -> R1 ;
	store R14 -> R1 ;
	add SP, 8 -> R1 ;
	store R15 -> R1 ;
	add SP, 4 -> R1 ;
	store LR -> R1 ;
	store R4 -> SP ;
	add R3, 0 -> R14 ;
	mov 63 -> R3 ;
	and R2, R3 -> R2 ;
	mov 64 -> R3 ;
	or R2, R3 -> R11 ;
	mov 0 -> R9 ;
	add R9, 0 -> R2 ;
	add R11, 0 -> R3 ;
	jlrel crc7 ;
	mov 24 -> R3 ;
	rsh R14, R3 -> R12 ;
	add R12, 0 -> R3 ;
	jlrel crc7 ;
	mov 16 -> R3 ;
	rsh R14, R3 -> R3 ;
	mov 255 -> R10 ;
	and R3, R10 -> R13 ;
	add R13, 0 -> R3 ;
	jlrel crc7 ;
	rsh R14, 8 -> R3 ;
	and R3, R10 -> R15 ;
	add R15, 0 -> R3 ;
	jlrel crc7 ;
	and R14, R10 -> R14 ;
	add R14, 0 -> R3 ;
	jlrel crc7 ;
	jlrel crc7end ;
	add R2, 0 -> R8 ;
	add R9, 0 -> R2 ;
	jlrel spi_devsel ;
	add R11, 0 -> R2 ;
	jlrel spi_tfer ;
	add R12, 0 -> R2 ;
	jlrel spi_tfer ;
	add R13, 0 -> R2 ;
	jlrel spi_tfer ;
	add R15, 0 -> R2 ;
	jlrel spi_tfer ;
	add R14, 0 -> R2 ;
	jlrel spi_tfer ;
	lsh R8, 1 -> R2 ;
	or R2, 1 -> R2 ;
	and R2, R10 -> R2 ;
	jlrel spi_tfer ;
.LBB0_1:                                // =>This Inner Loop Header: Depth=1
	add R10, 0 -> R2 ;
	jlrel spi_tfer ;
	add R2, 0 -> R11 ;
	sub R11, R10 -> R2 ;
	jrel(z R2) .LBB0_1 ;
	jrel .LBB0_2 ;
.LBB0_2:
	load SP -> R10 ;
	sub R10, R9 -> R2 ;
	jrel(z R2) .LBB0_5 ;
	jrel .LBB0_3 ;
.LBB0_3:
	mov 126 -> R2 ;
	and R11, R2 -> R2 ;
	sub R2, R9 -> R2 ;
	jrel(nz R2) .LBB0_5 ;
	jrel .LBB0_4 ;
.LBB0_4:                                // %.loopexit27
	store R9 -> R10 ;
	mov 255 -> R8 ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	load R10 -> R3 ;
	or R3, R2 -> R2 ;
	lsh R2, 8 -> R2 ;
	store R2 -> R10 ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	load R10 -> R3 ;
	or R3, R2 -> R2 ;
	lsh R2, 8 -> R2 ;
	store R2 -> R10 ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	load R10 -> R3 ;
	or R3, R2 -> R2 ;
	lsh R2, 8 -> R2 ;
	store R2 -> R10 ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	load R10 -> R3 ;
	or R3, R2 -> R2 ;
	store R2 -> R10 ;
.LBB0_5:
	mov 255 -> R8 ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	jlrel spi_devdesel ;
	add R8, 0 -> R2 ;
	jlrel spi_tfer ;
	add R11, 0 -> R2 ;
	add SP, 4 -> R1 ;
	load R1 -> LR ;
	add SP, 8 -> R1 ;
	load R1 -> R15 ;
	add SP, 12 -> R1 ;
	load R1 -> R14 ;
	lit 16 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R13 ;
	lit 20 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R12 ;
	lit 24 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R11 ;
	lit 28 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R10 ;
	lit 32 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R9 ;
	lit 36 -> R1 ;
	add R1, SP -> R1 ;
	load R1 -> R8 ;
	lit 40 -> R1 ;
	m R1 -> R1 ;
	add SP, R1 -> SP ;
	j LR;
.Lfunc_end0:
	.size	sd_command, .Lfunc_end0-sd_command


	.ident	"clang version 3.7.0 (tags/RELEASE_370/final)"
	.section	".note.GNU-stack","",@progbits
