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

// write a CSV text file to Intel HEX format

namespace makefont
{
    class Program
    {
        static void Main(string[] args)
        {
            System.IO.FileStream iput = new System.IO.FileStream("font.txt",
                System.IO.FileMode.Open);
            System.IO.StreamReader iput_sr = new System.IO.StreamReader(iput);

            System.IO.FileStream oput = new System.IO.FileStream("C:\\Users\\jncro\\Documents\\fpga\\vga\\font.hex",
                System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.StreamWriter oput_sr = new System.IO.StreamWriter(oput, Encoding.ASCII);

            uint addr = 0;
            while(!iput_sr.EndOfStream)
            {
                string line = iput_sr.ReadLine();
                string[] line_arr = line.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    int char_idx = int.Parse(line_arr[0], System.Globalization.NumberStyles.HexNumber);
                    if(char_idx >= 0x100)
                        continue;
                    string piece = line_arr[1];
                    {
                        string new_piece = piece.Trim();

                        for (int i = 0; i < 16; i++)
                        {
                            string byte_piece = new_piece.Substring(i * 2, 2);
                            uint ui = uint.Parse(byte_piece, System.Globalization.NumberStyles.HexNumber);
                            if (char_idx == 0)
                                ui = 0;     // make <nul> be blank

                            // swap bits in each byte
                            ui = (ui & 0xF0) >> 4 | (ui & 0x0F) << 4;
                            ui = (ui & 0xCC) >> 2 | (ui & 0x33) << 2;
                            ui = (ui & 0xAA) >> 1 | (ui & 0x55) << 1;

                            StringBuilder sb = new StringBuilder();
                            sb.Append(":01");
                            sb.Append(addr.ToString("X4"));
                            sb.Append("00");
                            sb.Append(ui.ToString("X2"));

                            uint csum = 01 + addr + (addr >> 8) + ui;
                            csum &= 0xffU;
                            csum = 0x100U - csum;
                            csum &= 0xffU;
                            sb.Append(csum.ToString("X2"));

                            addr++;
                            oput_sr.WriteLine(sb.ToString());
                        }
                    }
                }
                catch (Exception) { }
            }

            oput_sr.WriteLine(":00000001FF");
            oput_sr.Close();
            iput_sr.Close();
        }
    }
}
