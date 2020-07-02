# Implementation of the API

We have already described how the API was designed and briefly shown how it is used.
In this chapter is remains to describe how we implement the translation to C#.
As we already mentioned, we use ILSpy to generate the C# code, so we will mostly cover how we use the ILAst API.

## ExprCS API

As we have mentioned in the previous chapter, the model is declared in the GraphQL Schema.
The schema is split into the three files:
* `metadata.gql` - contains the symbol signatures and references (like `MethodSignature` and `MethodReference`)
* `expression.gql` - contains declaration of the `Expression` type and all the expression types
* `composition.gql` - contains the symbol definitions; the types that compose metadata with expressions

The GraphQL definitions are translated into partial C# classes by the Coberec.CLI.
Additional helper methods are declared on these classes (these are in the `src/Coberec.ExprCS/ModelExtensions` directory)

The API user creates the type definitions and registers them into the MetadataContext.
At this point, not much is done, the type is only registered and stays in a "Waiting for commit" stage until the user requests the result.
When the code is requested, we invoke CommitWaitingTypes method.
At this point, the types and their content will be actually converted into ILSpy's internal representation.
When the translation is performed, most of the tree will be type checked - checked that the referenced symbols actually exist and have the expected properties.

## Metadata translation

The reason why type definitions are translated and checked in bulk in the `CommitWaitingTypes` is that the types may be referencing each other.
If the the types were translated and registered one after another, the backward references would be invalid and cause the registration to fail.

The commit process is performed by the `CommitWaitingTypes` and has these steps:
* Types are ordered topologically in the order of inheritance.
  The base types are part of the core type definition, so all base types and implemented interfaces must be declared beforehand.
* Type names are assigned and the types declared without their members.
  We have already described how we rename symbols (TODO link).
* The type members are declared: Member declaration follows a similar pattern:
    - First, all the names are assigned.
    - Signatures are declared
    - After all signatures are declared, the method and property bodies are added
      This makes sure that cyclic references between symbols work correctly.

The translation from ExprCS metadata classes into ILSpy type system is handled by the `MetadataContext` class.

## Expression translation

The ExprCS expression are converted into ILSpy internal abstract tree called [ILAst](https://github.com/icsharpcode/ILSpy/blob/faea7ee90d636fe8d2bc6a2f7f7b00dada9f01b2/doc/ILAst.txt).
This transformation is performed by the `CodeTranslator` class.

The ILAst (`ILInstruction` nodes in code) is a tree made mainly of IL instructions.
It is specific with semantic information (types, methods and other symbols).
ILSpy then uses this tree for transformations on the code, before it's converted into a C# syntax tree.
The semantics of ILAst are closer to IL than C#, but not in every aspect - for example, it contains node for `async`/`await` and for `yield return`, which are purely C# concepts.
The tree is not designed to be easy to build - it performs almost no validation on creation, it generally not very intuitive to use and most mistakes result in a strange behavior.
Another major complication is that it does not have information about the result type of a given expression - this is because the type information is unclear only from the IL.

The advantage of using ILAst for code generation is that the tree does not describe syntax at all.
That means that `using` statements, implicit/explicit conversions, choosing the right syntax to call a specific method overload are handled for us by ILSpy logic.

The CodeTranslator recursively maps the ExprCS expressions into a StatementBlock block structure.
StatementBlock is a thin wrapper around the ILSpy ILInstruction node.
It contains:
* list of the nodes that are the effect of that expression - list of ILInstruction or Block.
* result value - one ILInstruction, or nothing
* type of the result value, since ILInstruction does not contain that information

Some translations are fairly obvious and simple, for example, the `NotExpression` is translated by the following method:

```csharp
StatementBlock TranslateNot(NotExpression item)
{
    // translate the argument recursively
    StatementBlock b = this.TranslateExpression(item.Expr);
    // create ILAst negation node
    ILInstruction expr = Comp.LogicNot(b.Instr());
    return StatementBlock.Concat(
        // first, we perform the effects of the inner expression
        b,
        // we return the negated value
        StatementBlock.Expression(b.Type, expr)
    );
}
```

Other expressions are usually a bit more involved.
For example, control flow (conditions, loops and breaks) are represented by basic blocks and jumps in the ILAst.
The ILAst has a concept of BlockContainer which is an ILInstruction that contains a list of basic blocks that form a small control flow graph.
The blocks are connected by (conditional) branches and the entire BlockContainer is exited by a Leave instruction.
The CodeTranslator creates a BlockContainer for ConditionalExpression, LoopExpression and BreakableExpression.
Since the block container can not return a value while conditional and breakable expressions may, we introduce a helper variable that stores the result.
In simpler cases, this variable will get elided by ILSpy in following ILAst transformations.

The translation of anonymous functions is quite tricky.
When the function is immediately assigned into a variable, we translate it into a local function; otherwise we translate it into a lambda function.
Creating lambda or local function is quite similar in the ILAst model and it is handled by the TranslateFunction and TranslateLocalFunction methods.
The functions are basically translated as separate methods.
We only keep the callee context when it comes to references to local variables.
We also have to assign a valid .NET delegate to each lambda function.
As discussed in the Functions and Delegates chapter (TODO link), functions have a special FunctionType which does not have a direct .NET mapping.
If the function is immediately converted to a delegate through the FunctionConversionExpression, we use the delegate that the user has requested.
Otherwise we pick any matching delegate like the `Func<...>` or `Action<...>`.

> Note that looking for a matching delegate might sometimes fail.
> Functions that take too many arguments or take them by reference might not have a matching delegate.
> In such case, the translation will fail and the user will have to declare the delegate explicitly.

## API usability

We tried to make the expression and metadata API easy to use and debug.

It takes less effort to debug errors that are raised early.
We have implemented number of checks that may raise errors when the expressions or signatures are created.
Most importantly, we always check that the expression types correspond to the expectations.
We also check other invariants, such that an abstract type can not be instantiated, etc.
As the types are immutable, we can check the invariants in the constructor.
When `With` is invoked, we revalidate the new object.
Sometimes, it is important to assign two properties in one `With` call to not break any invariants.
For example, changing a static method to an instance method must be done together with assigning the `target`.

However, there is a limit on what we can do during the initialization; we can only count with the information provided in the symbol signatures.
For example, we can not validate that a reference conversion is valid as we do not have enough information.
The type reference does not contain any information about base type, so we can not know if the cast is permitted.
However, we can still perform some checks on a best effort basis - for example sealed class can not be converted into a different sealed class.

Often, the expression will be allowed by the validation in constructors but the translation will fail.
We did our best to throw meaningful error message even in these cases.
The error message will usually contain the stringified expression that caused the problem.

All expressions and metadata types have a ToString method that produces a meaningful string representation.
Not only does it help with error messages, debuggers will invoke the ToString and show it as a variable tooltip.
Also, for printf-style debugging (or, Console.WriteLine in .NET), implemented ToString is obviously useful.
We try to keep the representation reasonably concise while being clear about types which is obviously a compromise.
For example, constants are formatted as `{value}:{type}` - `100:int` for integers.
Often the details are not necessary, but since the string representation is mostly going to used for debugging the problematic cases, it seems better to be explicit.

There is still a number of things that can be done to improve error messages and string representation of the expressions.
We can already format the expressions into a structure where we can track which expression produced which part of the string.
That should allow us to implement a formatter that will highlight the problematic places based on errors from the translator.
Apart from fancy features, we should simply collect cases when the translation fails in ILSpy or on internal asserts and handle them correctly.

> We tried tracking the creation location of each expression using [C# caller information attributes](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information).
> It could provide the user with a very valuable information for finding the root issue, however it is quite problematic.
> Most of the expressions are created through helper methods, not the pre-generated constructors.
> Either we would have to pass the caller information from every helper method or we would just see information about the helper.
> We could try to collect entire stack traces, but that would have a significant performance impact.

## Testing

The main goal of our project is to provide an abstraction that helps with correctness of code generator.
To accomplish that, we certainly should not cause even more problems by bugs in our code.
No testing can catch all issues and there is still work that could be done in the testing.
However, we have a number of different tests that we use to ensure that we do not break what already works.

We have many tests where we generate a piece of code and manually accept that the result corresponds to the Expression.
For asserting that the code is as expected, we use the [CheckTestOutput](https://github.com/exyi/CheckTestOutput) library.
For each test, there is a file the expected output.
If the source generator produces the same output, the test passes.
Otherwise a diff is printed out and the test fail.
In the likely case that the output is different but still correct, we can simply stage the new output version in git.

We have a custom model of metadata and we offer function to convert Reflection symbols into our symbols.
Since there was a number of bugs in the logic we have implemented tests that check every symbol in the standard library.
However, they are disabled by default as there were stability issues with different .NET framework versions and the tests require significant time to run.

The GraphQL Schema transpiler is implemented on top of our API, so this project is a complete end to end test of the abstraction.
The generated code is sometimes quite complex, so most bugs we discovered there now also have corresponding simpler case testing the API directly.
We also use the CheckTestOutput library for this project, but we do not have as many of them.
However, the simplicity of GraphQL allows us to generate random schemas using [FsCheck](https://github.com/fscheck/FsCheck).
Large random GraphQL types are good for uncovering edge cases in the symbol renaming logic and problems with reference cycles.

> We do have a lot of small tests for isolated cases, but we are still missing tests for more complex method bodies.
> The GraphQL Schema only generates quite simple methods, although there is a lot of symbols.