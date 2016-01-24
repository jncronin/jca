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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jcasm
{
    partial class Program
    {
        static internal Dictionary<string, int> regs = new Dictionary<string, int>();
        static Dictionary<string, byte> opcodes = new Dictionary<string, byte>();

        static internal Dictionary<string, Section> global_objs = new Dictionary<string, Section>();
        static internal List<string> extern_objs = new List<string>();
        static internal Dictionary<string, CommonSymbol> comm_objs = new Dictionary<string, CommonSymbol>();
        static internal Dictionary<string, string> obj_types = new Dictionary<string, string>();
        static internal Dictionary<string, Expression> obj_sizes = new Dictionary<string, Expression>();
        static internal List<Relocation> relocs = new List<Relocation>();
        static internal Dictionary<string, LabelOffset> los;

        static internal string cur_label;
        static string output_file = null;
        static string input_file = null;

        static Program()
        {
            regs["PC"] = 0;
            regs["R0"] = 0;
            regs["R1"] = 1;
            regs["R2"] = 2;
            regs["R3"] = 3;
            regs["R4"] = 4;
            regs["R5"] = 5;
            regs["R6"] = 6;
            regs["R7"] = 7;
            regs["R8"] = 8;
            regs["R9"] = 9;
            regs["R10"] = 10;
            regs["R11"] = 11;
            regs["R12"] = 12;
            regs["R13"] = 13;
            regs["R14"] = 14;
            regs["R15"] = 15;

            regs["LR"] = 6;
            regs["SP"] = 7;

            opcodes["load"] = 0;
            opcodes["store"] = 1;
            opcodes["move"] = 2;
            opcodes["mov"] = 2;
            opcodes["m"] = 2;
            opcodes["jmp"] = 2;
            opcodes["j"] = 2;

            opcodes["add"] = 3;
            opcodes["sub"] = 4;
            opcodes["sext"] = 5;
            opcodes["mul"] = 6;
            opcodes["iret"] = 7;

            opcodes["not"] = 8;
            opcodes["and"] = 9;
            opcodes["or"] = 10;
            opcodes["xor"] = 11;
            opcodes["xnor"] = 12;
            opcodes["lsh"] = 13;
            opcodes["shl"] = 13;
            opcodes["rsh"] = 14;
            opcodes["shr"] = 14;

            opcodes["dbg"] = 15;

            sections[".text"] = new Section { name = ".text", SectionType = Section.SectType.ProgBits, Alloc = true, Exec = true };
            sections[".data"] = new Section { name = ".data", SectionType = Section.SectType.ProgBits, Alloc = true, Write = true };
            sections[".rodata"] = new Section { name = ".rodata", SectionType = Section.SectType.ProgBits, Alloc = true };
            sections[".bss"] = new Section { name = ".bss", SectionType = Section.SectType.NoBits, Alloc = true, Write = true };
        }

        static void Main(string[] args)
        {
            if (ParseArgs(args) == false)
            {
                DispUsage();
                return;
            }
            if(output_file == null)
            {
                if (input_file != null)
                {
                    FileInfo fi = new FileInfo(input_file);
                    output_file = fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length) + ".o";
                }
                else
                    output_file = "a.out";
            }
            Stream istream = null;
            if (input_file == null)
            {
                // TODO - read from stdin
                DispUsage();
                return;
            }
            else
                istream = new FileStream(input_file, FileMode.Open);

            Parser p = new Parser(new Scanner(istream));
            bool res = p.Parse();
            if (res == false)
                throw new Exception("Parse error");

            // first loop to convert all operands to lowercase, and
            //  expand all complex instructions
            List<Statement> pass1 = new List<Statement>();
            foreach (Statement s in ((StatementList)p.output).list)
                ExpandComplex(s, pass1);

            Dictionary<string, LabelOffset> label_offsets;

            bool changes = false;
            do
            {
                // Reset all section offsets to 0
                foreach (var s in sections.Values)
                {
                    s.cur_offset = 0;
                    s.cur_label = "";
                }

                changes = false;
                // next, add offset information to statements and extract label offsets
                cur_section = sections[".text"];
                label_offsets = new Dictionary<string, LabelOffset>();
                los = label_offsets;
                foreach (Statement s in pass1)
                {
                    if (s == null)
                        continue;
                    if (s is SectionHeader)
                    {
                        var sh = s as SectionHeader;
                        cur_section = sections[sh.name];
                    }

                    s.offset = cur_section.cur_offset;
                    s.section = cur_section;
                    cur_section.cur_offset = s.OffsetAfter(cur_section.cur_offset);

                    if (s is LineLabel)
                    {
                        LineLabel ll = s as LineLabel;
                        ll.abs_name = ll.name;
                        label_offsets[ll.abs_name] = new LabelOffset { Section = cur_section, Offset = ll.offset };

                        if (global_objs.ContainsKey(ll.name))
                            global_objs[ll.name] = cur_section;
                    }


                }

                // do a pass to evaluate all expressions
                cur_section = sections[".text"];
                foreach (Statement s in pass1)
                {
                    if (s == null)
                        continue;

                    if (s is SectionHeader)
                    {
                        var sh = s as SectionHeader;
                        cur_section = sections[sh.name];
                    }

                    if (s is Instruction)
                    {
                        Instruction i = s as Instruction;
                        i.srca = EvaluateOperand(i.srca, label_offsets, cur_section);
                        i.srcb = EvaluateOperand(i.srcb, label_offsets, cur_section);
                        i.dest = EvaluateOperand(i.dest, label_offsets, cur_section);
                    }
                }

                /* Identify those instructions of the form jrel(cc) where we can see
                    for definite that the relocation won't fit */
                List<Statement> pass2 = new List<Statement>();
                foreach (Statement s in pass1)
                {
                    Instruction i = s as Instruction;
                    if (i != null)
                    {
                        if (i.op == "mov" && (i.dest is RegisterOperand) &&
                            (((RegisterOperand)i.dest).val.ToLower() == "pc" ||
                            ((RegisterOperand)i.dest).val.ToLower() == "r0") &&
                            i.cond != null &&
                            i.cond.ctype != Condition.CType.Always)
                        {
                            Relocation r = i.srca as Relocation;
                            if (r != null)
                            {
                                // Do we target a label in this section ?
                                if (r.TargetSection != null && r.TargetSection == i.section)
                                {
                                    // Relocation type will eventually be SRCABREL: S + A - P
                                    // Calculate its value
                                    long r_val = los[r.TargetName].Offset + r.Addend -
                                        i.offset;

                                    // SRCABREL can fit values from -1024 to + 1023
                                    if (r_val < -1024 || r_val > 1023)
                                    {
                                        // Replace with LIT val -> R1; mov(cc) R1 -> PC;

                                        Instruction lit = new Instruction();
                                        lit.op = "lit";
                                        lit.cond = new Condition { ctype = Condition.CType.Always };
                                        lit.dest = new RegisterOperand { val = "R1" };
                                        lit.srca = r;
                                        pass2.Add(lit);

                                        Instruction mov = new Instruction();
                                        mov.op = "mov";
                                        mov.cond = i.cond;
                                        mov.srca = new RegisterOperand { val = "R1" };
                                        mov.dest = new RegisterOperand { val = "PC" };
                                        pass2.Add(mov);

                                        changes = true;

                                        continue;
                                    }
                                }
                            }
                        }
                    }

                    if (s != null)
                        pass2.Add(s);
                }

                pass1 = pass2;
            } while (changes == true);


            // now do the actual encoding
            List<byte> oput;
            cur_section = sections[".text"];
            oput = cur_section.oput;
            string cur_label = "";
            foreach(Statement s in pass1)
            {
                if (s is SectionHeader)
                {
                    var sh = s as SectionHeader;
                    cur_section = sections[sh.name];
                    oput = cur_section.oput;
                }

                if (s is Instruction)
                {
                    Instruction i = s as Instruction;

                    // ensure dest is either null or a valid register
                    uint dest_idx = 0;
                    bool valid = false;
                    if (i.dest != null)
                    {
                        if (i.dest is RegisterOperand)
                        {
                            RegisterOperand r_dest = i.dest as RegisterOperand;
                            string r_dest_str = r_dest.val.ToUpper();
                            if (regs.ContainsKey(r_dest_str))
                            {
                                dest_idx = (uint)regs[r_dest_str];
                                valid = true;
                            }
                        }
                    }
                    else
                        valid = true;
                    if (dest_idx >= 32)
                        valid = false;
                    if (!valid)
                        throw new Exception("Invalid destination register: " + i.ToString());

                    uint srca, srcb, srcab, srcbcond, srcabcond, lit, litr1;
                    bool srca_fits, srcb_fits, srcab_fits, srcbcond_fits,
                        srcabcond_fits, lit_fits, litr1_fits;

                    // Extract data types
                    srca = ExtractData(i.srca, label_offsets, out srca_fits,
                        cur_label, i.offset, 6, false);
                    srcb = ExtractData(i.srcb, label_offsets, out srcb_fits,
                        cur_label, i.offset, 6, false);
                    srcab = ExtractData(i.srca, label_offsets, out srcab_fits,
                        cur_label, i.offset, 12, false);
                    srcbcond = ExtractData(i.srcb, label_offsets, out srcbcond_fits,
                        cur_label, i.offset, 11, false);
                    srcabcond = ExtractData(i.srca, label_offsets, out srcabcond_fits,
                        cur_label, i.offset, 17, false);
                    lit = ExtractData(i.srca, label_offsets, out lit_fits,
                        cur_label, i.offset, 25, true);
                    litr1 = ExtractData(i.srca, label_offsets, out litr1_fits,
                        cur_label, i.offset, 31, true);

                    // Extract relocations
                    Relocation srcar = i.srca as Relocation;
                    Relocation srcbr = i.srcb as Relocation;

                    uint oput_val = 0;
                    // encode literals
                    if (i.op == "lit")
                    {
                        if (i.cond.ctype != Condition.CType.Always)
                            throw new Exception("Cannot use conditions on lit: " + i.ToString());

                        // if dest not specified, assume r1
                        if (i.dest == null)
                            dest_idx = 1;

                        if (dest_idx != 1 && !lit_fits || !litr1_fits)
                        {
                            throw new Exception("Literal value too large: " + i.ToString());
                        }

                        if (dest_idx == 1)
                        {
                            oput_val = 0x80000000U | litr1;
                            if (srcar != null)
                                srcar.Type = binary_library.elf.ElfFile.R_JCA_LITR1;
                        }
                        else
                        {
                            oput_val = 0x40000000U | (dest_idx << 25) | lit;
                            if (srcar != null)
                                srcar.Type = binary_library.elf.ElfFile.R_JCA_LIT;
                        }
                    }
                    else
                    {
                        if (opcodes.ContainsKey(i.op))
                        {
                            // special case jump opcodes to always use pc as dest
                            if (i.op == "jmp" || i.op == "j")
                            {
                                dest_idx = (uint)regs["PC"];
                            }

                            // special case load/store to use 4 byte length unless already specified
                            if (i.op == "load" || i.op == "store")
                            {
                                if (i.srcb == null)
                                {
                                    srcb = 0x24;
                                    srcbcond = 0x404;
                                    srcb_fits = true;
                                    srcbcond_fits = true;
                                }
                            }
                            uint opcode_val = opcodes[i.op];

                            uint cond_val = Condition.cond_vals[i.cond.ctype];
                            uint cond_reg_val = (uint)i.cond.reg_no;

                            // if condition is always, srcab and srcb extend
                            //  into srcabcond and srcbcond respectively
                            bool has_cond_reg = true;
                            if (i.cond.ctype == Condition.CType.Always)
                            {
                                srcab = srcabcond;
                                srcb = srcbcond;
                                srcab_fits = srcabcond_fits;
                                srcb_fits = srcbcond_fits;
                                has_cond_reg = false;
                            }

                            // special case mov - it uses srcab instead of
                            //  srca
                            bool has_srcb = true;
                            if (opcode_val == opcodes["mov"])
                            {
                                srca = srcab;
                                srca_fits = srcab_fits;
                                has_srcb = false;
                            }

                            // ensure all operands fit
                            if (!srca_fits)
                                throw new Exception("First operand in \'" + i.ToString() + "\' is too large");
                            if (has_srcb && !srcb_fits)
                                throw new Exception("Second operand in \'" + i.ToString() + "\' is too large");

                            oput_val = opcode_val << 26 | cond_val << 22 |
                                dest_idx << 17 | srca;
                            if (has_cond_reg)
                                oput_val |= cond_reg_val << 12;
                            if (has_srcb)
                                oput_val |= srcb << 6;

                            if(srcar != null)
                            {
                                if (has_srcb)
                                {
                                    srcar.Type = binary_library.elf.ElfFile.R_JCA_SRCA;
                                    oput_val |= (1U << 5);
                                }
                                else if (has_cond_reg)
                                {
                                    srcar.Type = binary_library.elf.ElfFile.R_JCA_SRCAB;
                                    oput_val |= (1U << 11);
                                }
                                else
                                {
                                    srcar.Type = binary_library.elf.ElfFile.R_JCA_SRCABCOND;
                                    oput_val |= (1U << 16);
                                }

                                if (srcar.IsPCRel)
                                    srcar.Type = srcar.Type - binary_library.elf.ElfFile.R_JCA_SRCA +
                                        binary_library.elf.ElfFile.R_JCA_SRCAREL;
                            }

                            if(srcbr != null)
                            {
                                if (has_cond_reg)
                                {
                                    srcbr.Type = binary_library.elf.ElfFile.R_JCA_SRCB;
                                    oput_val |= (1U << 11);
                                }
                                else
                                {
                                    srcbr.Type = binary_library.elf.ElfFile.R_JCA_SRCBCOND;
                                    oput_val |= (1U << 16);
                                }

                                if (srcbr.IsPCRel)
                                    srcbr.Type = srcbr.Type - binary_library.elf.ElfFile.R_JCA_SRCA +
                                        binary_library.elf.ElfFile.R_JCA_SRCAREL;
                            }
                        }
                        else
                            throw new Exception("Unknown opcode " + i.ToString());
                    }
                    var bs = BitConverter.GetBytes(oput_val);
                    foreach (byte b in bs)
                        oput.Add(b);

                    if(srcar != null)
                    {
                        srcar.SourceOffset = i.offset;
                        relocs.Add(srcar);
                    }
                    if(srcbr != null)
                    {
                        srcbr.SourceOffset = i.offset;
                        relocs.Add(srcbr);
                    }
                }
                else if(s is DataDirective)
                {
                    DataDirective dd = s as DataDirective;

                    // Compress string and label members to integers
                    List<int> data = new List<int>();

                    foreach(var ddi in dd.data)
                    {
                        var e = ddi.Evaluate(new MakeState(label_offsets, cur_section));

                        switch(e.Type)
                        {
                            case Expression.EvalResult.ResultType.String:
                                foreach (char c in e.strval)
                                    data.Add((int)c);
                                break;
                            case Expression.EvalResult.ResultType.Int:
                                data.Add(e.intval);
                                break;
                            default:
                                throw new Exception("Unsupported type in data directive at " + s.ToString());
                        }
                    }

                    // Then output depending on ddtype
                    int b_count = 1;
                    switch (dd.directive)
                    {
                        case DataDirective.DDType.Byte:
                            b_count = 1;
                            break;
                        case DataDirective.DDType.Word:
                            b_count = 2;
                            break;
                        case DataDirective.DDType.DWord:
                            b_count = 4;
                            break;

                    }
                    foreach (int data_i in data)
                    {
                        byte[] data_b = BitConverter.GetBytes(data_i);
                        for (int data_bi = 0; data_bi < b_count; data_bi++)
                        {
                            if (data_bi < data_b.Length)
                                oput.Add(data_b[data_bi]);
                            else
                                oput.Add(0);
                        }
                    }
                }
                else if(s is LineLabel)
                {
                    LineLabel ll = s as LineLabel;
                    cur_label = ll.name;
                }
            }

            // Generate ELF output
            var fs_obj = new System.IO.FileStream(output_file, 
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write);
            var bw_obj = new System.IO.BinaryWriter(fs_obj);
            Elf.GenerateELF(bw_obj);
            bw_obj.Close();

            // Generate binary output
            /*
            string oput_bin = "test.bin";
            string oput_hex = "C:\\Users\\jncro\\Documents\\fpga\\cpu\\fware2.hex";

            System.IO.FileStream fs_bin = new System.IO.FileStream(oput_bin,
                System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.BinaryWriter bw_bin = new System.IO.BinaryWriter(fs_bin);
            foreach (byte b in oput)
                bw_bin.Write(b);
            bw_bin.Close();*/

            // Generate HEX output
            /*
            System.IO.FileStream fs_hex = new System.IO.FileStream(oput_hex,
                System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw_hex = new System.IO.StreamWriter(fs_hex, Encoding.ASCII);
            uint addr = 0;
            foreach(byte b in oput)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(":01");
                sb.Append(addr.ToString("X4"));
                sb.Append("00");
                sb.Append(b.ToString("X2"));

                uint csum = 01 + addr + (addr >> 8) + b;
                csum &= 0xffU;
                csum = 0x100U - csum;
                csum &= 0xffU;
                sb.Append(csum.ToString("X2"));

                addr++;
                sw_hex.WriteLine(sb.ToString());
            }
            sw_hex.WriteLine(":00000001FF");
            sw_hex.Close(); */
        }

        private static void DispUsage()
        {
            string cmd_line = Environment.GetCommandLineArgs()[0];
            FileInfo fi = new FileInfo(cmd_line);
            Console.WriteLine("Usage: " + fi.Name + " [-o output_file] <input_file>");
            Console.WriteLine();
        }

        private static bool ParseArgs(string[] args)
        {
            int i = 0;
            while(i < args.Length)
            {
                if (args[i] == "-o")
                {
                    i++;
                    if ((i >= args.Length) || (args[i].StartsWith("-")))
                        return false;
                    output_file = args[i];
                }
                else if (args[i] == "-" && i == args.Length - 1)
                    input_file = null;
                else if (args[i].StartsWith("-"))
                    return false;
                else if (i == args.Length - 1)
                    input_file = args[i];
                else
                    return false;
                i++;
            }
            return true;
        }

        private static Expression EvaluateOperand(Expression src, Dictionary<string, LabelOffset> label_offsets,
            Section cur_section)
        {
            if (src == null)
                return null;
            MakeState s = new MakeState(label_offsets, cur_section);

            // ExpandComplex already uses operands
            if (src is Operand)
                return src;

            var v = src.Evaluate(s);
            switch(v.Type)
            {
                case Expression.EvalResult.ResultType.Register:
                    return new RegisterOperand { val = v.strval };
                case Expression.EvalResult.ResultType.Int:
                    return new IntegerOperand { val = v.intval };
                case Expression.EvalResult.ResultType.Relocation:
                    return v.relocval;
                default:
                    throw new NotImplementedException();
            }
        }

        private static uint build_imm(int imm_val, int field_len,
            out bool fits, bool is_lit)
        {
            if (is_lit)
            {
                // Literals do not have immediate or sign bits, and can thus
                //  encode two extra bits
                if (imm_val < 0)
                {
                    fits = false;
                    return 0;
                }
                field_len += 2;
            }

            uint imm_bit = 1U << (field_len - 1);
            uint sext = 0xffffffffU << (field_len - 2);
            uint max_unsigned = ~sext;
            int max = (int)max_unsigned;
            int min = -max - 1;

            if(imm_val > max || imm_val < min)
            {
                fits = false;
                return 0;
            }

            fits = true;
            uint ret_mask = 0xffffffffU;
            if(field_len < 32)
                ret_mask = ~(0xffffffffU << (field_len - 1));

            uint ret = BitConverter.ToUInt32(BitConverter.GetBytes(imm_val), 0) &
                ret_mask;

            if(!is_lit)
                ret |= imm_bit;
            return ret;
        }

        private static uint ExtractData(Expression src, Dictionary<string, LabelOffset> label_offsets,
            out bool fits, string cur_label,
            int cur_offset, int field_len, bool is_lit)
        {
            if (src is IntegerOperand)
            {
                int i = ((IntegerOperand)src).val;

                return build_imm(i, field_len, out fits, is_lit);
            }
            else if (src is RegisterOperand)
            {
                RegisterOperand r = src as RegisterOperand;

                if (regs.ContainsKey(r.val.ToUpper()))
                {
                    fits = true;
                    return (uint)regs[r.val.ToUpper()];
                }
                else
                {
                    string label = r.val;
                    if (label_offsets.ContainsKey(label))
                    {
                        if (label_offsets[label].Section != cur_section)
                            throw new NotImplementedException();
                        int v = label_offsets[label].Offset;
                        return build_imm(v, field_len, out fits, is_lit);
                    }
                    else
                        throw new Exception("Undefined label: " + r.val);
                }
            }

            fits = true;
            return 0;
        }

        private static void ExpandComplex(Statement s, List<Statement> next_pass)
        {
            if(s is Instruction)
            {
                Instruction src = s as Instruction;

                string op_lower = src.op.ToLower();

                // is it complex?
                if (op_lower == "jl")
                {
                    /* Jump with link
                        add PC, 4->LR
                        j x
                    */
                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "add";
                    new_1.srca = new RegisterOperand { val = "PC" };
                    new_1.srcb = new IntegerOperand { val = 4 };
                    new_1.dest = new RegisterOperand { val = "LR" };
                    next_pass.Add(new_1);

                    Instruction new_2 = new Instruction();
                    new_2.cond = src.cond;
                    new_2.op = "j";
                    new_2.srca = src.srca;
                    next_pass.Add(new_2);
                }
                else if (op_lower == "jrel")
                {
                    /* Relative jump
                        only valid if srca is a label/integer
                        -> mov x -> pc (which is interpreted as an add to pc)
                    */
                    Expression new_srca = null;
                    if (src.srca is IntegerOperand)
                        new_srca = src.srca;
                    else if (src.srca is LabelExpression)
                    {
                        new_srca = src.srca;
                        ((LabelExpression)new_srca).addend = -4;
                        ((LabelExpression)new_srca).is_pcrel = true;
                    }
                    else if (src.srca is RegisterOperand)
                    {
                        var r_srca = src.srca as RegisterOperand;
                        if (regs.ContainsKey(r_srca.val.ToUpper()))
                        {
                            throw new Exception("jrel must have an integer or label operand");
                        }
                        new_srca = src.srca;
                        ((LabelExpression)new_srca).addend = -4;
                        ((LabelExpression)new_srca).is_pcrel = true;
                    }

                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "mov";
                    new_1.srca = new_srca;
                    new_1.dest = new RegisterOperand { val = "PC" };
                    next_pass.Add(new_1);
                }
                else if (op_lower == "jlrel")
                {
                    /* Relative jump with link
                        only valid if srca is a label/integer
                        ->  add pc, 4 -> lr
                            mov x -> pc (which is interpreted as an add to pc)
                    */
                    Expression new_srca = null;
                    if (src.srca is IntegerOperand)
                        new_srca = src.srca;
                    else if (src.srca is LabelExpression)
                    {
                        new_srca = src.srca;
                        ((LabelExpression)new_srca).addend = -4;
                        ((LabelExpression)new_srca).is_pcrel = true;
                    }
                    else if (src.srca is RegisterOperand)
                    {
                        var r_srca = src.srca as RegisterOperand;
                        if (regs.ContainsKey(r_srca.val.ToUpper()))
                        {
                            throw new Exception("jrel must have an integer or label operand");
                        }
                        new_srca = src.srca;
                        ((LabelExpression)new_srca).addend = -4;
                        ((LabelExpression)new_srca).is_pcrel = true;
                    }

                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "add";
                    new_1.srca = new RegisterOperand { val = "PC" };
                    new_1.srcb = new IntegerOperand { val = 4 };
                    new_1.dest = new RegisterOperand { val = "LR" };
                    next_pass.Add(new_1);

                    Instruction new_2 = new Instruction();
                    new_2.cond = src.cond;
                    new_2.op = "mov";
                    new_2.srca = new_srca;
                    new_2.dest = new RegisterOperand { val = "PC" };
                    next_pass.Add(new_2);
                }
                else if (op_lower == "ret")
                {
                    /* Return
                        j LR
                    */
                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "j";
                    new_1.srca = new RegisterOperand { val = "LR" };
                    next_pass.Add(new_1);
                }
                else if (op_lower == "push")
                {
                    /* Push x
                        sub SP, 4 -> SP
                        store x, 4 -> SP
                    */
                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "sub";
                    new_1.srca = new RegisterOperand { val = "SP" };
                    new_1.srcb = new IntegerOperand { val = 4 };
                    new_1.dest = new RegisterOperand { val = "SP" };
                    next_pass.Add(new_1);

                    Instruction new_2 = new Instruction();
                    new_2.cond = src.cond;
                    new_2.op = "store";
                    new_2.srca = src.srca;
                    new_2.srcb = new IntegerOperand { val = 4 };
                    new_2.dest = new RegisterOperand { val = "SP" };
                    next_pass.Add(new_2);
                }
                else if (op_lower == "pop")
                {
                    /* Pop x
                        load SP, 4 -> x
                        add SP, 4 -> SP
                    */
                    Instruction new_1 = new Instruction();
                    new_1.cond = src.cond;
                    new_1.op = "load";
                    new_1.srca = new RegisterOperand { val = "SP" };
                    new_1.srcb = new IntegerOperand { val = 4 };
                    new_1.dest = src.srca;
                    next_pass.Add(new_1);

                    Instruction new_2 = new Instruction();
                    new_2.cond = src.cond;
                    new_2.op = "add";
                    new_2.srca = new RegisterOperand { val = "SP" };
                    new_2.srcb = new IntegerOperand { val = 4 };
                    new_2.dest = new RegisterOperand { val = "SP" };
                    next_pass.Add(new_2);
                }
                else
                {
                    Instruction dest = new Instruction();
                    dest.op = op_lower;
                    dest.srca = src.srca;
                    dest.srcb = src.srcb;
                    dest.dest = src.dest;
                    dest.cond = src.cond;
                    next_pass.Add(dest);
                }
            }
            else
                next_pass.Add(s);
        }
    }

    class Statement
    {
        public int offset;
        public Section section;
        public virtual int OffsetAfter(int offset_before) { return offset_before + 4; }
    }

    abstract class DataItem
    {
        public abstract int Length { get; }
    }

    class DataDirective : Statement
    {
        public enum DDType { Byte, Word, DWord };
        public DDType directive;
        public List<Expression> data;

        public override int OffsetAfter(int offset_before)
        {
            int length = 0;
            foreach(Expression di in data)
            {
                var e = di.Evaluate(new MakeState(new Dictionary<string, LabelOffset>(), null));
                int cur_length = 1;
                if (e.Type == Expression.EvalResult.ResultType.String)
                    cur_length = e.strval.Length;

                switch(directive)
                {
                    case DDType.Byte:
                        length += cur_length;
                        break;
                    case DDType.Word:
                        length += cur_length * 2;
                        break;
                    case DDType.DWord:
                        length += cur_length * 4;
                        break;
                }
            }
            return length + offset_before;
        }
    }

    class StatementList : Statement
    {
        public List<Statement> list;
    }

    class Instruction : Statement
    {
        public string op;
        public Expression srca, srcb, dest;
        public Condition cond;

        public override string ToString()
        {
            if (cond != null && cond.ctype == Condition.CType.Never)
                return "nop";

            StringBuilder sb = new StringBuilder();
            if (op == null)
                sb.Append("{unknown}");
            else
                sb.Append(op);

            if(cond != null && cond.ctype != Condition.CType.Always)
            {
                sb.Append("(");
                switch(cond.ctype)
                {
                    case Condition.CType.Equals:
                        sb.Append("z");
                        break;
                    case Condition.CType.Negative:
                        sb.Append("neg");
                        break;
                    case Condition.CType.NegEqual:
                        sb.Append("negeq");
                        break;
                    case Condition.CType.NotEquals:
                        sb.Append("nz");
                        break;
                    case Condition.CType.NSOverflow:
                        sb.Append("nso");
                        break;
                    case Condition.CType.NUSOverflow:
                        sb.Append("nuo");
                        break;
                    case Condition.CType.PosEqual:
                        sb.Append("poseq");
                        break;
                    case Condition.CType.Positive:
                        sb.Append("p");
                        break;
                    case Condition.CType.SOverflow:
                        sb.Append("so");
                        break;
                    case Condition.CType.USOverflow:
                        sb.Append("uo");
                        break;
                }
                sb.Append(" R");
                sb.Append(cond.reg_no.ToString());
                sb.Append(")");
            }

            sb.Append(" ");
            sb.Append(srca.ToString());
            if(srcb != null)
            {
                sb.Append(", ");
                sb.Append(srcb.ToString());
            }
            if(dest != null)
            {
                sb.Append(" -> ");
                sb.Append(dest.ToString());
            }
            return sb.ToString();
        }
    }

    class Operand : Expression { }

    class IntegerOperand : Operand
    {
        public int val;
        public override string ToString()
        {
            return val.ToString();
        }
    }

    class RegisterOperand : Operand
    {
        public string val;
        public override string ToString()
        {
            return val;
        }
    }

    class IntegerDataItem : DataItem
    {
        public int val;
        public override int Length { get { return 1; } }
    }

    class StringDataItem : DataItem
    {
        public string val;
        public override int Length { get { return val.Length; } }
    }

    class LabelDataItem : DataItem
    {
        public string val;
        public override int Length { get { return 1; } }
    }

    class LineLabel : Statement
    {
        public string name;
        public string abs_name;

        public override int OffsetAfter(int offset_before)
        {
            return offset_before;
        }
    }

    class Condition
    {
        static Dictionary<string, CType> conds =
            new Dictionary<string, CType>();

        internal static Dictionary<CType, uint> cond_vals =
            new Dictionary<CType, uint>();

        static Condition()
        {
            conds["a"] = CType.Always;
            conds["always"] = CType.Always;
            conds["never"] = CType.Never;
            conds["e"] = CType.Equals;
            conds["eq"] = CType.Equals;
            conds["z"] = CType.Equals;
            conds["ne"] = CType.NotEquals;
            conds["neq"] = CType.NotEquals;
            conds["nz"] = CType.NotEquals;
            conds["p"] = CType.Positive;
            conds["pos"] = CType.Positive;
            conds["n"] = CType.Negative;
            conds["neg"] = CType.Negative;
            conds["poseq"] = CType.PosEqual;
            conds["negeq"] = CType.NegEqual;
            conds["so"] = CType.SOverflow;
            conds["nso"] = CType.NSOverflow;
            conds["uo"] = CType.USOverflow;
            conds["c"] = CType.USOverflow;
            conds["nuo"] = CType.NUSOverflow;
            conds["nc"] = CType.NUSOverflow;

            cond_vals[CType.Never] = 0;
            cond_vals[CType.Equals] = 1;
            cond_vals[CType.NotEquals] = 2;
            cond_vals[CType.Positive] = 3;
            cond_vals[CType.Negative] = 4;
            cond_vals[CType.PosEqual] = 5;
            cond_vals[CType.NegEqual] = 6;
            cond_vals[CType.SOverflow] = 7;
            cond_vals[CType.NSOverflow] = 8;
            cond_vals[CType.USOverflow] = 9;
            cond_vals[CType.NUSOverflow] = 10;
            cond_vals[CType.Always] = 15;                
        }

        public enum CType
        {
            Always, Never, Equals, NotEquals, Positive,
            Negative, PosEqual, NegEqual, SOverflow, NSOverflow,
            USOverflow, NUSOverflow
        };

        public CType ctype;
        public int reg_no = 0;
        public Condition() { }

        public Condition(string cond, string reg_name)
        {
            if (!conds.ContainsKey(cond))
                throw new Exception("Unknown condition: " + cond);
            ctype = conds[cond];

            reg_name = reg_name.ToUpper();

            if (!Program.regs.ContainsKey(reg_name))
                throw new Exception("Unknown register: ." + cond + " (" + reg_name + ")");
            reg_no = Program.regs[reg_name];
        }
    }

    class Util
    {
        public static int ParseBinary(string s)
        {
            int ret = 0;
            for(int i = 0; i < s.Length; i++)
            {
                ret <<= 1;
                if (s[i] == 1)
                    ret++;
                else if (s[i] != 0)
                    throw new FormatException(s + "b is not valid binary");
            }
            return ret;
        }
    }

    partial class Parser
    {
        internal Parser(Scanner s) : base(s) { }
    }

    partial class Scanner
    {
        public override void yyerror(string format, params object[] args)
        {
            throw new ParseException(String.Format(format, args) + " at line " + yyline + ", col " + yycol, yyline, yycol);
        }

        internal int sline { get { return yyline; } }
        internal int scol { get { return yycol; } }
    }

    public class ParseException : Exception
    {
        int l, c;
        public ParseException(string msg, int line, int col) : base(msg) { l = line; c = col; }
    }

}
