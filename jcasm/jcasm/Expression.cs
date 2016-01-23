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

namespace jcasm
{
    class MakeState
    {
        internal Dictionary<string, LabelOffset> lo;
        internal Section cs;
        internal MakeState(Dictionary<string, LabelOffset> label_offsets, Section cur_sect)
        { lo = label_offsets; cs = cur_sect; }

        internal bool IsDefined(string val)
        {
            if (!lo.ContainsKey(val))
                return false;
            if (lo[val].Section != cs)
                return false;
            return true;
        }

        internal Expression.EvalResult GetDefine(string val)
        {
            return new Expression.EvalResult(lo[val].Offset);
        }
    }

    internal class Expression
    {
        public Expression a, b;
        public Tokens op;

        public virtual EvalResult Evaluate(MakeState s)
        {
            EvalResult ea, eb;

            switch (op)
            {
                case Tokens.NOT:
                    ea = a.Evaluate(s);
                    return new EvalResult(~ea.AsInt);

                case Tokens.LNOT:
                    ea = a.Evaluate(s);
                    if (ea.AsInt == 0)
                        return new EvalResult(1);
                    else
                        return new EvalResult(0);

                case Tokens.LAND:
                    ea = a.Evaluate(s);
                    if (ea.AsInt == 0)
                        return new EvalResult(0);
                    eb = b.Evaluate(s);
                    if (eb.AsInt == 0)
                        return new EvalResult(0);
                    return new EvalResult(1);

                case Tokens.LOR:
                    ea = a.Evaluate(s);
                    if (ea.AsInt != 0)
                        return new EvalResult(1);
                    eb = b.Evaluate(s);
                    if (eb.AsInt != 0)
                        return new EvalResult(1);
                    return new EvalResult(0);

                case Tokens.PLUS:
                    ea = a.Evaluate(s);
                    eb = b.Evaluate(s);

                    if (ea.Type == EvalResult.ResultType.Int && eb.Type == EvalResult.ResultType.Int)
                        return new EvalResult(ea.intval + eb.intval);
                    else if (ea.Type == EvalResult.ResultType.String && eb.Type == EvalResult.ResultType.String)
                        return new EvalResult(ea.strval + eb.strval);
                    else if (ea.Type == EvalResult.ResultType.Void && eb.Type == EvalResult.ResultType.Void)
                        return new EvalResult();
                    else if(ea.Type == EvalResult.ResultType.Relocation && eb.Type == EvalResult.ResultType.Int)
                        return new EvalResult(new Relocation
                        {
                            Addend = ea.relocval.Addend + eb.intval,
                            IsPCRel = ea.relocval.IsPCRel,
                            SourceOffset = ea.relocval.SourceOffset,
                            SourceSection = ea.relocval.SourceSection,
                            TargetName = ea.relocval.TargetName,
                            TargetSection = ea.relocval.TargetSection,
                            Type = ea.relocval.Type
                        });
                    else if(ea.Type == EvalResult.ResultType.Int && eb.Type == EvalResult.ResultType.Relocation)
                        return new EvalResult(new Relocation
                        {
                            Addend = eb.relocval.Addend + ea.intval,
                            IsPCRel = eb.relocval.IsPCRel,
                            SourceOffset = eb.relocval.SourceOffset,
                            SourceSection = eb.relocval.SourceSection,
                            TargetName = eb.relocval.TargetName,
                            TargetSection = eb.relocval.TargetSection,
                            Type = eb.relocval.Type
                        });

                    else
                        throw new Exception("Mismatched arguments to PLUS: " + ea.Type.ToString() + " and " + eb.Type.ToString());

                case Tokens.MUL:
                    ea = a.Evaluate(s);
                    eb = b.Evaluate(s);

                    return new EvalResult(ea.AsInt * eb.AsInt);

                case Tokens.MINUS:
                    ea = a.Evaluate(s);

                    if (b == null)
                    {
                        // unary minus
                        if (ea.Type == EvalResult.ResultType.Void)
                            return ea;
                        if (ea.Type == EvalResult.ResultType.String)
                            throw new Exception("Cannot apply unary minus to type string");
                        return new EvalResult(0 - ea.intval);
                    }
                    else
                    {
                        eb = b.Evaluate(s);

                        if (ea.Type == EvalResult.ResultType.String && (eb.Type == EvalResult.ResultType.Int || eb.Type == EvalResult.ResultType.Void))
                        {
                            int rem_amount = eb.AsInt;
                            if (rem_amount > ea.strval.Length)
                                rem_amount = ea.strval.Length;
                            return new EvalResult(ea.strval.Substring(0, ea.strval.Length - rem_amount));
                        }
                        else if (ea.Type == EvalResult.ResultType.String && eb.Type == EvalResult.ResultType.String)
                        {
                            if (ea.strval.EndsWith(eb.strval))
                                return new EvalResult(ea.strval.Substring(0, ea.strval.Length - eb.strval.Length));
                            else
                                throw new Exception(ea.strval + " does not end with " + eb.strval);
                        }
                        else if (ea.Type == EvalResult.ResultType.Void && eb.Type == EvalResult.ResultType.Void)
                        {
                            return new EvalResult();
                        }
                        else if(ea.Type == EvalResult.ResultType.Relocation && eb.Type == EvalResult.ResultType.Relocation)
                        {
                            if (ea.relocval.TargetSection != null &&
                                ea.relocval.TargetSection == eb.relocval.TargetSection)
                            {
                                return new EvalResult(Program.los[ea.relocval.TargetName].Offset -
                                    Program.los[eb.relocval.TargetName].Offset);
                            }
                            else throw new NotSupportedException();
                        }
                        else
                        {
                            return new EvalResult(ea.AsInt - eb.AsInt);
                        }
                    }

                case Tokens.EQUALS:
                case Tokens.NOTEQUAL:
                    {
                        int _true = 1;
                        int _false = 0;

                        if (op == Tokens.NOTEQUAL)
                        {
                            _true = 0;
                            _false = 1;
                        }

                        ea = a.Evaluate(s);
                        eb = b.Evaluate(s);

                        if (ea.Type == EvalResult.ResultType.String && eb.Type == EvalResult.ResultType.String)
                        {
                            if (ea.strval == null)
                            {
                                if (eb.strval == null)
                                    return new EvalResult(_true);
                                else
                                    return new EvalResult(_false);
                            }
                            if (ea.strval.Equals(eb.strval))
                                return new EvalResult(_true);
                            else
                                return new EvalResult(_false);
                        }
                        else
                        {
                            if (ea.AsInt == eb.AsInt)
                                return new EvalResult(_true);
                            else
                                return new EvalResult(_false);
                        }
                    }

                case Tokens.LT:
                    ea = a.Evaluate(s);
                    eb = b.Evaluate(s);

                    if (ea.AsInt < eb.AsInt)
                        return new EvalResult(1);
                    else
                        return new EvalResult(0);

            }

            throw new NotImplementedException(op.ToString());
        }

        public class EvalResult
        {
            public enum ResultType { Int, String, Void, Register, Relocation };

            public ResultType Type;

            public string strval;
            public int intval;
            public Relocation relocval;

            public EvalResult()
            {
                Type = ResultType.Void;
            }
            public EvalResult(int i)
            {
                Type = ResultType.Int;
                intval = i;
            }
            public EvalResult(string s)
            {
                Type = ResultType.String;
                strval = s;
            }
            public EvalResult(Relocation r)
            {
                Type = ResultType.Relocation;
                relocval = r;
            }

            public int AsInt
            {
                get
                {
                    switch (Type)
                    {
                        case ResultType.Int:
                            return intval;
                        case ResultType.String:
                            if (strval == null || strval == "")
                                return 0;
                            return 1;
                        case ResultType.Register:
                            return Program.regs[strval];
                        case ResultType.Void:
                            return 0;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }

            public static implicit operator Expression(EvalResult er)
            {
                return new ResultExpression { e = er };
            }

            public override string ToString()
            {
                switch (Type)
                {
                    case ResultType.Int:
                        return intval.ToString();
                    case ResultType.String:
                        return "\"" + strval + "\"";
                    case ResultType.Void:
                        return "{void}";
                    case ResultType.Relocation:
                        return relocval.ToString();
                    default:
                        throw new NotSupportedException();
                }
            }
        }
    }

    internal class ResultExpression : Expression
    {
        public EvalResult e;

        public override EvalResult Evaluate(MakeState s)
        {
            return e;
        }
    }

    internal class StringExpression : Expression
    {
        public string val;

        public override EvalResult Evaluate(MakeState s)
        {
            return new EvalResult(val);
        }
    }

    internal class LabelExpression : Expression
    {
        public string val;
        public string cur_outer_label;
        public bool is_pcrel = false;
        public int addend = 0;

        public override EvalResult Evaluate(MakeState s)
        {
            // Is it a register?
            if(Program.regs.ContainsKey(val.ToUpper()))
            {
                var e = new EvalResult();
                e.strval = val.ToUpper();
                e.intval = Program.regs[e.strval];
                e.Type = EvalResult.ResultType.Register;
                return e;
            }

            string full_name = val;

            // If defined in the current file, say so
            if (s.lo.ContainsKey(val))
            {
                // Yes - emit a relocation to the label
                Relocation r = new Relocation();
                r.TargetName = val;
                r.TargetSection = s.lo[val].Section;
                r.Addend = addend;
                r.SourceSection = s.cs;
                r.IsPCRel = is_pcrel;

                return new EvalResult(r);
            }
            else
            {
                // ld defines all unknown symbols as externals
                Relocation r = new Relocation();
                r.TargetName = val;
                r.TargetSection = null;
                r.Addend = addend;
                r.SourceSection = s.cs;
                r.IsPCRel = is_pcrel;

                return new EvalResult(r);
            }
            
            /*if (s.IsDefined(val) == true)
                 return s.GetDefine(val);
            {
                // Not defined in the current section.  Is it
                //  in another?
                if (s.lo.ContainsKey(val))
                {
                    // Yes - emit a relocation to the label
                    Relocation r = new Relocation();
                    r.TargetName = val;
                    r.TargetSection = s.lo[val].Section;
                    r.Addend = addend;
                    r.SourceSection = s.cs;
                    r.IsPCRel = is_pcrel;

                    return new EvalResult(r);
                }

                // ld defines all unknown symbols as externals
                Relocation rext = new Relocation();
                rext.TargetName = val;
                rext.TargetSection = null;
                rext.Addend = addend;
                rext.SourceSection = s.cs;
                rext.IsPCRel = is_pcrel;

                return new EvalResult(rext);
            }*/
        }
    }

    internal class IntExpression : Expression
    {
        public int val;

        public override EvalResult Evaluate(MakeState s)
        {
            return new EvalResult(val);
        }
    }
}
