.globl tick

irq:
	push lr;
	push r1;
	push r2;
	push r3;
	push r4;
	push r5;
	push r8;
	push r9;
	push r10;
	push r11;
	push r12;
	push r13;
	push r14;
	push r15;

	jlrel tick;

	pop r15;
	pop r14;
	pop r13;
	pop r12;
	pop r11;
	pop r10;
	pop r9;
	pop r8;
	pop r5;
	pop r4;
	pop r3;
	pop r2;
	pop r1;
	pop lr;

	// ignore irpt_val
	add sp, 4 -> sp;
	
	iret;

