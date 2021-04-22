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
    sealed partial class CodeTranslator
    {
        /// <summary> <see cref="BasicBlockStatement" /> or <see cref="ExpressionStatement" /> </summary>
        abstract class Statement
        {
        }

        /// <summary> Performs a single Block.  </summary>
        sealed class BasicBlockStatement : Statement
        {
            public Block Block;
            public BasicBlockStatement(Block b)
            {
                this.Block = b ?? throw new ArgumentNullException(nameof(b));
            }
        }

        /// <summary> Performs a single IL instruction (a expression, basically). The result is stored in the <see cref="Output" /> variable, if it's not void. </summary>
        sealed class ExpressionStatement : Statement
        {
            /// <summary> The output variable. If no output is specified, the instruction is assumed to return void and the result is discarded. </summary>
            public readonly ILVariable Output;
            public readonly ILInstruction Instruction;

            public ExpressionStatement(ILInstruction instruction, ILVariable output = null)
            {
                Output = output;
                Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            }
        }

        /// <summary> Block of statements and a result expression (in <see cref="Instr" />). </summary>
        sealed class StatementBlock
        {
            /// <summary> List of statements executed in the block. Also see <see cref="Instruction" />, the result expression that should be also executed </summary>
            public readonly ImmutableList<Statement> Statements;
            private readonly ILInstruction Instruction;

            /// <summary> Result type of the block, if it is not void. </summary>
            public IType Type { get; }
            public bool IsVoid => Type is null;

            private bool InstructionWasGivenOut = false;
            public ILInstruction Instr()
            {
                if (Instruction is null)
                    return null;
                if (InstructionWasGivenOut)
                    return Instruction.Clone();
                else
                {
                    InstructionWasGivenOut = true;
                    return Instruction;
                }
            }

            /// <summary> Block that does nothing and returns void. </summary>
            public static StatementBlock Nop = new StatementBlock();
            /// <summary> When returns true, the block does nothing and returns void. </summary>
            public bool IsNop => this.Statements.Count == 0 && Instruction is null;

            public static StatementBlock Expression(IType type, ILInstruction i)
            {
                return new StatementBlock(type, i);
            }

            public StatementBlock(params Statement[] statements) : this(null, null, statements) { }
            public StatementBlock(IEnumerable<Statement> statements) : this(null, null, statements) { }
            public StatementBlock(IType type, ILInstruction output, params Statement[] statements) : this(type, output, statements.AsEnumerable()) { }
            public StatementBlock(IType type, ILInstruction output, IEnumerable<Statement> statements)
            {
                this.Statements = statements.ToImmutableList();
                this.Statements.ForEach(Assert.NotNull);
                Assert.NotEqual(StackType.Void, output?.ResultType);
                Assert.Equal(type?.GetStackType(), output?.ResultType);
                this.Type = type;
                this.Instruction = output;
            }

            /// <summary> Drops the result value. The result expression is still executed, though. </summary>
            public StatementBlock AsVoid()
            {
                if (IsVoid) return this;
                else if (SemanticHelper.IsPure(Instruction.Flags))
                    return new StatementBlock(Statements);
                else return new StatementBlock(Statements.Add(new ExpressionStatement(Instr())));
            }

            /// <summary> As a last step of the computation, assign result into the <paramref name="resultVar" />. Result of this StatementBlock is void. </summary>
            public StatementBlock WithOutputInto(ILVariable resultVar)
            {
                if (IsVoid)
                {
                    Assert.Null(resultVar);
                    return this;
                }
                else
                {
                    Assert.NotNull(resultVar);
                    return new StatementBlock(Statements.Add(new ExpressionStatement(new StLoc(resultVar, Instr()))));
                }
            }

            /// <summary> Concatenates statement blocks. The last one's result is the result of entire computation. </summary>
            public static StatementBlock Concat(params StatementBlock[] bs) => Concat((IEnumerable<StatementBlock>)bs);
            /// <summary> Concatenates statement blocks. The last one's result is the result of entire computation. </summary>
            public static StatementBlock Concat(IEnumerable<StatementBlock> bs)
            {
                var s = ImmutableList<Statement>.Empty;
                StatementBlock last = StatementBlock.Nop;
                foreach (var b in bs)
                {
                    if (b.Statements != null && !b.Statements.IsEmpty)
                        s = s.AddRange(b.Statements);
                    last = b;
                }
                if (s.IsEmpty)
                    return last;
                else
                    return new StatementBlock(last.Type, last.Instr(), s);
            }

            /// <summary> Concatenates the effects of the specified <paramref name="blocks" />. The result values are returned separate. </summary>
            public static (StatementBlock effect, ImmutableArray<ILInstruction> results) CombineInstr(IEnumerable<StatementBlock> blocks) => CombineInstr(blocks.ToArray());
            /// <summary> Concatenates the effects of the specified <paramref name="blocks" />. The result values are returned separate. </summary>
            public static (StatementBlock effect, ImmutableArray<ILInstruction> results) CombineInstr(params StatementBlock[] blocks) =>
                CombineInstr(false, blocks);
            public static (StatementBlock effect, ImmutableArray<ILInstruction> results) CombineInstr(bool forceIntoVars, params StatementBlock[] blocks)
            {
                bool hasEmptyBody = !forceIntoVars;
                foreach (var b in blocks)
                {
                    Assert.False(b.IsVoid);
                    hasEmptyBody &= b.Statements.IsEmpty;
                }
                if (hasEmptyBody)
                    return (StatementBlock.Nop, blocks.EagerSelect(r => r.Instr()));
                else
                {
                    var vars = blocks.EagerSelect(b => new ILVariable(VariableKind.StackSlot, b.Type));
                    var result = StatementBlock.Concat(blocks.Zip(vars, (r, v) => r.WithOutputInto(v)));
                    return (result, vars.EagerSelect(v => (ILInstruction)new LdLoc(v)));
                }
            }
        }
    }
}
