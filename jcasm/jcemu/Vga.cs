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
using System.Windows.Forms;

namespace jcemu
{
    class Vga : RAM
    {
        PaintableForm f;

        public Vga() : base(4096)
        {

            System.Threading.Thread t = new System.Threading.Thread(
                new System.Threading.ThreadStart(WindowProc));
            t.Start();
        }

        protected override void OnWriteByte(uint addr, uint val)
        {
            f.Invoke(new PaintableForm.UpdateDelegate(f.UpdateAddr), addr);
        }

        void WindowProc()
        {
            f = new PaintableForm(mem);

            var g = f.CreateGraphics();
            var font_bmp = new System.Drawing.Bitmap(8 * 256, 16,
                g);
            g.Dispose();

            // Load up the font data
            var fs = new System.IO.FileStream("C:\\Users\\jncro\\Documents\\fpga\\vga\\font.hex",
                System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var sr = new System.IO.StreamReader(fs);
            while(sr.EndOfStream == false)
            {
                string line = sr.ReadLine();
                if (!line.StartsWith(":01"))
                    break;

                uint addr = uint.Parse(line.Substring(3, 4),
                    System.Globalization.NumberStyles.HexNumber);
                uint val = uint.Parse(line.Substring(9, 2),
                    System.Globalization.NumberStyles.HexNumber);

                int char_idx = (int)(addr / 16);
                int char_row = (int)(addr % 16);

                for(int i = 0; i < 8; i++)
                {
                    uint bit_val = (val >> i) & 0x1;
                    System.Drawing.Color c;
                    if (bit_val == 0)
                        c = System.Drawing.Color.Black;
                    else
                        c = System.Drawing.Color.White;

                    font_bmp.SetPixel(char_idx * 8 + i, char_row, c);
                }
            }
            fs.Close();


            f.f = font_bmp;
            
            f.Show();

            Application.Run();
        }

        class PaintableForm : Form
        {
            public PaintableForm(byte[] d)
            { data = d; }
            byte[] data;
            internal System.Drawing.Bitmap f;
            internal delegate void UpdateDelegate(uint addr);

            public PaintableForm()
            {
                SetStyle(ControlStyles.Opaque, true);
            }

            protected override void OnLoad(EventArgs e)
            {
                ClientSize = new System.Drawing.Size(640, 480);
                Text = "JCA VGA";
            }

            internal void UpdateAddr(uint addr)
            {
                int char_width = ClientRectangle.Width / 80;
                int char_height = ClientRectangle.Height / 25;

                if (char_width == 0)
                    char_width = 1;
                if (char_height == 0)
                    char_height = 1;

                int x = (int)(addr % 128);
                int y = (int)(addr / 128);

                if (x > 80)
                    return;
                if (y > 25)
                    return;

                Invalidate(new System.Drawing.Rectangle(x * char_width,
                    y * char_height, char_width, char_height));
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.FillRectangle(System.Drawing.Brushes.Black,
                    e.ClipRectangle);
            }

            protected override void OnResize(EventArgs e)
            {
                this.Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                // Determine the rectangle to paint in terms of characters
                int top, left, bottom, right;

                int char_width = ClientRectangle.Width / 80;
                int char_height = ClientRectangle.Height / 25;

                if (char_width == 0)
                    char_width = 1;
                if (char_height == 0)
                    char_height = 1;

                top = e.ClipRectangle.Top / char_height;
                left = e.ClipRectangle.Left / char_width;
                if ((e.ClipRectangle.Bottom % char_height) == 0)
                    bottom = e.ClipRectangle.Bottom / char_height;
                else
                    bottom = e.ClipRectangle.Bottom / char_height + 1;
                if ((e.ClipRectangle.Right % char_width) == 0)
                    right = e.ClipRectangle.Right / char_width;
                else
                    right = e.ClipRectangle.Right / char_width + 1;

                for(int y = top; y < bottom; y++)
                {
                    if (y >= 25)
                        continue;
                    for(int x = left; x < right; x++)
                    {
                        if (x >= 80)
                            continue;
                        byte cur_char = data[y * 128 + x];

                        System.Drawing.Rectangle src = new System.Drawing.Rectangle(cur_char * 8,
                            0, 8, 16);
                        System.Drawing.Rectangle dest = new System.Drawing.Rectangle(
                            x * char_width, y * char_height, char_width, char_height);

                        // Draw character
                        e.Graphics.DrawImage(f, dest, src, System.Drawing.GraphicsUnit.Pixel);
                            
                    }
                }
                
            }
        }
    }
}
