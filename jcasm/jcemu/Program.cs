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

namespace jcemu
{
    class Program
    {
        static internal bool sstep = true;

        static void Main(string[] args)
        {
            AddrSpace a = new AddrSpace();

            /* Load up ROM */
            //string fname = "../../../jcasm/bin/Debug/test.bin";
            //string fname = "D:/tysos/branches/tysila3/tl/tl/bin/Debug/test.bin";
            string fname = "D:/cygwin64/home/jncro/fpga/loader.bin";
            System.IO.FileStream rom = new System.IO.FileStream(fname,
                System.IO.FileMode.Open);
            a.AddRegion(new RAM(4096, new System.IO.BinaryReader(rom)), 0);

            /* Add devices */
            a.AddRegion(new RAM(512 * 1024), 0x400000);
            a.AddRegion(new RAM(512 * 1024), 0x800000);
            a.AddRegion(new UART(), 0xc00000);
            a.AddRegion(new SPI(), 0x1400000);

            var irq = new IRQController();
            a.AddRegion(irq, 0x1c00000);

            var timer = new Timer();
            a.AddRegion(timer, 0x1800000);
            irq.irqs[0] = timer;

            a.AddRegion(new Vga(), 0x1000000);

            /* Create cpu */
            Cpu c = new Cpu(a);

            Dictionary<uint, bool> bpoints = new Dictionary<uint, bool>();

            //Console.TreatControlCAsInput = true;
            Console.CancelKeyPress += Console_CancelKeyPress;

            while(true)
            {
                timer.Tick();
                irq.Tick();
                if (irq.IsIRQSignalled())
                {
                    c.IRQ();
                    irq.AckIRQ();
                }

                if (bpoints.ContainsKey(c.PC))
                    sstep = true;

                if (sstep)
                {
                    System.Console.Write(c.ToString());
                    string s = System.Console.ReadLine();
                    if (s != null)
                    {
                        if (s == "c" | s == "g" | s == "r")
                            sstep = false;
                        if (s.StartsWith("b "))
                        {
                            s = s.Substring(2);
                            uint bpval = 0;
                            if (s.StartsWith("0x"))
                            {
                                s = s.Substring(2);
                                bpval = uint.Parse(s, System.Globalization.NumberStyles.HexNumber);
                            }
                            else
                                bpval = uint.Parse(s);

                            bpoints[bpval] = true;
                        }
                    }
                }
                c.Tick();
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (sstep)
                e.Cancel = false;
            else
            {
                sstep = true;
                e.Cancel = true;
            }
        }
    }

    class RAM : MemoryRegion
    {
        public RAM(uint byte_length) : this(byte_length, true, null) { }
        public RAM(uint byte_length, System.IO.BinaryReader r) : this(byte_length, true, r) { }

        public RAM(uint byte_length, bool _writeable, System.IO.BinaryReader r)
        {
            length = byte_length;
            writeable = _writeable;
            mem = new byte[byte_length];

            if (r != null)
            {
                if (r.BaseStream.Length > byte_length)
                {
                    var fname = "";
                    var bs = r.BaseStream as FileStream;
                    if (bs != null)
                        fname = bs.Name + " ";
                    throw new Exception("Memory initialization file " +
                        fname + "is too large for RAM block (" +
                        r.BaseStream.Length.ToString() + " vs " +
                        byte_length + ")");
                }

                int read_len = (int)byte_length;
                if (read_len > r.BaseStream.Length)
                    read_len = (int)r.BaseStream.Length;
                for (int i = 0; i < read_len; i++)
                    mem[i] = r.ReadByte();
            }
        }

        protected bool writeable;
        protected byte[] mem;

        protected virtual void OnWriteByte(uint addr, uint val) { }
        protected virtual void OnReadByte(uint addr) { }

        public override uint ReadByte(uint addr)
        {
            OnReadByte(addr);
            return mem[addr];
        }

        public override void WriteByte(uint addr, uint val)
        {
            if (writeable)
            {
                mem[addr] = (byte)(val & 0xff);
                OnWriteByte(addr, val);
            }
        }
    }

    class UART : MemoryRegion
    {
        byte last_byte = 0;
        public override uint Length { get { return 8; } }
        public override uint ReadByte(uint addr)
        {
            switch(addr)
            {
                case 0:
                    return 0;   // always ready
                case 4:
                    return last_byte;
                default:
                    return 0;
            }
        }
        public override void WriteByte(uint addr, uint val)
        {
            switch(addr)
            {
                case 4:
                    last_byte = (byte)(val & 0xff);
                    System.Diagnostics.Debugger.Log(0, "", "UART: " + (char)last_byte + Environment.NewLine);
                    break;
            }
        }
    }

    class SPI : MemoryRegion
    {
        uint cmd = 0;
        uint clkdiv = 125;
        uint data = 0;
        int selected_device = -1;

        SlaveDevice[] slaves = new SlaveDevice[8];

        public SPI()
        {
            slaves[0] = new SD();

            var fs = new FileStream("D:/cygwin64/home/jncro/fpga/prog.elf",
                FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);
            slaves[1] = new SPI_RAM(0x20000, false, br);
            slaves[2] = new SPI_RAM(0x20000, false, br);
            slaves[3] = new SPI_RAM(0x20000, false, br);
            slaves[4] = new SPI_RAM(0x20000, false, br);
        }

        public abstract class SlaveDevice
        {
            public abstract uint HandleByte(uint v);
            public virtual void SelectDevice() { }
            public virtual void DeselectDevice() { }
        }

        public override uint Length { get { return 12; } }
        public override uint ReadByte(uint addr)
        {
            switch (addr)
            {
                case 0:
                    return cmd;
                case 4:
                case 5:
                case 6:
                case 7:
                    return (clkdiv >> (((int)addr - 4) * 8)) & 0xffU;
                case 8:
                    return data;
                default:
                    return 0;
            }
        }
        public override void WriteByte(uint addr, uint val)
        {
            switch (addr)
            {
                case 0:
                    cmd = val & 0xff;

                    // handle device select
                    if((val & 0x80) != 0)
                    {
                        var dev = (val & 0x70) >> 4;
                        if((int)dev != selected_device)
                        {
                            selected_device = (int)dev;
                            if (slaves[selected_device] != null)
                                slaves[selected_device].SelectDevice();
                        }
                    }
                    else
                    {
                        if(selected_device != -1)
                        {
                            if (slaves[selected_device] != null)
                                slaves[selected_device].DeselectDevice();
                            selected_device = -1;
                        }
                    }

                    // handle reset
                    if((val & 0x1) != 0)
                    {
                        cmd = 0;
                        clkdiv = 125;
                        data = 0;
                        selected_device = -1;
                    }

                    // handle data send
                    if((val & 0x82) == 0x82)
                    {
                        // send byte to slave device
                        int slave_id = (int)((val >> 4) & 0x7);
                        if (slaves[slave_id] != null)
                            data = slaves[slave_id].HandleByte(data);
                        else
                            data = 0xffU;
                        cmd &= ~0x02U;
                    }
                    else if((val & 0x02) == 0x02)
                    {
                        // send byte to nothing
                        System.Diagnostics.Debugger.Log(0, "", "SPI: write to no device: " + data.ToString("X2") + Environment.NewLine);
                        data = 0xffU;
                        cmd &= ~0x02U;
                    }
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                    clkdiv &= ~(0xffU << (((int)addr - 4) * 8));
                    clkdiv |= (val & 0xffU) << (((int)addr - 4) * 8);
                    break;
                case 8:
                    data = val & 0xffU;
                    break;
            }
        }
    }

    abstract class MemoryRegion
    {
        public abstract uint ReadByte(uint addr);
        public abstract void WriteByte(uint addr, uint val);

        protected uint length;

        public virtual uint Length { get { return length; } }
    }

    interface ITick
    {
        void Tick();
    }

    interface IIRQ
    {
        bool IsIRQSignalled();
    }

    class AddrSpace
    {
        class MReg { public uint start; public MemoryRegion reg; }
        List<MReg> mregs = new List<MReg>();

        public void AddRegion(MemoryRegion reg, uint start)
        {
            mregs.Add(new MReg { reg = reg, start = start });
        }

        public uint ReadByte(uint addr)
        {
            foreach(var mreg in mregs)
            {
                if ((addr >= mreg.start) && (addr < mreg.start + mreg.reg.Length))
                    return mreg.reg.ReadByte(addr - mreg.start);
            }
            return 0;
        }

        public void WriteByte(uint addr, uint val)
        {
            foreach (var mreg in mregs)
            {
                if ((addr >= mreg.start) && (addr < mreg.start + mreg.reg.Length))
                {
                    mreg.reg.WriteByte(addr - mreg.start, val);
                    return;
                }
            }
        }
    }

    class Cpu
    {
        const int REGS = 16;
        const int ARCH_REGS = 32;

        uint[] r = new uint[ARCH_REGS];
        bool[] cf = new bool[ARCH_REGS];
        AddrSpace a;

        public Cpu(AddrSpace addrspace) { a = addrspace; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < REGS; i++)
            {
                sb.Append("R");
                sb.Append(i.ToString());
                sb.Append(": ");
                sb.Append(r[i].ToString("X8"));
                sb.Append(Environment.NewLine);
            }
            uint inst = Read(r[0], 4);
            sb.Append(inst.ToString("X8"));
            sb.Append(": ");
            sb.Append(disasm(inst));
            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        private string disasm(uint instr)
        {
            if ((instr & 0x80000000U) != 0)
            {
                // literal instruction
                return "lit " + (instr & ~0x80000000U).ToString("X8") + " -> R1";
            }
            else
            {
                if ((instr & 0x40000000U) != 0)
                {
                    uint dest = (instr >> 25) & 0x1fU;
                    string dests = "R" + dest.ToString();

                    return "lit " + (instr & 0x1ffffffU).ToString("X8") + " -> " + dests;
                }
                else
                {
                    uint srca = instr & 0x3f;
                    uint srcb = (instr >> 6) & 0x3f;
                    uint cond_reg = (instr >> 12) & 0x1f;
                    uint dest = (instr >> 17) & 0x1f;
                    uint cond = (instr >> 22) & 0xf;
                    uint opcode = (instr >> 26) & 0xf;

                    uint srcab = instr & 0xfff;
                    uint srcbcond = (instr >> 6) & 0x7ff;
                    uint srcabcond = instr & 0x1ffff;

                    bool ab_is_imm, abcond_is_imm;
                    string srcas = disasmop(srca, 6);
                    string srcbs = disasmop(srcb, 6);
                    string srcabs = disasmop(srcab, 12, out ab_is_imm);
                    string srcbconds = disasmop(srcbcond, 11);
                    string srcabconds = disasmop(srcabcond, 17, out abcond_is_imm);
                    string cond_regs = "R" + cond_reg.ToString();
                    string dests = "R" + dest.ToString();

                    string conds = "";

                    string op = "";

                    bool has_srca = true;
                    bool has_srcb = true;
                    bool has_dest = true;
                    bool has_cond = true;

                    switch(cond)
                    {
                        case 1:
                            conds = ".z";
                            break;
                        case 2:
                            conds = ".nz";
                            break;
                        case 3:
                            conds = ".pos";
                            break;
                        case 4:
                            conds = ".neg";
                            break;
                        case 5:
                            conds = ".poseq";
                            break;
                        case 6:
                            conds = ".negeq";
                            break;
                        case 7:
                            conds = ".s";
                            break;
                        case 8:
                            conds = ".ns";
                            break;
                        case 9:
                            conds = ".c";
                            break;
                        case 10:
                            conds = ".nc";
                            break;
                        case 15:
                            has_cond = false;
                            srcbs = srcbconds;
                            srcabs = srcabconds;
                            ab_is_imm = abcond_is_imm;
                            break;
                        default:
                            return "nop";
                    }

                    switch (opcode)
                    {
                        case 0:
                            op = "load";
                            break;
                        case 1:
                            op = "store";
                            break;
                        case 2:
                            op = "move";
                            srcas = srcabs;
                            has_srcb = false;
                            if(dest == 0)
                            {
                                if (ab_is_imm)
                                    op = "jrel";
                                else
                                    op = "j";
                            }
                            break;
                        case 3:
                            op = "add";
                            break;
                        case 4:
                            op = "sub";
                            break;
                        case 5:
                            op = "sext";
                            break;
                        case 6:
                            op = "mul";
                            break;
                        case 7:
                            op = "iret";
                            has_cond = false;
                            has_srca = false;
                            has_srcb = false;
                            has_dest = false;
                            break;
                        case 8:
                            op = "not";
                            has_srcb = false;
                            break;
                        case 9:
                            op = "and";
                            break;
                        case 10:
                            op = "or";
                            break;
                        case 11:
                            op = "xor";
                            break;
                        case 12:
                            op = "xnor";
                            break;
                        case 13:
                            op = "lsh";
                            break;
                        case 14:
                            op = "rsh";
                            break;
                        case 15:
                            op = "dbg";
                            has_srcb = false;
                            has_dest = false;
                            break;

                        default:
                            op = "unknown";

                            break;
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.Append(op);
                    if(has_cond)
                    {
                        sb.Append("(");
                        sb.Append(conds);
                        sb.Append(" ");
                        sb.Append(cond_regs);
                        sb.Append(")");
                    }
                    sb.Append(" ");
                    if (has_srca)
                    {
                        sb.Append(srcas);
                        if (has_srcb)
                        {
                            sb.Append(", ");
                            sb.Append(srcbs);
                        }
                    }
                    if (has_dest)
                    {
                        sb.Append(" -> ");
                        sb.Append(dests);
                    }
                    return sb.ToString();
                }
            }
        }

        private string disasmop(uint srca, int field_len)
        {
            bool b;
            return disasmop(srca, field_len, out b);
        }

        private string disasmop(uint srca, int field_len, out bool is_imm)
        {
            // most significant bit is 1 for immediate, 0 for register
            uint bit_test = 1U << (field_len - 1);
            if ((srca & bit_test) != 0)
            {
                // immediate - determine sign bit
                uint sign_bit = 1U << (field_len - 2);
                uint sext = 0xffffffffU << (field_len - 2);
                is_imm = true;
                if ((srca & sign_bit) != 0)
                {
                    uint neg_val = sext | srca;
                    int act_neg_val = BitConverter.ToInt32(BitConverter.GetBytes(neg_val), 0);
                    return act_neg_val.ToString();
                }
                else
                    return (srca & ~sext).ToString();
            }
            else
            {
                is_imm = false;
                return "R" + (srca & 0x1f).ToString();
            }
        }

        public uint PC { get { return r[0]; } }

        public void IRQ()
        { IRQ(0); }
        public void IRQ(uint ecode)
        {
            // Push return address followed by error code
            r[7] = r[7] - 4;
            Write(r[7], r[0], 4);
            r[7] = r[7] - 4;
            Write(r[7], ecode, 4);
            r[0] = Read(0x4, 4);
        }

        public void Tick()
        {
            // read instruction
            uint instr = Read(r[0], 4);

            if (instr == 0x0fc9fd84)
                Program.sstep = true;

            r[0] = r[0] + 4;

            if ((instr & 0x80000000U) != 0)
            {
                // literal instruction
                r[1] = instr & ~0x80000000U;
            }
            else
            {
                if ((instr & 0x40000000U) != 0)
                {
                    // literal instruction
                    uint dest = (instr >> 25) & 0x1fU;
                    r[dest] = instr & 0x1ffffffU;
                }
                else
                {
                    uint srca = instr & 0x3f;
                    uint srcb = (instr >> 6) & 0x3f;
                    uint cond_reg = (instr >> 12) & 0x1f;
                    uint dest = (instr >> 17) & 0x1f;
                    uint cond = (instr >> 22) & 0xf;
                    uint opcode = (instr >> 26) & 0xf;

                    uint srcab = instr & 0xfff;
                    uint srcbcond = (instr >> 6) & 0x7ff;
                    uint srcabcond = instr & 0x1ffff;

                    bool ab_is_imm, abcond_is_imm;
                    srca = decode(srca, 6);
                    srcb = decode(srcb, 6);
                    srcab = decode(srcab, 12, out ab_is_imm);
                    srcbcond = decode(srcbcond, 11);
                    srcabcond = decode(srcabcond, 17, out abcond_is_imm);

                    uint cond_val = r[cond_reg & 0x1f];
                    bool cond_cf = cf[cond_reg & 0x1f];

                    bool run_inst = true;
                    switch(cond)
                    {
                        case 0:
                            run_inst = false;
                            break;
                        case 1:
                            if (cond_val == 0)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 2:
                            if (cond_val != 0)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 3:
                            if (cond_val > 0 && cond_val < 0x80000000U)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 4:
                            if (cond_val >= 0x80000000U)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 5:
                            if (cond_val >= 0 && cond_val < 0x80000000U)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 6:
                            if (cond_val == 0 || cond_val >= 0x80000000U)
                                run_inst = true;
                            else
                                run_inst = false;
                            break;
                        case 7:
                            // signed overflow - TODO
                            run_inst = false;
                            break;
                        case 8:
                            // unsigned overflow - TODO
                            run_inst = false;
                            break;
                        case 9:
                            run_inst = cond_cf;
                            break;
                        case 10:
                            run_inst = !cond_cf;
                            break;
                        case 15:
                            run_inst = true;
                            // extend srcb and srcab into conditional reg
                            srcb = srcbcond;
                            srcab = srcabcond;
                            ab_is_imm = abcond_is_imm;
                            break;
                        default:
                            run_inst = false;
                            break;
                    }

                    if (run_inst)
                    {
                        cf[dest] = false;
                        unchecked
                        {
                            switch (opcode)
                            {
                                case 0:
                                    r[dest] = Read(srca, srcb);
                                    break;
                                case 1:
                                    Write(r[dest], srca, srcb);
                                    break;
                                case 2:
                                    // special case for mov - uses srcab
                                    if(dest == 0 && ab_is_imm)
                                    {
                                        // immediate move to pc -> add
                                        r[dest] = r[dest] + srcab;
                                    }
                                    else
                                        r[dest] = srcab;
                                    break;
                                case 3:
                                    r[dest] = srca + srcb;
                                    if (r[dest] < srca)
                                        cf[dest] = true;
                                    break;
                                case 4:
                                    r[dest] = srca - srcb;
                                    if (r[dest] > srca)
                                        cf[dest] = true;
                                    break;
                                case 5:
                                    switch (srcb)
                                    {
                                        case 1:
                                            if ((srca & 0x80) != 0)
                                            {
                                                r[dest] = srca | 0xffffff00U;
                                                cf[dest] = true;
                                            }
                                            else
                                            {
                                                r[dest] = srca & 0xffU;
                                                cf[dest] = false;
                                            }
                                            break;
                                        case 2:
                                            if ((srca & 0x8000) != 0)
                                            {
                                                r[dest] = srca | 0xffff0000U;
                                                cf[dest] = true;
                                            }
                                            else
                                            {
                                                r[dest] = srca & 0xffffU;
                                                cf[dest] = false;
                                            }
                                            break;
                                        default:
                                            r[dest] = srca;
                                            cf[dest] = false;
                                            break;
                                    }
                                    break;
                                case 6:
                                    {
                                        ulong tmp = srca * srcb;
                                        r[dest] = (uint)(tmp & 0xffffffffU);
                                        if (((tmp >> 32) & 0x1UL) != 0)
                                            cf[dest] = true;
                                    }
                                    break;
                                case 7:
                                    uint temp = Read(r[7], 4);
                                    r[7] = r[7] + 4;
                                    r[0] = temp;
                                    break;
                                case 8:
                                    r[dest] = ~srca;
                                    break;
                                case 9:
                                    r[dest] = srca & srcb;
                                    break;
                                case 10:
                                    r[dest] = srca | srcb;
                                    break;
                                case 11:
                                    r[dest] = srca ^ srcb;
                                    break;
                                case 12:
                                    r[dest] = ~(srca ^ srcb);
                                    break;
                                case 13:
                                    r[dest] = srca << (int)srcb;
                                    break;
                                case 14:
                                    r[dest] = srca >> (int)srcb;
                                    break;
                                case 15:
                                    System.Diagnostics.Debugger.Log(0, "", "DEBUG: " + srca.ToString("X8") + Environment.NewLine);
                                    break;

                                default:
                                    System.Diagnostics.Debugger.Log(0, "", "Unknown opcode: " + opcode.ToString("X2") + Environment.NewLine);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private uint decode(uint src, int field_len)
        {
            bool b;
            return decode(src, field_len, out b);
        }

        private uint decode(uint src, int field_len, out bool is_imm)
        {
            // most significant bit is 1 for immediate, 0 for register
            uint bit_test = 1U << (field_len - 1);
            if((src & bit_test) != 0)
            {
                // immediate - determine sign bit
                uint sign_bit = 1U << (field_len - 2);
                uint sext = 0xffffffffU << (field_len - 2);
                is_imm = true;
                if((src & sign_bit) != 0)
                {
                    // its a negative number, sign extend the least
                    //  significant field_len - 2 bits
                    return sext | src;
                }
                else
                {
                    return src & ~sext;
                }
            }
            else
            {
                // register
                is_imm = false;
                return r[src & 0x1f];
            }
        }

        uint Read(uint addr, uint length)
        {
            if (length > 4)
                length = 4;
            uint ret = 0;
            for(uint i = 0; i < length; i++)
                ret += a.ReadByte(addr + i) << (int)(i * 8);
            return ret;
        }

        void Write(uint addr, uint val, uint length)
        {
            if (length > 4)
                length = 4;
            for(uint i = 0; i < length; i++)
            {
                a.WriteByte(addr + i, val & 0xff);
                val >>= 8;
            }
        }
    }
}
