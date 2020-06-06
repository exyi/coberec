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

## Control flow

Same as Linq Expression, we have a `ConditionalExpression` representing an `if` statement or the `? :` ternary operator.
Other control flow statements (loops and jumps) are bit more different in our expression-based model.

Linq Expression support jumps with `goto` and labels.
The behavior is a bit weird, however.
Jumping in the middle of an expression does not make sense, since we could evaluate only a part of it and the other parts would have undefined result.
For example, a single method argument would be evaluated, and then we could not continue with invoking the method, since the other arguments were not computed.
For this reason, Linq Expressions do not support jumps to arbitrary locations, but the limitations are unclear.

WebAssembly is another expression-based system which handles control flow in a slightly different way.
It has `if`, which is very similar to our `ConditinalExpression`.
WebAssembly does not have `goto` and only supports an infinite loop (`loop` instruction) and break to a label (`br` and its shortcuts).
This ensures structured control flow, which fits nicely into the concept of expression-based code.

See https://www.w3.org/TR/wasm-core-1/#control-instructions%E2%91%A8

WebAssembly references the target label by its index, which work well for a binary format, but would not be nice in an API designed for building.
Apart from `BreakExpression`, we also introduce `BreakableExpression`, which is a target of the `break` referenced by a unique ID.

```gql
type BreakableExpression {
    expression: Expression
    label: LabelTarget
}
```

We also allow `BreakableExpression` to return a non-void value.
This value will either come from the inner `expression` or must be passed as `value` to the `BreakExpression` that interrupts the execution:

```gql
type BreakExpression
    @format(t: "break {value} -> {target}") {
    value: Expression
    target: LabelTarget
}
```

> We do not have any explicit support for early return (i.e. return that is not at the end of method body).
> Each method returns a single expression.
> However, this mechanism corresponds to early returns very closely and may be used implement as such.
> It is more generic than the C# `break` or `return`, but in some cases may emit code containing the `goto` statements.

## References

As of C# 7.0, we can work with references to variables and fields.
This means, having a field, we can:
* read the value of the field
* write a value into the field
* create a reference pointing to the field

When we take the reference to the field, we can both write and read into the reference.
This means that we only have to support taking references to fields and variables instead of having 3 different expression types for read, write or reference.

For this reason, we will only have a single `FieldAccessExpression` that returns a reference to the field.
Similarly, instead of variable assignment, we will have a `VariableReferenceExpression` that gets reference from a variable or a parameter.
To read a field, we need combine the `FieldAccessExpression` with `DereferenceExpression`; to write it, we need to combine it with `ReferenceAssignExpression`.

> This keeps the core model simpler.
> However, for ease of use, we provide helper methods like `AssignField` that create the combination at one method call.

The references can be stored in variables, passed as argument and returned from methods.
We cannot however store them in a field, since C# does not allow that.
That means that reference variables can not be passed into a local function by closure.

## ILSpy as a backend

C# is a complex language with many edge cases, so emitting code that corresponds to the exact symbols specified by the expression is very tricky.
To illustrate that, consider few examples.
Let us assume, we want to call the `Uri.EscapeDataString` method.
In our expression tree, this will be represented by a `MethodCallExpression` pointing to the descriptor of this static method.
Under standard circumstances, we invoke the method by the code `Uri.EscapeDataString(argument)`.
However, that is not the case, if there is another symbol called `Uri` in scope - for example a property:

```csharp
public string Uri { get; }
public void M() {
    var x = Uri.EscapeDataString(Uri);
}
```

In such case, we have to use the full name `System.Uri.EscapeDataString(Url)`.
This makes all code longer and moreover does not really solve our problem.
`System` corresponds to the namespace only until there is another symbol called `System`:

```csharp
public string Uri { get; }
public bool System { get; }
public void M() {
    var x = System.Uri.EscapeDataString(Uri);
}
```

We can be even more explicit and prefix the fully qualified name with `global::` to instruct the C# compiler that it should look for global symbols (i.e. namespaces).

There is many more similar cases.
Another example might be an implicit conversion.
We could think that we do not have to emit any code when an implicit conversion is used as the compiler will do the automatically.
This is not the case, however, specifically due to method overloading.
Let us say we have a class with an implicit conversion to `double`:

```csharp
public class MyNumber {
    public static implicit operator double(MyNumber @this) => ...;
    public override string ToString() => ...;
}
```

Then, we use `Console.WriteLine(object)` to print a `MyNumber` instance to stdout (which will call the `ToString` method).
Conversion to `object`, the expected argument of `WriteLine` method is implicit, but if we do not write the conversion explicitly, the `Console.WriteLine(double)` overload will be called instead.
In practice, this could lead to reduced precision of the output or wrong output format; most importantly it is a violation of the promise that our library will always reference exactly the symbol user has ordered.

Being explicit in every aspect is a way chosen by many code generators. TODO

## Metadata

Since we can not depend on System.Reflection, we need another to represent references to symbols.
We also want to generate code that defines new symbols, so our model must be account for that.
