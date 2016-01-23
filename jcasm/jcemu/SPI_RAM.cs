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
    class SPI_RAM : SPI.SlaveDevice
    {
        bool writeable;
        uint len;
        byte[] b;
        uint addr;

        uint cur_state = STATE_READY;
        uint cur_cmd = 0;
        uint addr1, addr2, addr3;

        const uint STATE_READY = 0;
        const uint STATE_ADDR1 = 1;
        const uint STATE_ADDR2 = 2;
        const uint STATE_ADDR3 = 3;
        const uint STATE_DOCMD = 4;

        public SPI_RAM(uint byte_length, bool _writeable, System.IO.BinaryReader r)
        {
            b = new byte[byte_length];

            long br_len = r.BaseStream.Length;
            long br_pos = r.BaseStream.Position;
            for(int i = 0; i < byte_length; i++, br_pos++)
            {
                if (br_pos < br_len)
                    b[i] = r.ReadByte();
                else
                    b[i] = 0;
            }
        }

        public override void SelectDevice()
        {
            //System.Diagnostics.Debugger.Log(0, "", "SPI_RAM: selected" + Environment.NewLine);
            cur_state = STATE_READY;
        }

        public override void DeselectDevice()
        {
            cur_state = STATE_READY;
        }

        public override uint HandleByte(uint v)
        {
            switch(cur_state)
            {
                case STATE_READY:
                    switch (v)
                    {
                        case 3:
                            // READ
                            addr = 0;
                            cur_cmd = 3;
                            cur_state = STATE_ADDR1;
                            return 0xff;
                        default:
                            // unsupported command
                            System.Diagnostics.Debugger.Log(0, "", "SPI_RAM: unsupported command " + v.ToString("X2") + Environment.NewLine);
                            return 0xff;
                    }

                case STATE_ADDR1:
                    addr = 0;
                    addr += (v & 0x1) << 16;
                    cur_state = STATE_ADDR2;
                    return 0xff;

                case STATE_ADDR2:
                    addr += (v & 0xff) << 8;
                    cur_state = STATE_ADDR3;
                    return 0xff;

                case STATE_ADDR3:
                    addr += v & 0xff;
                    cur_state = STATE_DOCMD;
                    return 0xff;

                case STATE_DOCMD:
                    switch(cur_cmd)
                    {
                        case 3:
                            // Read
                            //System.Diagnostics.Debugger.Log(0, "", "SPI_RAM: Read " + addr.ToString("X5") + ": " + b[addr].ToString("X2") + Environment.NewLine);
                            return b[addr++];
                    }
                    break;

            }
            // should never get here
            return 0xff;   
        }
    }
}
