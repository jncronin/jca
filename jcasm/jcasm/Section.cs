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
    partial class Program
    {
        static internal Dictionary<string, Section> sections = new Dictionary<string, Section>();
        static Section cur_section;

        static internal void RegisterSection(string name, string flags, string type, string flag_specific_arguments)
        {
            if (sections.ContainsKey(name))
                return;

            GetKnownSectionType(name, ref flags, ref type, ref flag_specific_arguments);

            Section s = new Section();
            s.name = name;
            if (flags == null)
                flags = "";
            if (type == null)
                type = "";
            if (flag_specific_arguments == null)
                flag_specific_arguments = "";

            // Default if flags is null is for not loaded or writeable
            if (flags == "")
            {
                s.Alloc = false;
                s.Write = false;
                s.Exec = false;
            }

            foreach(char c in flags)
            {
                switch(c)
                {
                    case 'a':
                        s.Alloc = true;
                        break;
                    case 'w':
                        s.Write = true;
                        s.Alloc = true;
                        break;
                    case 'x':
                        s.Exec = true;
                        s.Alloc = true;
                        break;
                }
            }

            // Default type is data (ProgBits)
            s.SectionType = Section.SectType.ProgBits;
            if (type == "progbits")
                s.SectionType = Section.SectType.ProgBits;
            else if (type == "nobits")
                s.SectionType = Section.SectType.NoBits;
            else if (type == "note")
                s.SectionType = Section.SectType.Note;
            else
                s.SectionType = Section.SectType.Unknown;

            sections[s.name] = s;
        }

        private static void GetKnownSectionType(string name, ref string flags, ref string type, ref string flag_specific_arguments)
        {
            // See if we know the section type
            string new_flags = null;
            string new_type = null;
            string new_flag_specific_arguments = null;

            if (name.StartsWith(".note."))
            {
                new_flags = "";
                new_type = "note";
                new_flag_specific_arguments = "";
            }
            else
                return;

            if (flags == null || flags == "")
                flags = new_flags;
            if (type == null || type == "")
                type = new_type;
            if (flag_specific_arguments == null || flag_specific_arguments == "")
                flag_specific_arguments = new_flag_specific_arguments;
        }
    }

    class Section
    {
        public string name;

        public enum SectType { ProgBits, NoBits, Note, Rela, SymTab, StrTab, Unknown };
        public SectType SectionType;
        public bool Write;
        public bool Alloc;
        public bool Exec;

        public int cur_offset = 0;
        public string cur_label = "";

        public int SectionIdx;
        public int RelocSectionIdx;
        public int ShStrNdx;
        public int FileOffset;
        public int SectionSize;
        public Dictionary<string, int> StringCache;

        public List<Relocation> relocs = new List<Relocation>();

        public List<byte> oput = new List<byte>();

        public override string ToString()
        {
            return name;
        }
    }

    class LabelOffset
    {
        public int Offset;
        public Section Section;
    }

    class CommonSymbol
    {
        public Expression Size;
        public Expression Align;
    }

    class SectionHeader : Statement
    {
        public string name;

        public override int OffsetAfter(int offset_before)
        {
            return offset_before;
        }
    }

    class Symbol
    {
        public string name;
        public int Offset;
        public Section Section;
        public int Index;
        public int StrNdx;
        public int Size;
        public bool IsGlobal = false;
        public bool IsCommon = false;
        public bool IsWeak = false;
        public int Type = 0;

        public override string ToString()
        {
            return name;
        }
    }
}
