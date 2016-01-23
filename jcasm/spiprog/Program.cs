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
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace spiprog
{
    class Program
    {
        static SerialPort sp;

        static void Main(string[] args)
        {
            var fs = new FileStream("D:/cygwin64/home/jncro/fpga/prog.elf",
                FileMode.Open, FileAccess.Read);

            var test = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
            //var br = new BinaryReader(new MemoryStream(test));
            var br = new BinaryReader(fs);

            sp = new SerialPort();
            sp.BaudRate = 19200;
            sp.DataBits = 8;
            sp.StopBits = StopBits.One;
            sp.Parity = Parity.None;
            sp.PortName = "COM9";

            sp.Open();

            int cur_addr = 0;
            int cur_chip = -1;
            int size = (int)br.BaseStream.Length;

            // Set up spi outputs to mode 0, 200 kHz
            SendCommand(0x00, 0x00);
            SendCommand(0x01, 0x00);
            SendCommand(0x02, 0x00);
            SendCommand(0x03, 0x00);
            SendCommand(0x04, 250);      // 50MHz/200 kHz = 250

            while(cur_addr < size)
            {
                if((cur_addr % 0x20000) == 0)
                {
                    // Select next chip
                    Console.Write("C");
                    cur_chip++;
                    SendCommand(0x05, 0x00);

                    // Get chip signature
                    SendCommand(0x05, 0x09 + cur_chip);
                    SendCommand(0x06, 0xab);
                    SendCommand(0x06, 0x00);
                    SendCommand(0x06, 0x00);
                    SendCommand(0x06, 0x00);
                    int sig = SendCommand(0x06, 0xff);
                    SendCommand(0x05, 0x00);
                    Console.Write(sig.ToString("X2"));
                }

                if((cur_addr % 256) == 0)
                {
                    Console.Write("P");
                    // End last transmission
                    SendCommand(0x05, 0x00);

                    // Wait for write in progress to go low
                    int status = 0;
                    do
                    {
                        SendCommand(0x05, 0x09 + cur_chip);
                        SendCommand(0x06, 0x05);
                        status = SendCommand(0x06, 0xff);
                        SendCommand(0x05, 0x00);
                        Console.Write(status.ToString("X2"));
                    } while ((status & 0x1) != 0);


                    // Write enable
                    SendCommand(0x05, 0x09 + cur_chip);
                    SendCommand(0x06, 0x06);
                    SendCommand(0x05, 0x00);

                    // Select next page
                    SendCommand(0x05, 0x09 + cur_chip);
                    SendCommand(0x06, 0x02);
                    SendCommand(0x06, (cur_addr >> 16) & 0x1);
                    SendCommand(0x06, (cur_addr >> 8) & 0xff);
                    SendCommand(0x06, cur_addr & 0xff);
                }

                // Send data byte
                byte b = br.ReadByte();
                SendCommand(0x06, b);

                Console.Write("+");

                cur_addr++;
            }
            SendCommand(0x05, 0x00);

            // verify
            cur_addr = 0;
            cur_chip = -1;
            br.BaseStream.Position = 0;
            while (cur_addr < size)
            {
                if ((cur_addr % 0x20000) == 0)
                {
                    // Select next chip
                    Console.Write("C");
                    cur_chip++;
                    SendCommand(0x05, 0x00);

                    // Get chip signature
                    SendCommand(0x05, 0x09 + cur_chip);
                    SendCommand(0x06, 0xab);
                    SendCommand(0x06, 0x00);
                    SendCommand(0x06, 0x00);
                    SendCommand(0x06, 0x00);
                    int sig = SendCommand(0x06, 0xff);
                    SendCommand(0x05, 0x00);
                    if (sig != 0x29)
                        throw new Exception();
                    Console.Write(sig.ToString("X2"));

                    // Select read
                    SendCommand(0x05, 0x09 + cur_chip);
                    SendCommand(0x06, 0x03);
                    SendCommand(0x06, (cur_addr >> 16) & 0x1);
                    SendCommand(0x06, (cur_addr >> 8) & 0xff);
                    SendCommand(0x06, cur_addr & 0xff);
                }

                var vb = SendCommand(0x06, 0xff);
                byte b = br.ReadByte();

                if (vb == b)
                    Console.Write("v");
                else
                    throw new Exception();

                cur_addr++;
            }
            SendCommand(0x05, 0x00);



            br.Close();
            sp.Close();
        }

        static int SendCommand(int cmd_idx, int cmd_data)
        {
            // Clear receive buffer
            while (sp.BytesToRead > 0)
                sp.ReadByte();

            byte[] msg = new byte[] { (byte)cmd_idx, (byte)cmd_data };
            sp.Write(msg, 0, 2);

            while (sp.BytesToRead == 0) ;
            int ret = sp.ReadByte();
            while (sp.BytesToRead > 0)
                System.Diagnostics.Debugger.Log(0, "", "Spurious byte: " + sp.ReadByte().ToString("X2"));

            //System.Diagnostics.Debugger.Log(0, "", msg[0].ToString("X2") + " " + msg[1].ToString("X2") + ": " + ret.ToString("X2") + Environment.NewLine);
            return ret;
        }
    }
}
