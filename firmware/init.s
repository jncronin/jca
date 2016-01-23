// basic stack setup for JCA.  Link as first object in each binary

lit 0x00480000 -> R7;
lit main -> R1;
jl R1;
.Lhalt: jrel .Lhalt;

