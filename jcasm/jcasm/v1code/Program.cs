using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jcasm
{
    class Program
    {
        static Dictionary<string, int> regs = new Dictionary<string, int>();
        static Dictionary<string, byte> opcodes = new Dictionary<string, byte>();

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

            opcodes["load"] = 0;
            opcodes["store"] = 1;
            opcodes["move"] = 2;
            opcodes["mov"] = 2;
            opcodes["m"] = 2;
            opcodes["jmp"] = 2;
            opcodes["j"] = 2;
            opcodes["mz"] = 3;
            opcodes["jz"] = 3;
            opcodes["mnz"] = 4;
            opcodes["jnz"] = 4;
            opcodes["mpos"] = 5;
            opcodes["jpos"] = 5;
            opcodes["mneg"] = 6;
            opcodes["jneg"] = 6;

            opcodes["add"] = 8;
            opcodes["sub"] = 9;

            opcodes["not"] = 16;
            opcodes["and"] = 17;
            opcodes["or"] = 18;
            opcodes["xor"] = 19;
            opcodes["xnor"] = 20;
            opcodes["lsh"] = 21;
            opcodes["shl"] = 21;
            opcodes["rsh"] = 22;
            opcodes["shr"] = 22;

            opcodes["eq"] = 32;
            opcodes["gr"] = 33;
            opcodes["ge"] = 34;

            opcodes["dbg"] = 40;
        }

        static void Main(string[] args)
        {
            System.IO.FileStream f = new System.IO.FileStream("test.s", System.IO.FileMode.Open);
            Parser p = new Parser(new Scanner(f));
            bool res = p.Parse();
            if (res == false)
                throw new Exception("Parse error");

            // first loop to convert all operands to lowercase, and
            //  expand all complex instructions
            List<Statement> pass1 = new List<Statement>();
            foreach (Statement s in ((StatementList)p.output).list)
                pass1.Add(ExpandComplex(s));

            // next, add offset information to statements and extract label offsets
            int cur_offset = 0;
            string cur_label = "";
            Dictionary<string, int> label_offsets = new Dictionary<string, int>();
            foreach(Statement s in pass1)
            {
                s.offset = cur_offset;
                cur_offset = s.OffsetAfter(cur_offset);

                if(s is LineLabel)
                {
                    LineLabel ll = s as LineLabel;
                    // append local labels to their base name
                    if (ll.name.StartsWith("."))
                        ll.abs_name = cur_label + ll.name;
                    else
                    {
                        cur_label = ll.name;
                        ll.abs_name = ll.name;
                    }
                    label_offsets[ll.abs_name] = ll.offset;
                }
            }

            // now do the actual encoding
            List<byte> oput = new List<byte>();
            cur_label = "";
            foreach(Statement s in pass1)
            {
                if(s is Instruction)
                {
                    Instruction i = s as Instruction;

                    int srca = 0;
                    int srcb = 0;
                    int dest = 0;
                    byte srca_b, srcb_b, dest_b;
                    bool srca_fits, srcb_fits, dest_fits;

                    // Extract data types
                    srca = ExtractData(i.srca, label_offsets, out srca_b,
                        out srca_fits, cur_label);
                    srcb = ExtractData(i.srcb, label_offsets, out srcb_b, 
                        out srcb_fits, cur_label);
                    dest = ExtractData(i.dest, label_offsets, out dest_b, 
                        out dest_fits, cur_label);

                    if (i.op == "lit")
                    {
                        var bs = BitConverter.GetBytes(srca);
                        for(int idx = 0; idx < 4; idx++)
                        {
                            byte b;
                            if (idx < bs.Length)
                                b = bs[idx];
                            else
                                b = 0;
                            if (idx == 3)
                                b |= 0x80;
                            oput.Add(b);
                        }
                    }
                    else
                    {
                        if (opcodes.ContainsKey(i.op))
                        {
                            // special case jump opcodes to always use pc as dest and subtract 4 from srca
                            if(i.op == "jmp" || i.op == "j" || i.op == "jz" ||
                                i.op == "jnz" || i.op == "jpos" || i.op == "jneg")
                            {
                                dest_b = (byte)regs["PC"];
                                if ((srca_b & 0x80) == 0x80)
                                {
                                    if (srca_b >= 0xc0 && srca_b <= 0xc3)
                                        srca_fits = false;
                                    srca_b = (byte)(srca_b - 0x4U);
                                    srca_b |= 0x80;
                                }
                            }

                            // special case load/store to use 4 byte length unless already specified
                            if(i.op == "load" || i.op == "store")
                            {
                                if (i.srcb == null)
                                    srcb_b = 0x84;
                            }
                            // ensure all byte operands fit
                            if (!srca_fits)
                                throw new Exception("First operand in \'" + i.ToString() + "\' is too large");
                            if (!srcb_fits)
                                throw new Exception("Second operand in " + i.ToString() + " is too large");
                            if ((dest_b & 0x80) == 0x80)
                                throw new Exception("Destination in " + i.ToString() + " is not a register");

                            oput.Add(srca_b);
                            oput.Add(srcb_b);
                            oput.Add(dest_b);
                            oput.Add(opcodes[i.op]);
                        }
                        else
                            throw new Exception("Unknown opcode " + i.ToString());
                    }
                }
                else if(s is DataDirective)
                {
                    DataDirective dd = s as DataDirective;

                    // Compress string and label members to integers
                    List<int> data = new List<int>();

                    foreach(var ddi in dd.data)
                    {
                        if (ddi is StringDataItem)
                        {
                            foreach (char c in ((StringDataItem)ddi).val)
                                data.Add((int)c);
                        }
                        else if (ddi is IntegerDataItem)
                            data.Add(((IntegerDataItem)ddi).val);
                        else if(ddi is LabelDataItem)
                        {
                            string label = ((LabelDataItem)ddi).val;
                            if (label_offsets.ContainsKey(label))
                                data.Add(label_offsets[label]);
                            else
                                throw new Exception("Label " + label + " + not found");
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
                    if (!ll.name.StartsWith("."))
                        cur_label = ll.name;
                }
            }

            string oput_bin = "test.bin";
            string oput_hex = "C:\\Users\\jncro\\Documents\\fpga\\cpu\\fware.hex";

            System.IO.FileStream fs_bin = new System.IO.FileStream(oput_bin,
                System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.BinaryWriter bw_bin = new System.IO.BinaryWriter(fs_bin);
            foreach (byte b in oput)
                bw_bin.Write(b);
            bw_bin.Close();

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
            sw_hex.Close();
        }

        private static int ExtractData(Operand src, Dictionary<string, int> label_offsets, out byte byte_val, out bool fits_byte, string cur_label)
        {
            if (src is IntegerOperand)
            {
                int i = ((IntegerOperand)src).val;
                byte_val = (byte)(0x80U | (i & 0x7f));
                if (i < -64 || i > 63)
                    fits_byte = false;
                else
                    fits_byte = true;
                return i;
            }
            else if (src is RegisterOperand)
            {
                RegisterOperand r = src as RegisterOperand;

                if (regs.ContainsKey(r.val.ToUpper()))
                {
                    byte_val = (byte)regs[r.val.ToUpper()];
                    fits_byte = true;
                    return byte_val;
                }
                else
                {
                    string label = r.val;
                    if (label.StartsWith("."))
                        label = cur_label + r.val;
                    if (label_offsets.ContainsKey(label))
                    {
                        int v = label_offsets[label];
                        if (v < -64 || v > 63)
                            fits_byte = false;
                        else
                            fits_byte = true;
                        byte_val = (byte)(0x80U | (v & 0x7f));
                        return v;
                    }
                    else
                        throw new Exception("Undefined label: " + r.val);
                }
            }

            byte_val = 0;
            fits_byte = true;
            return 0;
        }

        private static Statement ExpandComplex(Statement s)
        {
            if(s is Instruction)
            {
                Instruction src = s as Instruction;

                // TODO: is it complex?
                Instruction dest = new Instruction();
                dest.op = src.op.ToLower();
                dest.srca = src.srca;
                dest.srcb = src.srcb;
                dest.dest = src.dest;
                return dest;
            }
            return s;
        }
    }

    class Statement
    {
        public int offset;
        public virtual int OffsetAfter(int offset_before) { return offset_before + 4; }
    }

    class Operand
    {

    }

    abstract class DataItem
    {
        public abstract int Length { get; }
    }

    class DataDirective : Statement
    {
        public enum DDType { Byte, Word, DWord };
        public DDType directive;
        public List<DataItem> data;

        public override int OffsetAfter(int offset_before)
        {
            int length = 0;
            foreach(DataItem di in data)
            {
                switch(directive)
                {
                    case DDType.Byte:
                        length += di.Length;
                        break;
                    case DDType.Word:
                        length += di.Length * 2;
                        break;
                    case DDType.DWord:
                        length += di.Length * 4;
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
        public Operand srca, srcb, dest;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(op);
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
