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
    class SD : SPI.SlaveDevice
    {
        int cur_byte = 0;
        byte[] data = new byte[6];
        int cur_state = STATE_RECV;
        int cur_sd_state = SDSTATE_RESET;
        ulong resp = 0;
        int send_idx = 0;
        uint ocr = 0x80ff8000;

        const int STATE_RECV = 0;
        const int STATE_RECVAPP = 1;
        const int STATE_SENDR1 = 2;
        const int STATE_SENDR2 = 3;
        const int STATE_SENDR3 = 4;
        const int STATE_SENDR7 = 5;
        const int STATE_SENDR1_APP = 6;

        const int SDSTATE_RESET = 1;
        const int SDSTATE_INIT = 2;
        const int SDSTATE_READY = 3;

        public override uint HandleByte(uint v)
        {
            switch (cur_state)
            {
                case STATE_RECV:
                case STATE_RECVAPP:
                    if (cur_byte == 0 && ((v & 0xc0) != 0x40))
                        return 0xff;

                    data[cur_byte++] = (byte)v;
                    if (cur_byte == 6)
                    {
                        // handle command
                        // CRC check
                        byte crc = 0;
                        for (int i = 0; i < 5; i++)
                            crc = crc7(crc, data[i]);
                        crc = crc7end(crc);
                        crc <<= 1;
                        crc += 1;

                        send_idx = -1;
                        resp = 0;
                        if (crc != data[5])
                            resp |= 0x8;
                        else
                        {
                            if (cur_state == STATE_RECV)
                            {
                                switch (data[0] & 0x3fU)
                                {
                                    case 0:
                                        // INIT cmd
                                        cur_state = STATE_SENDR1;
                                        cur_sd_state = SDSTATE_INIT;
                                        break;

                                    case 8:
                                        if(cur_sd_state != SDSTATE_INIT)
                                        {
                                            resp |= 0x4;
                                            cur_state = STATE_SENDR1;
                                        }
                                        // SEND_IF_COND
                                        cur_state = STATE_SENDR7;
                                        // check voltage
                                        switch (data[3])
                                        {
                                            case 1:
                                            case 2:
                                            case 4:
                                            case 8:
                                                resp |= ((uint)data[3] << 24);
                                                break;
                                        }
                                        resp |= (ulong)data[4] << 32;
                                        break;

                                    case 55:
                                        // APP_CMD
                                        if(cur_sd_state == SDSTATE_READY)
                                        {
                                            resp |= 0x4;
                                            cur_state = STATE_SENDR1;
                                        }
                                        cur_state = STATE_SENDR1_APP;
                                        break;

                                    case 58:
                                        // READ_OCR
                                        cur_state = STATE_SENDR3;
                                        resp += ((ocr >> 24) & 0xff) << 8;
                                        resp += ((ocr >> 16) & 0xff) << 16;
                                        resp += ((ocr >> 8) & 0xff) << 24;
                                        resp += (ocr & 0xff) << 32;
                                        break;

                                    default:
                                        // unknown cmd
                                        System.Diagnostics.Debugger.Log(0, "", "SD: unknown command " + (data[0] & 0x3fU).ToString() + Environment.NewLine);

                                        cur_state = STATE_SENDR1;
                                        resp |= 0x4;
                                        break;
                                }
                            }
                            else
                            {
                                // APP_CMD
                                switch(data[0] & 0x3fU)
                                {
                                    case 41:
                                        // ACMD41 - complete init
                                        cur_sd_state = SDSTATE_READY;
                                        cur_state = STATE_SENDR1;
                                        break;

                                    case 55:
                                        /* In case of multiple CMD55, treat
                                            combination of last CMD55 and the
                                            command that follows as an app cmd */
                                        if (cur_sd_state == SDSTATE_READY)
                                        {
                                            resp |= 0x4;
                                            cur_state = STATE_SENDR1;
                                        }
                                        cur_state = STATE_SENDR1_APP;
                                        break;

                                    default:
                                        // unknown cmd
                                        System.Diagnostics.Debugger.Log(0, "", "SD: unknown app command " + (data[0] & 0x3fU).ToString() + Environment.NewLine);

                                        cur_state = STATE_SENDR1;
                                        resp |= 0x4;
                                        break;
                                }
                            }
                        }

                        if (cur_sd_state == SDSTATE_INIT)
                            resp |= 0x1;

                        cur_byte = 0;
                    }
                    return 0xffU;

                case STATE_SENDR1:
                case STATE_SENDR2:
                case STATE_SENDR3:
                case STATE_SENDR7:
                case STATE_SENDR1_APP:
                    if (send_idx == -1)
                    {
                        // delay state is expected by some drivers
                        send_idx++;
                        return 0xffU;
                    }

                    int num_bytes = 1;
                    switch(cur_state)
                    {
                        case STATE_SENDR1:
                        case STATE_SENDR1_APP:
                            num_bytes = 1;
                            break;
                        case STATE_SENDR2:
                            num_bytes = 2;
                            break;
                        case STATE_SENDR3:
                        case STATE_SENDR7:
                            num_bytes = 5;
                            break;
                    }
                    ulong ret = resp >> (send_idx * 8);
                    send_idx++;

                    if(send_idx == num_bytes)
                    {
                        send_idx = 0;

                        if (cur_state == STATE_SENDR1_APP)
                            cur_state = STATE_RECVAPP;
                        else
                            cur_state = STATE_RECV;
                    }
                    return (uint)(ret & 0xffU);
                    
            }
        
            System.Diagnostics.Debugger.Log(0, "", "SD: " + v.ToString("X2") + Environment.NewLine);
            return 0xff;
        }

        byte shift_bit(byte cur_val, byte next_val)
        {
            byte old_val = cur_val;
            cur_val <<= 1;
            cur_val |= next_val;
            if ((old_val & 0x40) != 0)
                cur_val ^= 0x09;
            return cur_val;
        }

        byte crc7(byte cur_val, byte next_val)
        {
            for (int i = 7; i >= 0; i--)
                cur_val = shift_bit(cur_val, (byte)((next_val >> i) & 0x1));
            return (byte)(cur_val & 0x7fU);
        }

        byte crc7end(byte cur_val)
        {
            for (int i = 0; i < 7; i++)
                cur_val = shift_bit(cur_val, 0);
            return (byte)(cur_val & 0x7fU);
        }
    }
}
