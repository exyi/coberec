# Design

In this chapter, we will go through the design decisions made during the project development, explore alternatives and also some potential future development.

## `System.Linq.Expressions` influence

We have had a good experience with the `System.Linq.Expressions` interface provided in .NET, which can be used to create methods at runtime.
The API is a very reliable abstraction for emitting IL instructions at runtime.
In combination with `System.Reflection.Emit`, it is possible to use it to emit a .NET assembly, but this stops being easy to use and reliable.
Since the API depends on Reflection, the referenced symbols of output assembly must be loaded into the code generator, which may collide with existing dependencies, afflicting reliability.
Also, while it is a pleasure to use `System.Linq.Expressions`, we do not think that about `System.Reflection.Emit`, which quite cumbersome.

The original project idea was to reimplement the Linq Expressions API for code generation.
However, we must not depend on Reflection to avoid the issues with dependencies, so we have to use a different model of .NET metadata.
Our design of the code model is thus also expression-based, and the API will be very familiar to a Linq Expressions user.
There are going to be differences, however.

### Expression based model

Major similarity to `System.Linq.Expressions` is that every code fragment is an expression; there are no statements.
That does not mean that the generated code will always be a single expression; the expressions will get expanded into a reasonable C# form.
When everything is an expression, writing generic helpers gets less complicated, as we do not have to worry about the arguments being expressions or statements.
For example, we do not distinguish between `if` statement or `if` expression (the `a ? b : c` ternary operator in C#), we just need a single `ConditionalExpression`.

C# distinguishes between expressions and statements.
C# expressions always return a value (i.e. cannot return `void`) while statements do not.
To simplify the concept, we will represent every code fragment as a generalized expression.
Our expressions do not have to return anything - we allow returning `void`.
We will also allow variable declarations and inline blocks in the expressions.
From that perspective, our code model would be similar to F#, Scala or other expression-based languages.

## Blocks and variables

We will represent blocks and variable definition a bit differently than Linq Expressions, to more closely follow the single responsibility principle and thus make the types simpler.
Linq Expressions use one node type ([`BlockExpression`](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.blockexpression?view=netcore-3.1)) to both declare variables and chain expressions.
We will separate the two responsibilities into `BlockExpression` and `LetInExpression`.

```gql
type BlockExpression {
    expressions: [Expression]
    result: Expression
}
```

The block expression executes a list of expressions ignoring their results and then returns the `result`.
The `result` may be `default(void)` expression, if there is no need to return a value from the block.

```gql
type LetInExpression {
    variable: ParameterExpression
    value: Expression
    target: Expression
}
```

The name is inspired by the `let variable = value in target` syntax from F#, since this expression has the same semantics.
First, the variable is initialized by the `value` and then `target` is evaluated with the new variable in scope.

## Control flow

Same as Linq Expressions, we have a `ConditionalExpression` representing an `if` statement or the `? :` ternary operator.
Other control flow statements (loops and jumps) are a bit more different in our expression-based model.

Linq Expressions support jumps with `goto` and labels.
The behaviour is a bit weird, however.
Jumping in the middle of an expression does not make sense since that could evaluate only a part of it, and the other parts would have undefined result.
For example, a single method argument would get evaluated, and then we could not continue with invoking the method, since the other arguments would not be computed.
For this reason, Linq Expressions do not support jumps to arbitrary locations, but the limitations are unclear.

WebAssembly is another expression-based system which handles control flow in a slightly different way.
It has an `if` instruction, which is very similar to our `ConditinalExpression`.
WebAssembly does not have `goto` and only supports an infinite loop (`loop` instruction) and break to a label (`br` and its shortcuts).
The lack of the `goto` instruction ensures structured control flow, which fits nicely into the concept of expression-based code.

See https://www.w3.org/TR/wasm-core-1/#control-instructions%E2%91%A8

WebAssembly references the target label by its index, which works well for a binary format, but would not be a pleasure to use it in an API while building the expression.
Apart from `BreakExpression`, we also introduce `BreakableExpression`, which is a target of the `break`.
It is going to be referenced by a unique ID.

```gql
type BreakableExpression {
    expression: Expression
    label: LabelTarget
}
```

We also allow the `BreakableExpression` to return a value.
This value will either come from the inner `expression` or must be passed as `value` to the `BreakExpression` that interrupts the execution:

```gql
type BreakExpression
    @format(t: "break {value} -> {target}") {
    value: Expression
    target: LabelTarget
}
```

> We do not have any explicit support for early return (i.e. return that is not at the end of a method body).
> Each method returns a single expression.
> However, this mechanism corresponds to early returns very closely and may be used to implement it.
> It is more generic than the C# `break` or `return`, but in some cases may emit code containing the `goto` statements.

## References

As of C# 7.0, we can work with references to variables and fields.
Having a field, we can:
* read the value of the field
* write a value into the field
* create a reference pointing to the field

When we have the reference to the field, we can both write it and read into the reference.
This means that we only have to support creating references to fields and variables instead of having three different expression types for read, write and reference.

We will only have a single `FieldAccessExpression` that returns a reference to the field.
Similarly, instead of a variable assignment, we will have a `VariableReferenceExpression` that creates a reference to a variable or a parameter.
To read a field, we need to combine the `FieldAccessExpression` with `DereferenceExpression`; to write it, we need to combine it with `ReferenceAssignExpression`.

> This keeps the core model simpler.
> However, for ease of use, we provide extension methods like `AssignField` that create the whole subexpression.

We may store the references in variables, pass them as arguments and return them from methods.
We cannot, however, store them in a field, as C# does not allow references inside of objects that will get stored on the heap instead of the stack.
That also means that reference variables can not be captured in a closure of a local function or a lambda function.

## ILSpy as a backend

C# is a complex language with many edge cases, so emitting code that corresponds to the exact symbols specified by the expression is very tricky.
To illustrate that, consider a few examples.
Let us assume that we want to call the `Uri.EscapeDataString` method.
In our expression tree, this will be represented by a `MethodCallExpression` pointing to the descriptor of this static method.
Under standard circumstances, we invoke the method by the code `Uri.EscapeDataString(argument)`.
However, that is not the case, if there is another symbol called `Uri` in scope - for example a property:

```csharp
public string Uri { get; }
public void M() {
    var x = Uri.EscapeDataString(Uri);
}
```

In such a case, we have to use a full name of the Uri type: `System.Uri.EscapeDataString(Url)`.
Using full names everywhere would, however, make all our code longer and, additionally, it does not actually solve our problem.
`System` corresponds to the namespace only until there is another symbol called `System`:

```csharp
public string Uri { get; }
public bool System { get; }
public void M() {
    var x = System.Uri.EscapeDataString(Uri);
}
```

We can be even more explicit and prefix the fully qualified name with `global::` to instruct the C# compiler that it should look for global symbols (i.e. namespaces).

There are many more similar cases - another example might be an implicit conversion.
We could think that we do not have to emit any code when we want to use an implicit conversion, as the compiler will perform it automatically.
However, this is not always the case, specifically due to method overloading.
Let us say we have a class with an implicit conversion to `double`:

```csharp
public class MyNumber {
    public static implicit operator double(MyNumber @this) => ...;
    public override string ToString() => ...;
}
```

Then, we use `Console.WriteLine(object)` to print a `MyNumber` instance to stdout (which will call the `ToString` method).
Conversion to `object`, the expected argument of `WriteLine` method is implicit, but if we do not write the conversion explicitly, the `Console.WriteLine(double)` overload will be called instead.
In practice, this could lead to reduced precision of the output or wrong output format;
most importantly, it is a violation of the promise that our library will always reference the exact symbols user has ordered.

Being explicit in every aspect is a way chosen by many code generators.
An example might the code generator for resource (`.resx`) files ([example file](https://github.com/dotnet/runtime/blob/3bb5f14/src/libraries/Common/tests/Resources/Strings.Designer.cs)) or DotVVM view compiler ([generator implementation](https://github.com/riganti/dotvvm/blob/61ee3fd/src/DotVVM.Framework/Compilation/DefaultViewCompilerCodeEmitter.cs#L702)).
However, the explicitness heavily afflicts readability of the produced code.
We think that that transparency and "debuggability" is a crucial advantage of code generation compared to the other approaches, so we would like to get the best of both worlds by having a smart code generator.

Implementing such a smart code generator would be very demanding, so we are not aware of anyone doing that in C# for code generation.
However, the [ILSpy project](https://github.com/icsharpcode/ILSpy) shares the same problem, and it already has a very reliable C# emitter.
ILSpy is a decompiler for .NET assemblies - a program that converts .NET intermediate language (IL) into C#.
IL has exact symbol references and no space for implicitness, similar to our expression tree.
The ILSpy authors take correctness and precision very seriously - in the end, we were only able to find two bugs, and one of them was already fixed in a newer version.

In the first step of the decompilation, ILSpy parses IL into an internal abstract tree - [the ILAst tree](https://github.com/icsharpcode/ILSpy/blob/faea7ee90d636fe8d2bc6a2f7f7b00dada9f01b2/doc/ILAst.txt).
After that, many transformations run on the tree and then it is translated into a C# syntax tree.
Few other transformation run on the C# syntax tree and then it is formatted into a text form.

To avoid too noisy output and too complicated implementation, we will translate our expression into ILSpy's internal ILAst tree and then let ILSpy produce the code.

## Metadata

Since we can not depend on System.Reflection, we need another to represent references to symbols.
We also need to allow users to define new symbols, so our model must account for that.

### ILSpy type system

ILSpy has its own type system built on top of [`System.Reflection.Metadata` library](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.metadata?view=netcore-3.1) (that is very different from standard Reflection).
`System.Reflection.Metadata` only allows reading .NET assemblies; it is not possible to define new symbols on the fly.
However, ILSpy type system is quite extensible, so it is possible to create new symbols.

We started with using the ILSpy type system directly in our expression trees, but it has several deficiencies.
In ILSpy, all the symbols are known in advance - there are not any new symbols emerging after the assemblies are loaded.
That is not the case in our project, where the entire point is to declare new symbols and create code from them.
Types in ILSpy system contain references to all other related symbols like declaring type and the members.
That is convenient when we inspect the types, but complicates everything when we create new types.
The references between symbols in ILSpy type system are cyclic, so we have to make some of the properties mutable to make the creation possible.
At some places, however, ILSpy assumes that symbols do not change and caches information, so we have to be quite careful about what and when can we mutate.

Having mutable types also complicates the design for us.
While the expression tree is created, we would like to validate basic constraints (if types fit, if the correct number of arguments was specified, etc.).
The validation however stops making sense, when some of the validated properties may change.
Having an immutable model of metadata would be very beneficial for this purpose.

Also, by using ILSpy type system, we would lock ourselves to using ILSpy as a backend.
At the time publishing of this work, there is no support for any other backend.
However, it could make sense to add support for Reflection.Emit and Linq.Expression backend.
That would provide a common API for generating code compile-time and code at runtime.

### Symbol signatures

We cannot simply copy the ILSpy system or Reflection and allow adding new symbols, since we want to have the metadata immutable.
Immutability will be a promise that whenever a function gets information about a symbol, it will not change.

We need to allow adding new symbols even when there are cyclic references (like recursive methods and recursive types).
This means that the information about a type can not contain its members, and the information about a method can not contain the body.
We will split the responsibilities - type or method **definition** will contain all information about its contents and a **signature**, that will only contain the most basic information.

To define a type definition, the user will need all members of the type already defined.
On the other hand, creating a signature is going to be trivial - they will just need to know the full name and basic information like parameters, accessibility, etc..
To reference a symbol from the expression (such as to call a method), only the signature will be needed.
So, symbols can be referenced before they are defined with all their contents.
Moreover, validating usage should be sound, since all properties of the type, method, field or property signature are going to be immutable.
Obviously, we will not be able to perform validation based on the contents of the type - for example, there is not going to be a way to check that a referenced field exists on the type.

Type signatures contain information about its full name, accessibility, kind of the type (interface, struct, class, ...), if it is abstract or sealed and the generic type parameters.
Type members contain a signature of the declaring type, name of the member and other basic information (arguments, result type, if it is static, virtual or abstract).

> For more details, see the documentation of `TypeSignature`, `MethodSignature`, `FieldSignature` and `PropertySignature` (TODO link)

### Symbol references

.NET has support for generics - types and methods may be parametrized by a type argument.
Since we want to support the concept, every time we will be referencing a symbol from the expression, we will provide a list of type arguments for that symbol.

It is important to make a distinction between a symbol signature and a symbol reference.
**Signature** is a generic version of the symbol - as it is declared in the code; the type parameters are unassigned.
**Reference** is a specialized version of the symbol - the type parameters have their arguments filled in.
When declaring symbols, we use the signatures - we only know the definition of the type parameter.
On the other hand, in code, we almost always need to know the type including its type argument.
It would not make any sense to create an instance of `List<?>` without knowing the type parameter, for example.

When the symbol is not generic, there are no generic arguments to be filled, so the reference becomes basically equivalent to a signature.
Because most types and methods are not generic, it is a very common situation, and we will have an implicit conversion from signature to reference.
The conversion will throw an exception if there would be missing type arguments, which is not considered a good practice in general.
However, it significantly reduces noises of the user code, so we find it helpful regardless.

> Note that the type arguments may still contain different generic parameters.
> When we are declaring a generic type or method, we will be using its generic parameters (instances of `GenericParameter`) in the symbol references.
> We can then use the generic parameters in the arguments of other types.
> The difference between a signature and a reference is similar to the difference between `typeof(List<>)` and `typeof(List<X>)` in .NET Reflection.
> In the second case (similar to reference), the List is not specialized by another parameter `X`, not its own parameter `T`.


## Functions and delegates

C# has support for functions declared inside of methods - either as lambda function or local functions. (TODO link docs)
The concept of delegates allows us to work with the function as with any object - i.e. store them in variables, fields, parameters and return them from methods.
Local function and lambda function can capture variables from the scope where they are declared.

Since local function is almost equivalent to a lambda assigned to a local variable, we have decided to simplify the concept and only support lambda function.
When the lambda function is immediately assigned to a variable, we will translate it into a C# local function, which arguably looks nicer.
However, to declare a function variable in C#, we would need a delegate matching the function signature.
Finding a matching delegate is usually not a problem, unless our function has `ref` parameters (TODO link) or very many arguments - we can usually use `Action<...>` or `Func<...>` delegates from the standard library.

The lack of a matching delegates would make it impossible to declare local functions taking parameters by reference.
That could be quite limiting.
However, since we own and design the type system, we could add a special function type - an inline delegate.
This is nothing new under the sun - most functional programming languages have a "function type".

There is one more problem with delegates that we can solve using our function types.
Delegates are basically special objects with an Invoke method matching the signature of the delegate, so our type signature is not going to contain the actual arguments and return type.
Not only we would not be able to validate the arguments of an invocation - we would not be able to determine result type of the invocation expression.
For all other expressions, we can do that, and most of the validation logic depends on the `Expression.Type()` method (see TODO link Type method).

Including the delegate arguments and return type in the type signature is not even possible - delegates can return themselves: `delegate A A()` is perfectly valid C# code.
Since our types are immutable, it would not be possible to construct such a delegate.
Not only it would not be possible to create such code; more importantly, we could not even load assemblies with such types into our type system.

```gql
type FunctionType {
    params: [MethodParameter]
    resultType: TypeReference
}
```

Our function type simply contains parameters and the return type.
We will only allow invocations when the target is an expression of FunctionType and FunctionExpression, will return the FunctionType.
To convert between delegates and function, we are going to provide FunctionConversionExpression.

> Apparently, it is not possible to return itself from the FunctionType as such type would have to return itself.
> However, we can still return a delegate from type function.
> Afterwards, the delegate can be freely converted into a function type, and invoked again.

> Note that FunctionConversionExpression can also convert between FunctionTypes, when the parameters and return types are compatible.
> For consistency, it can also convert between different delegate types, which is a bit problematic in C# itself, so it will result in a lambda function being emitted.

## MetadataContext



## External References

TODO:

## ILSpy Fallback
