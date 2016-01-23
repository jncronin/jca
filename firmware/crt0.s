// crt initialization for JCA

.globl _start
_start:
lit 0x00480000 -> R7;
lit main -> R1;
jl R1;
.Lhalt: jrel .Lhalt;

