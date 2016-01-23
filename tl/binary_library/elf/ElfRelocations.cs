/* Copyright (C) 2013-2015 by John Cronin
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

using System;
using System.Collections.Generic;
using System.Text;

namespace binary_library.elf
{
    partial class ElfFile
    {
        Dictionary<int, Dictionary<int, IRelocationType>> reloc_types;

        void InitRelocTypes()
        {
            reloc_types = new Dictionary<int, Dictionary<int, IRelocationType>>();
            for (int i = 0; i < 255; i++)
                reloc_types[i] = new Dictionary<int, IRelocationType>();

            reloc_types[EM_X86_64][R_X86_64_64] = new Rel_x86_64_64();
            reloc_types[EM_X86_64][R_X86_64_PC32] = new Rel_x86_64_pc32();
            reloc_types[EM_X86_64][R_X86_64_32] = new Rel_x86_64_32();

            reloc_types[EM_JCA] = new Dictionary<int, IRelocationType>();
            reloc_types[EM_JCA][R_JCA_LITR1] = new Rel_JCA_LitR1();
            reloc_types[EM_JCA][R_JCA_LIT] = new Rel_JCA_Lit();
            reloc_types[EM_JCA][R_JCA_SRCA] = new Rel_JCA_Srca();
            reloc_types[EM_JCA][R_JCA_SRCB] = new Rel_JCA_Srcb();
            reloc_types[EM_JCA][R_JCA_SRCAB] = new Rel_JCA_Srcab();
            reloc_types[EM_JCA][R_JCA_SRCBCOND] = new Rel_JCA_Srcbcond();
            reloc_types[EM_JCA][R_JCA_SRCABCOND] = new Rel_JCA_Srcabcond();
            reloc_types[EM_JCA][R_JCA_SRCAREL] = new Rel_JCA_SrcaRel();
            reloc_types[EM_JCA][R_JCA_SRCBREL] = new Rel_JCA_SrcbRel();
            reloc_types[EM_JCA][R_JCA_SRCABREL] = new Rel_JCA_SrcabRel();
            reloc_types[EM_JCA][R_JCA_SRCBCONDREL] = new Rel_JCA_SrcbcondRel();
            reloc_types[EM_JCA][R_JCA_SRCABCONDREL] = new Rel_JCA_SrcabcondRel();
        }

        /* x86_64 relocations */
        const int R_X86_64_NONE = 0;
        const int R_X86_64_64 = 1;
        const int R_X86_64_PC32 = 2;
        const int R_X86_64_GOT32 = 3;
        const int R_X86_64_PLT32 = 4;
        const int R_X86_64_COPY = 5;
        const int R_X86_64_GLOB_DAT = 6;
        const int R_X86_64_JUMP_SLOT = 7;
        const int R_X86_64_RELATIVE = 8;
        const int R_X86_64_GOTPCREL = 9;
        const int R_X86_64_32 = 10;
        const int R_X86_64_32S = 11;
        const int R_X86_64_16 = 12;
        const int R_X86_64_PC16 = 13;
        const int R_X86_64_8 = 14;
        const int R_X86_64_PC8 = 15;
        const int R_X86_64_DTPMOD64 = 16;
        const int R_X86_64_DTPOFF64 = 17;
        const int R_X86_64_TPOFF64 = 18;
        const int R_X86_64_TLSGD = 19;
        const int R_X86_64_TLSLD = 20;
        const int R_X86_64_DTPOFF32 = 21;
        const int R_X86_64_GOTTPOFF = 22;
        const int R_X86_64_TPOFF32 = 23;
        const int R_X86_64_PC64 = 24;
        const int R_X86_64_GOTOFF64 = 25;
        const int R_X86_64_GOTPC32 = 26;
        const int R_X86_64_SIZE32 = 32;
        const int R_X86_64_SIZE64 = 33;
        const int R_X86_64_GOTPC32_TLSDESC = 34;
        const int R_X86_64_TLSDESC_CALL = 35;
        const int R_X86_64_TLSDESC = 36;

        class Rel_x86_64_32 : IRelocationType
        {
            public int Length
            {
                get { return 4; }
            }

            public ulong KeepMask
            {
                get { return 0; }
            }

            public ulong SetMask
            {
                get { return 0xffffffff; }
            }

            public string Name
            {
                get { return "R_X86_64_32"; }
            }

            public int Type
            {
                get { return (int)R_X86_64_32; }
            }

            public long Evaluate(IRelocation reloc)
            {
                // S + A
                return (long)(reloc.References.DefinedIn.LoadAddress + reloc.References.Offset) + reloc.Addend;
            }

            public long GetCurrentValue(IRelocation reloc)
            {
                throw new NotImplementedException();
            }

            public int BitLength
            { get { return 32; } }
            public bool IsSigned
            { get { return false; } }
            public int BitOffset
            { get { return 0; } }
        }

        class Rel_x86_64_pc32 : IRelocationType
        {
            public int Length
            {
                get { return 4; }
            }

            public ulong KeepMask
            {
                get { return 0; }
            }

            public ulong SetMask
            {
                get { return 0xffffffff; }
            }

            public string Name
            {
                get { return "R_X86_64_PC32"; }
            }

            public int Type
            {
                get { return (int)R_X86_64_PC32; }
            }

            public long Evaluate(IRelocation reloc)
            {
                // S + A - P
                return (long)(reloc.References.DefinedIn.LoadAddress + reloc.References.Offset) + reloc.Addend - (long)(reloc.DefinedIn.LoadAddress + reloc.Offset);
            }

            public long GetCurrentValue(IRelocation reloc)
            {
                throw new NotImplementedException();
            }

            public int BitLength
            { get { return 32; } }
            public bool IsSigned
            { get { return true; } }
            public int BitOffset
            { get { return 0; } }
        }

        class Rel_x86_64_64 : IRelocationType
        {
            public int Length
            {
                get { return 8; }
            }

            public ulong KeepMask
            {
                get { return 0; }
            }

            public ulong SetMask
            {
                get { return 0xffffffffffffffff; }
            }

            public string Name
            {
                get { return "R_X86_64_64"; }
            }

            public int Type
            {
                get { return (int)R_X86_64_64; }
            }

            public long Evaluate(IRelocation reloc)
            {
                // S + A
                return (long)(reloc.References.DefinedIn.LoadAddress + reloc.References.Offset) + reloc.Addend;
            }

            public long GetCurrentValue(IRelocation reloc)
            {
                throw new NotImplementedException();
            }

            public int BitLength
            { get { return 64; } }
            public bool IsSigned
            { get { return false; } }
            public int BitOffset
            { get { return 0; } }
        }
    }
}
