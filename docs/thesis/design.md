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

Being explicit in every aspect is a way chosen by many code generators.
An example might the code generator for resource (`.resx`) files ([example file](https://github.com/dotnet/runtime/blob/3bb5f14/src/libraries/Common/tests/Resources/Strings.Designer.cs)) or DotVVM view compiler ([generator implementation](https://github.com/riganti/dotvvm/blob/61ee3fd/src/DotVVM.Framework/Compilation/DefaultViewCompilerCodeEmitter.cs#L702)).
However, the explicitness heavily afflicts readability of the produced code.
We think that that transparency and "debuggability" is a very important advantage of code generation compared to the other approaches, so we would like to get the best of both worlds by having a smart code generator.

Implementing such a smart code generator would be very demanding, so we are not aware of anyone doing that in C# for the purpose of code generation.
However, the [ILSpy project](https://github.com/icsharpcode/ILSpy) shares the same problem and it already has a very reliable C# emitter.
ILSpy is a decompiler for .NET assemblies and the authors take correctness and precision very seriously.

We could say that ILSpy is also a code generator, but the source metadata is the .NET intermediate language (IL).
In the first step of the decompilation, ILSpy parses IL into an internal abstract tree - [the ILAst tree](https://github.com/icsharpcode/ILSpy/blob/faea7ee90d636fe8d2bc6a2f7f7b00dada9f01b2/doc/ILAst.txt).
After that, many transformations are ran on the tree and then it is translated into a C# syntax tree.
Few other transformation run on the C# syntax tree and then it is formatted into a text form.

To avoid too noisy output and too complicated implementation, we will translate our expression into ILSpy's internal ILAst tree and then let ILSpy produce the code.

## Metadata

Since we can not depend on System.Reflection, we need another to represent references to symbols.
We also want to allow users to define new symbols, so our model must be account for that.

### ILSpy type system

ILSpy has its own type system built on top of [`System.Reflection.Metadata` library](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.metadata?view=netcore-3.1) (that is very different from standard Reflection).
`System.Reflection.Metadata` only allows reading .NET assemblies, it is not possible to define new symbols on fly.
However, ILSpy type system is quite extensible, so it is possible to declare new symbols.

We started with using the ILSpy type system directly in our expression trees, but it has several deficiencies.
In ILSpy, all the symbols are known in advance, there are not any new symbols emerging after the assemblies are loaded.
This is not the case in our project, where the entire point is to declare new symbols.
Types in ILSpy system contain references to all other related symbols like declaring type and the members.
That is convenient when we inspect the types, but complicates everything when the types are created.
The references in ILSpy type system are cyclic, so we have to make the some of the properties mutable to make their creation possible.
ILSpy however assumes that symbols do not change at some places and caches information.

Having mutable types also complicates the design for us.
While the expression tree is created, we would like to validate basic constraints (if types fit, correct number of arguments was specified, ...).
The validation however stops making sense, when some of the validated properties may change.
Having an immutable model of metadata would be very beneficial for this purpose.

Also, by using ILSpy type system, we would lock ourselves to using ILSpy as a backend.
At the time publishing of this work, there is no support for any other backend.
However, it could make sense to create one - for example, we could add support for Reflection.Emit and Linq.Expression to provide a common API for generating code at compile time and code at runtime.

### Signatures

We cannot simply copy the ILSpy system or reflection and allow additions of new symbols, since we want to have the metadata immutable.
Immutability will be a promise that whenever a function gets information about a symbol, it won't change.

We need to allow adding new symbols even when there are cyclic references (like recursive methods and recursive types).
This means that information about type can not contain its members and information about methods can not contain the body.
We will split the responsibilities - type or method **definition** will contain all information about its contents and a **signature**, that will only contain the most basic information.

To define a type definition, the user will need all members of the type already defined.
On the other hand, creating a signature will be trivial - they will just need to know the full name and basic information (parameters, ...).
To reference a symbol from code (such as calling a method), only the signature will be needed.
So, symbols can be referenced before they are defined with all the information.
Moreover, validating usage should be sound, since all properties of the type, method, field or property signature are going to be immutable.

Type signatures contain information about its full name, accessibility, kind of the type (interface, struct, class, ...), if it is abstract or sealed and the generic type parameters.
Type members contain a signature of the declaring type, name of the member and other basic information (arguments, result type, if it is static, virtual or abstract).

> For more details, see the documentation of `TypeSignature`, `MethodSignature`, `FieldSignature` and `PropertySignature` (TODO link)

### References

.NET has support generics - types and methods may be parametrized by a type argument.
Since we want to support the concept, every time we will be referencing a symbol from code, we will provide a list of type arguments for that symbol.

It is important to make a distinction between a symbol signature and a symbol reference.
Simply put, signature is a generic version of the symbol - as it is declared in the code, the type parameters are unassigned.
Reference is a specialized version of the symbol - the type parameters have their arguments filled in.
When declaring symbols, we use the signatures - we only know the definition of the type parameter.
On the other hand, in code, we almost always need to know the type including its type argument.
It would not make any sense to create a instance of `List<?>`, for example.

When the symbol is not generic, there are no generic arguments to be filled and the reference becomes basically equivalent to a signature.
Because most types nd method are not generic, this is a very common situation and we will have an implicit conversion from signature to reference.
It will throw an exception if there would be missing types arguments, which is not considered a good practice for implicit conversions.
However, it saves a lot of typing, so we find it useful anyway.

> Note that the type arguments may still contain different generic parameters.
> When we are declaring a generic type or method, we will be using the its generic parameters (instances of `GenericParameter`) in the symbol references.
> We can then use the parameters in the arguments of other types.
> The different between a signature and a reference is similar to `typeof(List<>)` and `typeof(List<X>)`.
> In the second case (similar to reference), the List is not specialized by another parameter `X`, not its own `T`.
