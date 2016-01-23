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

namespace jcemu
{
    class IRQController : MemoryRegion, IIRQ, ITick
    {
        bool sirq = false;
        bool cpu_int = false;
        uint irq_mask = 0;
        int cpu_irq = 0;

        public IIRQ[] irqs = new IIRQ[32];
        public void AckIRQ()
        {
            cpu_int = false;
        }
        public int CpuIRQNum { get { return cpu_irq; } }
        public bool IsIRQSignalled() { return cpu_int; }

        public void Tick()
        {
            if(sirq == false)
            {
                for(int i = 0; i < irqs.Length; i++)
                {
                    if(irqs[i] != null && irqs[i].IsIRQSignalled() &&
                        ((irq_mask >> i) != 0))
                    {
                        sirq = true;
                        cpu_int = true;
                        cpu_irq = i;
                        return;
                    }
                }
            }
        }

        public override uint Length { get { return 12; } }

        public override uint ReadByte(uint addr)
        {
            switch(addr)
            {
                case 0:
                    uint ret = 0;
                    if (sirq)
                        ret |= 0x80;
                    ret |= (uint)cpu_irq;
                    return ret;

                case 4:
                case 5:
                case 6:
                case 7:
                    return (irq_mask >> (((int)addr - 4) * 8)) & 0xffU;

                default:
                    return 0;
            }
        }

        public override void WriteByte(uint addr, uint val)
        {
            switch (addr)
            {
                case 4:
                case 5:
                case 6:
                case 7:
                    irq_mask &= ~(0xffU << (((int)addr - 4) * 8));
                    irq_mask |= (val & 0xffU) << (((int)addr - 4) * 8);
                    break;
                case 8:
                    sirq = false;
                    cpu_int = false;
                    break;
            }
        }
    }
}
