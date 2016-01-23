/* Copyright (C) 2013-2016 by John Cronin
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

namespace tl
{
    class Program
    {
        internal static Options options = new Options();
        static List<string> ifiles = new List<string>();
        static string output_file = null;

        static void Main(string[] args)
        {
            if (ParseArgs(args) == false)
            {
                DispUsage();
                return;
            }

            // Initialize the default linker scripts
            DefaultScripts.InitScripts();

            // Load the input files
            if(ifiles.Count == 0)
            {
                Console.WriteLine("No input files!");
                return;
            }
            List<binary_library.IBinaryFile> inputs = new List<binary_library.IBinaryFile>();
            foreach (string ifile in ifiles)
            {
                binary_library.elf.ElfFile ef = new binary_library.elf.ElfFile();
                ef.Filename = ifile;
                ef.Read();
                inputs.Add(ef);
            }

            // Determine the architecture
            string cur_arch = null;
            if (options["arch"].Set)
                cur_arch = options["arch"].String;
            foreach (binary_library.IBinaryFile ifile in inputs)
            {
                if (cur_arch == null)
                    cur_arch = ifile.NameTriple;
            }

            string[] ntriple = cur_arch.Split('-');
            if(ntriple.Length != 3)
            {
                Console.WriteLine("Error: invalid architecture: " + cur_arch);
                throw new Exception();
            }

            // Check input files are of the appropriate architecture
            foreach (var ifile in inputs)
            {
                if (ntriple[0] != ifile.Architecture)
                {
                    Console.WriteLine("Error: " + ifile.Filename + " is not built for architecture " + ntriple[0]);
                    throw new Exception();
                }
            }

            // Create an output file
            binary_library.IBinaryFile of = null;
            string oformat = null;
            if (options["oformat"].Set)
                oformat = options["oformat"].String.ToLower();
            else
                oformat = "elf";

            if (oformat == "elf")
                of = new binary_library.elf.ElfFile();
            else if (oformat == "binary")
                of = new binary_library.binary.FlatBinaryFile();
            else if (oformat == "hex")
                of = new binary_library.binary.HexFile();
            else
                throw new Exception("Unsupported output format: " + oformat);

            if (output_file == null)
                output_file = "a.out";
            
            of.Init();
            of.Architecture = ntriple[0];
            of.BinaryType = ntriple[1];
            of.OS = ntriple[2];

            // Get the linker script
            LinkerScript script = DefaultScripts.GetScript(ntriple);
            
            // Run the script
            script.RunScript(of, inputs);

            of.Filename = output_file;
            of.Write();
        }

        private static bool ParseArgs(string[] args)
        {
            string[] valid_args = new string[]
            {
                "text-start-addr",
                "data-start-addr",
                "rodata-start-addr",
                "bss-start-addr",
                "page-size",
                "arch",
                "oformat"
            };
                
            int i = 0;
            while (i < args.Length)
            {
                if (args[i] == "-o")
                {
                    i++;
                    if ((i >= args.Length) || (args[i].StartsWith("-")))
                        return false;
                    output_file = args[i];
                }
                else if (args[i].StartsWith("-"))
                {
                    bool is_valid = false;
                    if (!args[i].StartsWith("--"))
                        return false;
                    else
                    {
                        foreach (string valid_arg in valid_args)
                        {
                            if (valid_arg.Length + 3 <= args[i].Length &&
                                args[i].Substring(2, valid_arg.Length).Equals(valid_arg) &&
                                args[i][2 + valid_arg.Length] == '=')
                            {
                                is_valid = true;
                                options[valid_arg] = Options.OptionValue.InterpretString(args[i].Substring(3 + valid_arg.Length));
                                break;
                            }
                        }
                    }
                    if (!is_valid)
                        return false;
                }
                else
                    ifiles.Add(args[i]);
                i++;
            }
            return true;

        }

        private static void DispUsage()
        {
            throw new NotImplementedException();
        }
    }
}
