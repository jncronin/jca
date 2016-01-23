﻿/* Copyright (C) 2013-2016 by John Cronin
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
using System.Text;

namespace binary_library
{
    abstract class BaseSection : ISection
    {
        protected string name;
        protected long length;
        protected ulong load_address;
        protected long addr_align;
        protected bool is_alloc = true;
        protected bool is_writeable = true;
        protected bool is_executable = false;
        protected IBinaryFile file;

        protected List<ISymbol> symbols = new List<ISymbol>();

        public BaseSection(IBinaryFile binary_file) { if (binary_file == null) throw new ArgumentNullException(); file = binary_file; }

        public override string ToString()
        {
            return name;
        }

        public virtual string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                name = value;
            }
        }

        public virtual long Length
        {
            get
            {
                return length;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException();
                length = value;
            }
        }

        public virtual ulong LoadAddress
        {
            get
            {
                return load_address;
            }
            set
            {
                load_address = value;
            }
        }

        public virtual long AddrAlign
        {
            get
            {
                return addr_align;
            }
            set
            {
                addr_align = value;
            }
        }

        public virtual bool IsAlloc
        {
            get
            {
                return is_alloc;
            }
            set
            {
                is_alloc = value;
            }
        }

        public virtual bool IsWriteable
        {
            get
            {
                return is_writeable;
            }
            set
            {
                is_writeable = value;
            }
        }

        public abstract bool IsExecutable { get; set; }

        public abstract bool HasData { get; set; }

        public abstract IList<byte> Data { get; }


        public virtual int GetSymbolCount()
        {
            return symbols.Count;
        }

        public virtual ISymbol GetSymbol(int idx)
        {
            return symbols[idx];
        }


        public virtual IBinaryFile File
        {
            get { return file; }
        }

        public virtual int AddSymbol(ISymbol sym)
        {
            if(!symbols.Contains(sym))
                symbols.Add(sym);
            if (sym.DefinedIn != this)
                sym.DefinedIn = this;
            if (!file.ContainsSymbol(sym))
                file.AddSymbol(sym);
            return symbols.Count - 1;
        }

        public virtual void RemoveSymbol(int idx)
        {
            throw new NotImplementedException();
        }
    }

    class GeneralSection : BaseSection
    {
        public GeneralSection(IBinaryFile binary_file) : base(binary_file) { }

        bool is_exec = false;
        protected byte[] data = null;

        public override bool IsExecutable
        {
            get
            {
                return is_exec;
            }
            set
            {
                is_exec = value;
            }
        }

        public override bool HasData
        {
            get
            {
                return (data != null);
            }
            set
            {
                if (value == HasData)
                    return;
                if (value == false)
                    data = null;
                else
                    data = new byte[length];
            }
        }

        public override IList<byte> Data
        {
            get { return data; }
        }

        public override long Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
                if (HasData)
                    data = new byte[value];
            }
        }
    }

    class ContentsSection : BaseSection
    {
        protected byte[] data = new byte[0];

        public ContentsSection(IBinaryFile binary_file) : base(binary_file) { }

        public override bool HasData
        {
            get
            {
                return true;
            }
            set
            {
                if (value != true)
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override IList<byte> Data
        {
            get { return data; }
        }

        public override long Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
                data = new byte[length];
            }
        }

        public override bool IsExecutable
        {
            get
            {
                return is_executable;
            }
            set
            {
                is_executable = value;
            }
        }
    }

    class DataSection : ContentsSection, ISection
    {
        public DataSection(IBinaryFile binary_file) : base(binary_file) { }

        public override bool IsExecutable
        {
            get
            {
                return false;
            }
            set
            {
                if (value != false)
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    class CodeSection : ContentsSection, ISection
    {
        public CodeSection(IBinaryFile binary_file) : base(binary_file) { }

        public override bool IsExecutable
        {
            get
            {
                return true;
            }
            set
            {
                if (value != true)
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    class BssSection : BaseSection, ISection
    {
        public BssSection(IBinaryFile binary_file) : base(binary_file) { }

        public override bool IsExecutable
        {
            get
            {
                return false;
            }
            set
            {
                if (value != false)
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override bool HasData
        {
            get
            {
                return false;
            }
            set
            {
                if (value != false)
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override IList<byte> Data
        {
            get { throw new NotSupportedException(); }
        }
    }

    class DummySection : BaseSection, ISection
    {
        string _name;
        public DummySection(string name, IBinaryFile binary_file) : base(binary_file) { _name = name; }

        List<ISymbol> syms = new List<ISymbol>();

        public override string Name
        {
            get
            {
                return _name;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override long Length
        {
            get
            {
                return 0;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override long AddrAlign
        {
            get
            {
                return 0;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override ulong LoadAddress
        {
            get
            {
                return 0;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override bool IsAlloc
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override bool IsWriteable
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override bool IsExecutable
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override bool HasData
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override IList<byte> Data
        {
            get { throw new NotImplementedException(); }
        }

        public override int AddSymbol(ISymbol sym)
        {
            syms.Add(sym);
            return syms.Count - 1;
        }

        public override int GetSymbolCount()
        {
            return syms.Count;
        }

        public override ISymbol GetSymbol(int idx)
        {
            return syms[idx];
        }

        public override void RemoveSymbol(int idx)
        {
            syms.RemoveAt(idx);
        }
    }
}
