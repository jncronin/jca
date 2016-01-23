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
    class Timer : MemoryRegion, ITick, IIRQ
    {
        uint cur_val = 0;
        uint match_val = 0;

        long old_dt;
        bool has_dt = false;

        public void Tick()
        {
            uint increment = 0;
            if (has_dt)
            {
                var now = System.DateTime.Now.Ticks;
                var diff = now - old_dt;
                old_dt = now;

                if (diff == 0)
                    diff = 1;
                /* There are 10,000 .NET ticks per millisecond,
                    however there are 50,000 JCA timer ticks per millisecond
                    We therefore multiply diff by 5 here */
                diff *= 50000 / TimeSpan.TicksPerMillisecond;
                increment = (uint)diff;
            }
            else
            {
                increment = 1;
                old_dt = System.DateTime.Now.Ticks;
                has_dt = true;
            }

            unchecked { cur_val = cur_val + increment; }
        }

        public override uint Length { get { return 8; } }
        public override uint ReadByte(uint addr)
        {
            switch (addr)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    return (cur_val >> (((int)addr - 0) * 8)) & 0xffU;
                case 4:
                case 5:
                case 6:
                case 7:
                    return (match_val >> (((int)addr - 4) * 8)) & 0xffU;
                default:
                    return 0;
            }
        }

        public override void WriteByte(uint addr, uint val)
        {
            switch (addr)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    cur_val &= ~(0xffU << (((int)addr - 0) * 8));
                    cur_val |= (val & 0xffU) << (((int)addr - 0) * 8);
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                    match_val &= ~(0xffU << (((int)addr - 4) * 8));
                    match_val |= (val & 0xffU) << (((int)addr - 4) * 8);
                    break;
            }
        }

        public bool IsIRQSignalled()
        {
            if (match_val == 0)
                return false;
            return cur_val >= match_val;
        }
    }
}
