# Design

In this chapter, we will go through the design decisions made during the project development, explore alternatives and also some potential future development.

## `System.Linq.Expressions` influence

We have had very good experience with the `System.Linq.Expressions` interface provided in .NET, which can be used to create methods at runtime.
The API is a very reliable abstraction for emitting IL instructions at runtime.
In combination with `System.Reflection.Emit`, it is possible to use it to emit a .NET assembly, but this stops being easy to use and reliable.
Since the API depends on Reflection, the referenced symbols of output assembly must be loaded into the compiler, which may collide with existing dependencies, imperiling reliability.
Also, while it is a pleasure to use `System.Linq.Expressions`, we do not think that about `System.Reflection.Emit`, which quite cumbersome.

The original project idea was to reimplement the Linq Expressions API for code generation.
However, we must not depend on Reflection to avoid the issues with dependencies, so we have to use different model of .NET metadata.
Our design of the code model is thus also expression based and the API will be very familiar to a Linq Expressions user.
There are differences, though.

### Expression based model

The similarity to `System.Linq.Expressions` is that every code fragment is an expression; there are no statements.
That does not mean that the generated code will always be a single expression; the expressions will get expanded into a reasonable C# form.
When everything is an expression, writing generic helpers gets less complicated, as we do not have to worry about the arguments being expressions or statements.
For example, we do not distinguish between `if` statement or `if` expression (the `a ? b : c` ternary operator in C#), we just need a single `ConditionalExpression`.

C# distinguishes between expressions and statements.
C# expressions always return value (i.e. can't return `void`) while statements can not.
To simplify the concept, we will represent every code fragment as a generalized expression.
Our expressions don't have to return anything, or return `void`.
We will also allow variable declarations and inline blocks in the expression.
From that perspective, our code model would be more similar to F#, Scala or other expression based languages.

## Blocks and variables

We will represent blocks and variable definition a bit differently than Linq.Expressions, to more closely follow single responsibility principe and thus make the types simpler.
Linq Expression use one node type ([`BlockExpression`](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.blockexpression?view=netcore-3.1)) to both declare variables and chain expressions.
We will separate the two responsibilities into `BlockExpression` and `LetInExpression`.

```gql
type BlockExpression {
    expressions: [Expression]
    result: Expression
}
```

The block expression executes a list of expressions ignoring their results and then returns the `result`.
The `result` may be `default(void)` expression, if there is no need for a result value.

```gql
type LetInExpression {
    variable: ParameterExpression
    value: Expression
    target: Expression
}
```

The name is inspired by the `let variable = value in target` syntax from F#, since this expression has the same semantics.
The variable is first initialized by the `value` and then `target` is evaluated with the new variable in scope.

### Control flow

Same as Linq Expression, we have a `ConditionalExpression` representing an `if` statement or the `? :` ternary operator.
Other control flow statements (loops and jumps) are bit more different in our expression-based model.

Linq Expression support jumps with `goto` and labels.
The behavior is a bit weird, however.
Jumping in the middle of an expression does not make sense, since we could evaluate only a part of it and the other parts would have undefined result.
For example, a single method argument would be evaluated, and then we could not continue with invoking the method, since the other arguments were not computed.
For this reason, Linq Expressions do not support jumps to arbitrary locations, but the limitations are unclear.

WebAssembly is another expression-based system
