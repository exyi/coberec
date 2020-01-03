using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using Xunit;

namespace Coberec.ExprCS.CodeTranslation
{
    partial class CodeTranslator
    {
        class Statement
        {
        }

        class BasicBlockStatement : Statement
        {
            public Block Block;
            public BasicBlockStatement(Block b)
            {
                this.Block = b ?? throw new ArgumentNullException(nameof(b));
            }
        }

        class ExpressionStatement : Statement
        {
            public readonly ILVariable Output;
            public readonly ILInstruction Instruction;

            public ExpressionStatement(ILInstruction instruction, ILVariable output = null)
            {
                Output = output;
                Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            }
        }
        class Result
        {
            public readonly ImmutableList<Statement> Statements;
            private readonly ILInstruction Instruction;
            private bool InstructionWasGivenOut = false;

            public IType Type { get; }
            public bool IsVoid => Type is null;

            public ILInstruction Instr()
            {
                if (InstructionWasGivenOut)
                    return Instruction.Clone();
                else
                {
                    InstructionWasGivenOut = true;
                    return Instruction;
                }
            }

            public static Result Nop = new Result();
            public bool IsNop => this.Statements.Count == 0 && Instruction is null;

            public static Result Expression(IType type, ILInstruction i)
            {
                return new Result(type, i);
            }

            public Result(params Statement[] statements) : this(null, null, statements) { }
            public Result(IEnumerable<Statement> statements) : this(null, null, statements) { }
            public Result(IType type, ILInstruction output, params Statement[] statements) : this(type, output, statements.AsEnumerable()) { }
            public Result(IType type, ILInstruction output, IEnumerable<Statement> statements)
            {
                this.Statements = statements.ToImmutableList();
                this.Statements.ForEach(Assert.NotNull);
                Assert.NotEqual(StackType.Void, output?.ResultType);
                Assert.Equal(type?.GetStackType(), output?.ResultType);
                this.Type = type;
                this.Instruction = output;
            }

            public Result AsVoid()
            {
                if (IsVoid) return this;
                else if (SemanticHelper.IsPure(Instruction.Flags))
                    return new Result(Statements);
                else return new Result(Statements.Add(new ExpressionStatement(Instr())));
            }

            public Result WithOutputInto(ILVariable resultVar)
            {
                if (IsVoid)
                {
                    Assert.Null(resultVar);
                    return this;
                }
                else
                {
                    Assert.NotNull(resultVar);
                    return new Result(Statements.Add(new ExpressionStatement(new StLoc(resultVar, Instr()))));
                }
            }

            public static Result Concat(params Result[] rs) => Concat((IEnumerable<Result>)rs);
            public static Result Concat(IEnumerable<Result> rs)
            {
                var s = ImmutableList<Statement>.Empty;
                Result last = null;
                foreach (var r in rs)
                {
                    if (r.Statements != null && !r.Statements.IsEmpty)
                        s = s.AddRange(r.Statements);
                    last = r;
                }
                if (s.IsEmpty)
                    return last;
                else
                    return new Result(last.Type, last.Instr(), s);
            }

            public static (Result, ImmutableArray<ILInstruction>) CombineInstr(IEnumerable<Result> results) => CombineInstr(results.ToArray());
            public static (Result, ImmutableArray<ILInstruction>) CombineInstr(params Result[] results)
            {
                bool ok = true;
                foreach (var r in results)
                {
                    Assert.False(r.IsVoid);
                    ok &= r.Statements.IsEmpty;
                }
                if (ok)
                    return (Result.Nop, results.EagerSelect(r => r.Instr()));
                else
                {
                    var vars = results.EagerSelect(r => new ILVariable(VariableKind.StackSlot, r.Type));
                    var result = Result.Concat(results.Zip(vars, (r, v) => r.WithOutputInto(v)));
                    return (result, vars.EagerSelect(v => (ILInstruction)new LdLoc(v)));
                }
            }
        }
    }
}
