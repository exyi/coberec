# Design

In this chapter, we will go through the design decisions made during the project development and explore some alternatives.

## `System.Linq.Expressions` Influence

We consider the [Linq Expressions API](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression?view=netframework-4.7.2) a very reliable API for emitting the intermediate language at runtime.
We have designed our API to be similar and thus easy to use for developers who are already familiar with Linq Expressions.

In combination with Reflection Emit, it is possible to create a .NET assembly using this API.
However, we find Reflection Emit harder to use and it has several additional problems.
In particular, the API depends on reflection, so the referenced symbols of the output assembly must be loaded into the code generator.
These references may collide with dependencies of the generator itself and cause reliability issues.

We originally intended to reimplement the Linq Expression API for code generation.
However, to avoid the dependency on .NET reflection a new method of representing metadata [had to be created](./design.md#metadata).

## Expression Based Model

Major similarity to Linq Expressions is that every code fragment is an expression; there are no statements.
For example, we do not distinguish between `if` statement or `if` expression (the `a ? b : c` ternary operator in C#), there is only a single ConditionalExpression.
The Coberec library will expand the expressions into a sensible C# form; the generated code will not be necessarily a single expression.

C# distinguishes between expressions and statements, expressions always return a value (i.e. cannot return `void`) while statements do not.
To simplify the concept, we represent every code fragment as a generalized expression.
Our expressions do not have to return anything -- we allow returning `void` and we will allow variable declarations and inline blocks in the expressions.
From that perspective, our code model is similar to F#, Scala or other expression-based languages.

## Blocks and Variables

We represent blocks and variable definitions a bit differently than Linq Expressions do, to more closely follow the [single responsibility principle](https://en.wikipedia.org/wiki/Single-responsibility_principle) and thus make the types simpler.
Linq Expressions use a single node type ([BlockExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.blockexpression?view=netcore-3.1)) to declare variables and chain multiple expressions.
We have separated the two responsibilities into BlockExpression and LetInExpression.

The block expression executes a list of expressions ignoring their results and then returns the `result`.
The `result` may be `default(void)` expression, if we do not want to return anything:

```gql
type BlockExpression {
    expressions: [Expression]
    result: Expression
}
```

The name of the "variable declaration" is inspired by the `let variable = value in target` syntax from F#, since the expression has the same semantics.
First, the variable is initialized by the `value` and then `target` expression is evaluated with the new variable in scope.

```gql
type LetInExpression {
    variable: ParameterExpression
    value: Expression
    target: Expression
}
```

## Control Flow

Same as in Linq Expressions, we have a ConditionalExpression representing an `if` statement or the `a ? b : c` ternary operator.
Other control flow statements (loops and jumps) are a bit more different to C# in our expression-based model.

Linq Expressions support jumps with `goto` and labels.
The behaviour is a bit unintuitive, however.
Jumping into the middle of an expression does not make sense since that could evaluate only a part of the expression, and the other parts would have an undefined result.
For example, we would jump to an expression in a method call.
The single method argument would get evaluated, but the method can not be invoked, since the other arguments would not be computed.
For this reason, Linq Expressions do not support jumps to arbitrary locations, but the limitations are hard to understand.

WebAssembly is another expression-based system which handles control flow in a slightly different way.
It has an [`if` instruction](https://www.w3.org/TR/wasm-core-1/#control-instructions%E2%91%A8), which is very similar to our ConditionalExpression.
WebAssembly does not have a generic `goto` instruction and only supports an infinite loop (`loop` instruction) and break to a label (`br` and its shortcuts).
The lack of the `goto` instruction ensures structured control flow, which fits nicely into the concept of expression-based code.


WebAssembly `br` instruction links to the target by its index.
That works well for a binary format, but would not be a pleasure to use it in an API while building the expression.
Apart from BreakExpression, we also introduce BreakableExpression, which is a possible target of the `break`.
It is referenced by a unique ID (inside of the LabelTarget type).

```gql
type BreakableExpression {
    expression: Expression
    label: LabelTarget
}
```

We also allow the BreakableExpression to return a value.
This value either comes from the inner `expression` or must be passed as `value` to the BreakExpression that interrupts the execution:

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
> It is more generic than the C# `break` or `return`, so, in some cases, it may emit code containing `goto` statements.

## References

As of version 7.0, C# allows working with references to variables and fields.
Having a field, it is possible:

* to read the value of the field;
* to write a value into the field;
* to create a reference pointing to the field,

Having a reference to a field, it is possible to both write to and read from the reference.
This means that it is only necessary to support creation of references to fields and variables instead of having three different expression types for read, write and reference.

We will only have a single FieldAccessExpression that returns a reference to the field.
Similarly, instead of a variable assignment, we will have a VariableReferenceExpression that creates a reference to a variable or a parameter.

To read a value from a field, we need to combine the FieldAccessExpression with DereferenceExpression.
To write a value into a field, we need to combine it with ReferenceAssignExpression.

> This keeps the core model simpler.
> However, for ease of use, we provide extension methods like `AssignField` that create the whole subexpression.

It is possible to store the references in variables, pass them as arguments and return them from methods.
However it is not allowed to store them in a field -- C# does not allow references inside of objects; references can not be stored on the garbage collected heap.
That also means that reference variables can not be captured in a closure of a local function nor a lambda function.

## ILSpy as a Backend

We use [ILSpy](https://github.com/icsharpcode/ILSpy) as a code generation backend.
ILSpy is a C# decompiler witch can serve as a reliable abstraction of several edge cases of the C# language.
C# is a complex language with many edge cases, so emitting code that corresponds to the exact symbols specified by the expression is very tricky.

To illustrate that, consider a few examples.
Let us assume that we want to call the `Uri.EscapeDataString` method.
In our expression tree, it is represented by a MethodCallExpression pointing to the descriptor of the static method.
Normally, the method is invoked by the code `Uri.EscapeDataString(argument)`.
However, that is not the case, if there is another symbol called `Uri` in scope -- for example a property:

```csharp
public string Uri { get; }
public void M() {
    var x = Uri.EscapeDataString(Uri);
}
```

That code would not compile as C# would try to invoke the method on the property `Uri`.
To resolve the issue, we would have to use the full name:

```csharp
public string Uri { get; }
public void M() {
    var x = System.Uri.EscapeDataString(Uri);
}
```

However, using full names everywhere would make all generated code longer.
Additionally, it does not actually solve our problem.
`System` corresponds to the namespace only until there is another symbol called `System`:

```csharp
public string Uri { get; }
public bool System { get; }
public void M() {
    var x = System.Uri.EscapeDataString(Uri);
}
```

We can be even more explicit and prefix the fully qualified name with `global::` to instruct the C# compiler that it should look for global symbols (i.e. namespaces).

There are many more similar cases -- another example might be an implicit conversion.
One could think that no code has to be emitted when we want to use an implicit conversion, as the compiler will perform it automatically.
However, this is not always the case, specifically due to method overloading.
Let us say we have a class with an implicit conversion to `double`:

```csharp
public class MyNumber {
    public static implicit operator double(MyNumber @this) => ...;
    public override string ToString() => ...;
}
```

Then, we use `Console.WriteLine(object)` to print a `MyNumber` instance to stdout (which will call the `ToString` method).
Conversion to `object`, the expected argument of `WriteLine` method is implicit, but without an explicit conversion, the `Console.WriteLine(double)` overload will be called instead.
In practice, this could lead to reduced precision of the output or wrong output format;
most importantly, it is a violation of the promise that Coberec always references the exact symbols user has specified in the tree.

Being explicit in every aspect is a way chosen by many code generators.
An example might be the code generator for resource (`.resx`) files (see an [example file](https://github.com/dotnet/runtime/blob/3bb5f14/src/libraries/Common/tests/Resources/Strings.Designer.cs) or DotVVM view compiler ([generator implementation](https://github.com/riganti/dotvvm/blob/61ee3fd/src/DotVVM.Framework/Compilation/DefaultViewCompilerCodeEmitter.cs#L702)).
However, the explicitness heavily afflicts readability of the produced code.
We think that that transparency and "debuggability" is a crucial advantage of code generation compared to the other approaches, so we would like to get the best of both worlds by having a smart code generator.

Implementing such a smart code generator would be very demanding, so we are not aware of anyone doing that in C# for code generation.
However, the ILSpy project shares the same problem, and it already has a very reliable C# emitter.
ILSpy is a decompiler for .NET assemblies -- a program that converts .NET intermediate language (IL) into C#.
IL has exact symbol references and no space for implicitness, similar to our expression tree.
The ILSpy authors take correctness and precision very seriously -- in the end, we were only able to find two bugs, and one of them was already fixed in a newer version.

In the first step of the decompilation, ILSpy parses IL into an internal abstract tree -- [the ILAst tree](https://github.com/icsharpcode/ILSpy/blob/master/doc/ILAst.txt).
After that, many transformations run on the tree and then it is translated into a C# syntax tree.
Few other transformation run on the C# syntax tree and then it is formatted into a text form.

To avoid too noisy output and too complicated implementation, we will translate our expression into ILSpy's internal ILAst structure and then let ILSpy produce the code.

## Metadata

Since we can not depend on the .NET reflection, we need another way to represent references to [symbols](https://en.wikipedia.org/wiki/Symbol_%28programming%29) -- types, methods, properties, etc.
We also need to allow users to define new symbols, so our model must account for that.

### ILSpy Type System

ILSpy has its own type system built on top of [System.Reflection.Metadata library](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.metadata?view=netcore-3.1).
System.Reflection.Metadata only allows reading .NET assemblies; it is not possible to define new symbols on the fly.
However, ILSpy type system is quite extensible, so it is possible to create new symbols.

> Note that System.Reflection.Metadata is very different to the standard reflection in .NET framework.
> It does not use the internal .NET mechanisms to load types, it has its own assembly reading implementation in C#.
> Loading types using this library does not have the problem with version collisions, like loading it with reflection has.

We started with using the ILSpy type system directly in our expression trees, but it has several deficiencies.
In ILSpy, all the symbols are known in advance -- there are not any new symbols emerging after the assemblies are loaded.
That is not the case in our project, where the entire point is to declare new symbols and then create code from them.
Types in ILSpy system contain references to all other related symbols like the type members and the declaring type.
That is convenient when we inspect the types, but complicates the construction of new types.
The references between symbols in ILSpy type system are cyclic, so we have to make some of the properties mutable to make the creation possible.
At some places, however, ILSpy assumes that symbols do not change to cache information, so we have to be quite careful about what and when we can mutate.

Having mutable types also complicates the design for us.
While the expression tree is created, we would like to validate basic constraints (if types fit, if the correct number of arguments was specified, etc.).
The validation however stops making sense, when some of the validated properties may change.
Having an immutable model of metadata would be very beneficial for this purpose.

Also, by using ILSpy type system, we would lock ourselves to using ILSpy as a backend.
At the time publishing of this work, there is no support for any other.
However, it could make sense to add support for Reflection Emit and Linq Expression backend.
That would provide a common API for generating code compile-time and code at runtime.

### Symbol Signatures

We cannot simply copy the ILSpy system or reflection and allow adding new symbols, since we want to have the metadata immutable.
Immutability is a promise that whenever a function gets information about a symbol, it will not change.

We need to allow adding new symbols even when there are cyclic references (like recursive methods and recursive types).
This means that the information about a type can not contain its members, and the information about a method can not contain the body.
We have split the responsibilities -- type or method **definition** contains contain all information about its contents and a **signature**, that only contains the most basic information.

To define a type definition, the user will need all members of the type already defined.
On the other hand, creating a signature is simple -- they will just need to know the full name and basic information like parameters, accessibility, etc..
To reference a symbol from the expression (such as to call a method), only the signature is needed.
So, symbols can be referenced before they are defined with all of their contents.
Moreover, validating usage should be sound, since all properties of the type, method, field or property signature are immutable.
Obviously, is not possible to perform validation based on the contents of the type -- for example, there is not a way to check that a referenced field exists on the type.

Type signatures contain information about its full name, accessibility, kind of the type (interface, struct, class, ...), if it is abstract or sealed and the generic type parameters.
Type members contain a signature of the declaring type, name of the member and other basic information (arguments, result type, if it is static, virtual or abstract).

> For more details, see the definition of [TypeSignature](https://exyi.cz/coberec_doxygen/de/d05/classCoberec_1_1ExprCS_1_1TypeSignature.html), [MethodSignature](https://exyi.cz/coberec_doxygen/d7/d36/classCoberec_1_1ExprCS_1_1MethodSignature.html), [FieldSignature](https://exyi.cz/coberec_doxygen/d2/d18/classCoberec_1_1ExprCS_1_1FieldSignature.html) and [PropertySignature](https://exyi.cz/coberec_doxygen/d8/dd1/classCoberec_1_1ExprCS_1_1PropertySignature.html).

### Symbol References

.NET has support for generics -- types and methods may be parametrized by a type argument.
Since we want to support the concept, every time we will be referencing a symbol from the expression, we will provide a list of type arguments for that symbol.

It is important to make a distinction between a symbol signature and a symbol reference.
**Signature** is a generic version of the symbol -- as it is declared in the code; the type parameters are unassigned.
**Reference** is a specialized version of the symbol -- the type parameters have their arguments filled in.
When declaring symbols, we use the signatures -- we only know the definition of the type parameter.
On the other hand, in code, we almost always need to know the type including its type argument.
It would not make any sense to create an instance of `List<?>` without knowing the type parameter, for example.

When the symbol is not generic, there are no generic arguments to be filled, so the reference becomes basically equivalent to a signature.
Because in practice, most types and methods are not generic, and we will have an implicit conversion from signature to reference.
The conversion will throw an exception if there would be missing type arguments, which is not considered a good practice in general.
However, it significantly reduces noises of the user code, so we find it helpful regardless.

> Note that a type parameter may end up filled by another type parameter.
> When we are declaring a generic type or method, we will be using its generic parameters (instances of GenericParameter) in the symbol references.
> We can then use the generic parameters in the arguments of other types.
> The difference between a signature and a reference is similar to the difference between `typeof(List<>)` and `typeof(List<TParam>)` in .NET reflection.
> In the second case (similar to reference), the List is not specialized by another parameter `TParam`, not its own parameter `T`.


## Functions and Delegates

C# has support for functions declared inside of methods -- either as [lambda functions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions) or [local functions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/local-functions).
The concept of delegates allows us to work with a function as with any object -- i.e. store them in variables, fields, parameters and return them from methods.
Both local function and lambda function can capture variables from the scope where they are declared.

Since local function is almost equivalent to a lambda assigned to a local variable, we have decided to simplify the concept and only support lambda function.
When the lambda function is immediately assigned to a variable, Coberec will translate it into a C# local function, which arguably looks nicer.
However, to declare a function variable in C#, we would need a delegate matching the function signature.
Finding a matching delegate is usually not a problem -- unless our function has `ref` parameters or very many arguments, we can usually use `Action<...>` or `Func<...>` delegates from the standard library.

The lack of a matching delegates would make it impossible to declare local functions taking parameters by reference.
That could be quite limiting.
However, since we own and design the type system, we could add a special function type -- an inline delegate.
This is nothing new under the sun -- most functional programming languages have a "function type".

There is one more problem with delegates that we can solve using our function types.
Delegates are basically special objects with an Invoke method matching the signature of the delegate, so our type signature is not going to contain the actual arguments and return type.
Not only we would not be able to validate the arguments of an invocation -- we would not be able to determine result type of the invocation expression.
For all other expressions, we can do that, and most of the validation logic depends on the [`Expression.Type()` method](https://exyi.cz/coberec_doxygen/da/d1b/classCoberec_1_1ExprCS_1_1Expression.html#aa3966c00e7b5358f7ca116c23a3f0e98).

Including the delegate arguments and return type in the type signature is not even possible -- delegates that return themselves (`delegate A A()`) are perfectly valid in C#.
Since our types are immutable, it would not be possible to construct such a delegate.
More importantly, we could not even load assemblies with such types into our type system.

```gql
type FunctionType {
    params: [MethodParameter]
    resultType: TypeReference
}
```

Our function type simply contains parameters and the return type.
We only allow invocations when the target is an expression of FunctionType.
Every FunctionExpression returns the FunctionType and to convert between delegates and function, we are providing FunctionConversionExpression.

> Apparently, it is not possible to return itself from the FunctionType as such type would have to return itself.
> However, we can still return a delegate from the function.
> Afterwards, the delegate can be freely converted into a function type, and invoked again.

> Note that FunctionConversionExpression can also convert between FunctionTypes, when the parameters and return types are compatible.
> For consistency, it can also convert between different delegate types, which is a bit problematic in C# itself, so it will result in a lambda function being emitted.

## Metadata Context

Using the Expression class, we can define the code that we can put into metadata definitions.
Then, we need to collect the type definitions, connect them with references libraries and emit the C# code.
We will introduce a MetadataContext class that will have the following responsibilities:

1. Load symbols from the referenced assemblies.
2. Allow the user to add new types.
3. Allow the user to explore the registered and referenced symbols.
4. Emit the added types into C# code -- either a single string or multiple files.

Since the Coberec API is build on top of ILSpy, MetadataContext is a wrapper of the ILSpy ICompilation object, which has a similar role of holding all the type information together.

In principle, the API workflow looks like:

* Create MetadataContext:
  ```csharp
  context = MetadataContext.Create(
      references: listOfReferencedAssemblies);
  ```
* Explore referenced symbols:
  ```csharp
  type = context.GetNamespaceMembers(namespace).First();
  members = context.GetMembers(type);
  ```
* Add new type definitions:
  ```csharp
  context.AddType(typeDef)
  ```
* Build the result -- generate the C# code:
  ```csharp
  var output = context.EmitToString();
  context.EmitToDirectory(outputDirectory);
  ```

Likely, significant parts of the code working with the MetadataContext are only going to use it only for symbol exploration and not to add new types.
So, it could make sense to split the context into a type that only allows exploration and type that also allows adding new types.
However, listing types depends on the AddType method, as it also lists the added types.
Having a read-only context could create a false sense of immutability, and we would have multiple objects that are interconnected by mutations.
We choose against this division of responsibilities; this way, it is simpler and will hopefully be less confusing.

## Symbol Renaming

We have already discussed that naming of the generated symbols is a significant problem for code generators.
Most existing code generators handle it somehow heuristically, and it is quite easy to come up with an input that forces it to produce invalid code.
It is a topic for a different discussion on how big of a problem this is -- it depends strongly on the specific use-case.
However, our abstraction mostly solves this problem once for all.

We let the API user register symbols with any names -- even those completely invalid in C#.
When we get to the point of generating C# code, we have to transform the registered metadata into ILSpy type system.
It is in this step that we do the symbol naming.

First, we sanitize each name of invalid characters.
Optionally, we transform the casing into the C# recommended PascalCase.
Then, we assign names to types, one by one.
We can not name a type the same as another type in the namespace or any of its members.
From principle, we refuse to name types as .NET special methods like `Finalize`, `GetHashCode`, etc.
Even when the type does not contain the method, it would be impossible to add it later.

When naming members, we try to prioritize them based on the damage done by giving them a different name.
The absolute priority is a overridden method or property -- these must have the same name as they have in the base type.
Fortunately, C# does not support multiple class inheritance, so we can not have a name collision.
Interface implementations are not as critical; we can rename the public method and add an [explicit interface implementation](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/interfaces/explicit-interface-implementation).
Then we try to prioritize public members over private or internal ones.

C# supports method overloading, so we try not to rename methods.
However, when there is a method collision with a higher priority property or when we have two methods with the same parameters, we will do the rename regardless.

> Note that we do not rename method unless they have exactly the same parameters.
> This may still create problems when relying on implicit conversions.
> Hopefully, this problem is quite rare and will not lead to many problems.

Because all references to the renamed symbols are symbolic, and nothing in the expression is referenced by name, we rename all usages automatically.

## External References

The ExprCS code emitter must know everything about the symbols that are used in the code.
If we want to use external libraries, we must explicitly include them in the project.
When creating a new MetadataContext, we can use the `references` parameter with a list of paths to referenced libraries.

Sometimes it is useful to generate code into an existing project that already contains some code.
It is not possible to add an assembly reference to the same project we are just building -- for that case, we need an External Symbol API.
That will allow us to declare the types we expect to be in the project and register them in the MetadataContext.

Since we already have the broad API for defining types, the simplest option is to reuse it.
We have added an `isExternal` parameter to the MetadataContext.AddType method.
When set to true, the added type will not be included in the output, but the emitter will know that it exists.

> The API requires us to specify even the bodies of the declared methods, the same as it does for standard definitions.
> It does not make much sense, but it is easier this way.
> Providing an empty method body is very easy for the user -- create a DefaultExpression of the result type.

This simple addition breaks the fundamental barrier that we could not call the handwritten code from the generated code.
However, unlike in macro or reflection-based approaches, this connection is still very far from easy-to-use.
We can not inspect the handwritten code, and we even have to redeclare it.

It may be quite a significant issue of code generation, and resolving it could be a direction for future extension of the project.
One idea would be to parse the existing C# code to extract the declared symbols from it.
However, that could be a significant performance cost.
With version 9, C# should get the Source Generators feature, as we discussed [in the previous chapter](./approaches.md#c-9-source-generators).
The Source Generators run in the C# compiler, so we would use the metadata from the compilation.

## ILSpy Fallback

C# has a rich set of language features, and it is not possible to express everything in our simplified expression model.
We hope that we will be able to fill in the essential missing bits, but it will probably never be complete.
For this reason, we provide a simple fallback API that allows users to build the ILAst directly or do any post-processing to the ILSpy type system entities.

We have introduced a RegisterTypeMod method on the MetadataContext that registers a function to a specified type signature.
The function will run on the created type object -- a VirtualType instance.
The function is allowed to do anything -- add new symbols, modify the existing ones, remove or hide symbols, etc.
However, there is the risk of breaking the automatic symbol renaming by adding more symbols that were not expected.
It is thus recommended to add dummy symbols and then replace them.

Another option is to use a special ILSpyMethodBody expression as a method body.
It contains a function that returns an ILFunction -- an ILAst node that represents the whole function.
This option is lighter compared to the RegisterTypeMod and also less risky in terms of symbol renaming.


Next: [API Overview](./API-overview.md)
