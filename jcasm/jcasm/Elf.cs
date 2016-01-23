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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jcasm
{
    class Elf
    {
		static internal void GenerateELF(System.IO.BinaryWriter w)
		{
            /* First, build the section table.  We only include those
			sections that have some data defined.

			Section 0 is the null section.  We also include sections for
			the string and symbol tables
			*/

            MakeState ms = new MakeState(Program.los, null);

            List<Section> sects = new List<Section>();
            sects.Add(null);
            int cur_sect = 1;
            foreach(var s in Program.sections)
            {
                if (s.Value.cur_offset == 0)
                    continue;
                if (s.Value.SectionType == Section.SectType.Unknown)
                    continue;

                s.Value.SectionIdx = cur_sect++;
                sects.Add(s.Value);

                Section s_reloc = new Section();
                foreach(var r in Program.relocs)
                {
                    if(r.SourceSection == s.Value)
                        s_reloc.relocs.Add(r);
                }

                if(s_reloc.relocs.Count > 0)
                {
                    s_reloc.name = s.Value.name + ".rela";
                    s_reloc.SectionType = Section.SectType.Rela;
                    s_reloc.SectionIdx = cur_sect++;
                    s.Value.RelocSectionIdx = s_reloc.SectionIdx;
                    s_reloc.RelocSectionIdx = s.Value.SectionIdx;
                    sects.Add(s_reloc);
                }
            }

            // Add symbol, symbol string and section header string tables
            Section symtab = new Section();
            symtab.name = ".symtab";
            symtab.SectionIdx = cur_sect++;
            symtab.SectionType = Section.SectType.SymTab;
            sects.Add(symtab);

            Section strtab = new Section();
            strtab.name = ".strtab";
            strtab.SectionIdx = cur_sect++;
            strtab.SectionType = Section.SectType.StrTab;
            sects.Add(strtab);

            Section shstrtab = new Section();
            shstrtab.name = ".shstrtab";
            shstrtab.SectionIdx = cur_sect++;
            shstrtab.SectionType = Section.SectType.StrTab;
            sects.Add(shstrtab);

            // Add strings to section string table
            foreach(Section s in sects)
            {
                if (s == null)
                    continue;
                s.ShStrNdx = AllocateString(s.name, shstrtab);
            }

            // Build symbol table
            Dictionary<string, Symbol> syms = new Dictionary<string, Symbol>();
            foreach(var v in Program.los)
            {
                Symbol newsym = new Symbol();
                newsym.name = v.Key;
                newsym.StrNdx = AllocateString(newsym.name, strtab);
                newsym.Section = v.Value.Section;
                newsym.Offset = v.Value.Offset;

                if(Program.comm_objs.ContainsKey(newsym.name))
                {
                    newsym.IsCommon = true;
                    newsym.Offset = Program.comm_objs[newsym.name].Align.Evaluate(ms).AsInt;
                    newsym.Size = Program.comm_objs[newsym.name].Size.Evaluate(ms).AsInt;
                }

                if(Program.obj_sizes.ContainsKey(newsym.name))
                {
                    newsym.Size = Program.obj_sizes[newsym.name].Evaluate(ms).AsInt;
                }

                if(Program.obj_types.ContainsKey(newsym.name))
                {
                    string t = Program.obj_types[newsym.name];
                    if (t == "function")
                        newsym.Type = 2;
                    else if (t == "object")
                        newsym.Type = 1;
                }

                if(Program.global_objs.ContainsKey(newsym.name))
                {
                    newsym.IsGlobal = true;
                }

                syms[newsym.name] = newsym;
            }
            foreach(var v in Program.comm_objs)
            {
                if (syms.ContainsKey(v.Key))
                    continue;

                Symbol newsym = new Symbol();
                newsym.name = v.Key;
                newsym.StrNdx = AllocateString(newsym.name, strtab);
                newsym.IsCommon = true;

                if(Program.global_objs.ContainsKey(newsym.name))
                    newsym.IsGlobal = true;
                if (Program.obj_types.ContainsKey(newsym.name))
                {
                    string t = Program.obj_types[newsym.name];
                    if (t == "function")
                        newsym.Type = 2;
                    else if (t == "object")
                        newsym.Type = 1;
                }

                newsym.Size = Program.comm_objs[newsym.name].Size.Evaluate(ms).AsInt;
                newsym.Offset = Program.comm_objs[newsym.name].Align.Evaluate(ms).AsInt;

                syms[newsym.name] = newsym;
            }
            foreach(var v in Program.relocs)
            {
                if (syms.ContainsKey(v.TargetName))
                    continue;

                Symbol newsym = new Symbol();
                newsym.name = v.TargetName;
                newsym.StrNdx = AllocateString(newsym.name, strtab);
                newsym.IsGlobal = true;
                newsym.Section = null;

                syms[newsym.name] = newsym;
            }

            // Symbols start with a null symbol, then all local symbols,
            //  then global ones.
            List<Symbol> sym_list = new List<Symbol>();
            sym_list.Add(null);

            // Add symbols for each section
            foreach(Section s in sects)
            {
                if (s == null)
                    continue;
                Symbol sect_sym = new Symbol();
                sect_sym.name = s.name;
                sect_sym.StrNdx = AllocateString(sect_sym.name, strtab);
                sect_sym.Index = sym_list.Count;
                sect_sym.Type = 3;
                sect_sym.Section = s;
                sect_sym.Offset = 0;
                sect_sym.Size = s.oput.Count;
                sym_list.Add(sect_sym);
            }

            // Now iterate through looking for local symbols
            foreach(var v in syms)
            {
                if (v.Value.IsGlobal)
                    continue;
                v.Value.Index = sym_list.Count();
                sym_list.Add(v.Value);
            }

            int max_local = sym_list.Count();

            // Now add global symbols
            foreach(var v in syms)
            {
                if (!v.Value.IsGlobal)
                    continue;
                v.Value.Index = sym_list.Count();
                sym_list.Add(v.Value);
            }

            // Extract a list of symbol indices
            Dictionary<string, int> sym_idx = new Dictionary<string, int>();
            foreach(var v in sym_list)
            {
                if (v == null)
                    continue;
                sym_idx[v.name] = v.Index;
            }

            // Begin writing out the elf file header
            w.Write((byte)0x7f);
            w.Write((byte)'E');
            w.Write((byte)'L');
            w.Write((byte)'F');
            w.Write((byte)1);
            w.Write((byte)1);
            w.Write((byte)1);
            for (int i = 0; i < 9; i++)
                w.Write((byte)0);

            w.Write((ushort)1);     // relocatable
            w.Write((byte)'J');     // Machine type 'J', 'C'
            w.Write((byte)'C');

            w.Write((uint)1);       // version
            w.Write((uint)0);       // e_entry
            w.Write((uint)0);       // e_phoff

            long e_shoff_offset = w.BaseStream.Position;
            w.Write((uint)0);       // e_shoff

            w.Write((uint)0);       // e_flags
            w.Write((ushort)52);      // e_ehsize
            w.Write((ushort)0);     // e_phentsize
            w.Write((ushort)0);     // e_phnum
            w.Write((ushort)40);    // e_shentsize
            w.Write((ushort)cur_sect);  // e_shnum
            w.Write((ushort)shstrtab.SectionIdx);   // e_shstrndx

            // Now write out the section data
            foreach(Section s in sects)
            {
                if (s == null)
                    continue;

                // align up to a multiple of 16
                while ((w.BaseStream.Position & 0xf) != 0)
                    w.Write((byte)0);

                switch(s.SectionType)
                {
                    case Section.SectType.NoBits:
                        s.SectionSize = s.cur_offset;
                        break;

                    case Section.SectType.ProgBits:
                    case Section.SectType.Note:
                    case Section.SectType.StrTab:
                        s.FileOffset = (int)w.BaseStream.Position;
                        foreach (byte b in s.oput)
                            w.Write(b);
                        s.SectionSize = (int)w.BaseStream.Position - s.FileOffset;
                        break;

                    case Section.SectType.SymTab:
                        s.FileOffset = (int)w.BaseStream.Position;

                        foreach(var sym in sym_list)
                        {
                            if(sym == null)
                            {
                                w.Write((uint)0);
                                w.Write((uint)0);
                                w.Write((uint)0);
                                w.Write((uint)0);
                            }
                            else
                            {
                                w.Write(sym.StrNdx);
                                w.Write(sym.Offset);
                                w.Write(sym.Size);

                                uint st_info = 0;
                                if (sym.IsWeak)
                                    st_info |= 0x20;
                                else if (sym.IsGlobal)
                                    st_info |= 0x10;
                                st_info |= (uint)sym.Type;

                                w.Write((byte)st_info);
                                w.Write((byte)0);

                                if (sym.IsCommon)
                                    w.Write((ushort)0xfff2);
                                else if (sym.Section == null)
                                    w.Write((ushort)0x0);
                                else
                                    w.Write((ushort)sym.Section.SectionIdx);
                            }
                        }

                        s.SectionSize = (int)w.BaseStream.Position - s.FileOffset;
                        break;

                    case Section.SectType.Rela:
                        s.FileOffset = (int)w.BaseStream.Position;

                        foreach(var r in s.relocs)
                        {
                            if (r == null)
                            {
                                w.Write((uint)0);
                                w.Write((uint)0);
                                w.Write((uint)0);
                            }
                            else
                            {
                                w.Write(r.SourceOffset);

                                uint sym_tab_idx = (uint)sym_idx[r.TargetName];
                                uint r_info = sym_tab_idx << 8;
                                r_info |= (uint)r.Type;
                                w.Write(r_info);

                                w.Write(r.Addend);
                            }
                        }

                        s.SectionSize = (int)w.BaseStream.Position - s.FileOffset;
                        break;
                }
            }

            // Align up to a multiple of 16
            while ((w.BaseStream.Position & 0xf) != 0)
                w.Write((byte)0);

            // Store section table offset back in the file header
            long shoff = w.BaseStream.Position;
            w.Seek((int)e_shoff_offset, System.IO.SeekOrigin.Begin);
            w.Write((uint)shoff);
            w.Seek((int)shoff, System.IO.SeekOrigin.Begin);

            // Write out section table
            foreach(var s in sects)
            {
                if(s == null)
                {
                    for (int i = 0; i < 10; i++)
                        w.Write((uint)0);
                }
                else
                {
                    w.Write((uint)s.ShStrNdx);

                    switch(s.SectionType)
                    {
                        case Section.SectType.NoBits:
                            w.Write((uint)8);
                            break;
                        case Section.SectType.Note:
                            w.Write((uint)7);
                            break;
                        case Section.SectType.ProgBits:
                            w.Write((uint)1);
                            break;
                        case Section.SectType.Rela:
                            w.Write((uint)4);
                            break;
                        case Section.SectType.StrTab:
                            w.Write((uint)3);
                            break;
                        case Section.SectType.SymTab:
                            w.Write((uint)2);
                            break;
                        case Section.SectType.Unknown:
                            w.Write((uint)0);
                            break;
                    }

                    uint sh_flags = 0;
                    if (s.Alloc)
                        sh_flags |= 0x2;
                    if (s.Write)
                        sh_flags |= 0x1;
                    if (s.Exec)
                        sh_flags |= 0x4;
                    w.Write(sh_flags);

                    w.Write((uint)0);   // sh_addr

                    w.Write((uint)s.FileOffset);
                    w.Write((uint)s.SectionSize);

                    switch(s.SectionType)
                    {
                        case Section.SectType.Rela:
                            w.Write((uint)symtab.SectionIdx);
                            w.Write((uint)s.RelocSectionIdx);
                            break;
                        case Section.SectType.SymTab:
                            w.Write((uint)strtab.SectionIdx);
                            w.Write((uint)max_local);
                            break;
                        default:
                            w.Write((uint)0);
                            w.Write((uint)0);
                            break;
                    }

                    w.Write((uint)4);       // sh_addralign

                    switch(s.SectionType)
                    {
                        case Section.SectType.Rela:
                            w.Write((uint)12);
                            break;
                        case Section.SectType.SymTab:
                            w.Write((uint)16);
                            break;
                        default:
                            w.Write((uint)0);
                            break;
                    }
                }
            }			
		}

        private static int AllocateString(string name, Section strtab)
        {
            if (strtab.StringCache == null)
                strtab.StringCache = new Dictionary<string, int>();
            else if (strtab.StringCache.ContainsKey(name))
                return strtab.StringCache[name];

            if(strtab.cur_offset == 0)
            {
                strtab.oput.Add(0);
                strtab.cur_offset++;
            }

            int ret = strtab.cur_offset;
            foreach(char c in name)
            {
                strtab.oput.Add((byte)c);
                strtab.cur_offset++;
            }
            strtab.oput.Add(0);
            strtab.cur_offset++;

            strtab.StringCache[name] = ret;
            return ret;
        }
    }
}
