# Implementation of the API

We have already described how the API was designed and briefly shown how it is used.
In this chapter, it remains to describe how we implement the translation to C#.

## ExprCS API

As we have mentioned in the previous chapter, the model is declared in the GraphQL Schema.
The schema is in these three files:

* `metadata.gql`: Contains the symbol signatures and references (like MethodSignature and MethodReference).
* `expression.gql`: Contains the declaration of the Expression type and all the expression subtypes.
* `composition.gql`: Contains the symbol definitions; the types that compose metadata with expressions.

The GraphQL files are translated into partial C# classes by the Coberec.CLI.
Additional helper methods are declared on these classes manually (these are in the src/​Coberec.​ExprCS/​Model​Extensions directory).

The user will create type definitions and registers them into the MetadataContext.
At this point, not much is done; the type is only registered and stays in a "Waiting for commit" stage until the user requests the output code.
When the code is requested, we invoke CommitWaitingTypes method.
At this point, the types and their content will be converted into ILSpy internal representation.
When the translation is performed, most of the tree will be type-checked -- checked that the referenced symbols exist and have the expected properties.

## Metadata Translation

The reason why type definitions are translated and checked in bulk in the CommitWaitingTypes method is that the types may be referencing each other.
If the types were translated and registered one after another, the backward references would be invalid and cause the registration to fail.

The commit process is performed by the CommitWaitingTypes method and has these steps:

* Types are ordered topologically in the order of inheritance.
  The base types are part of the core type definition, so all base types and implemented interfaces must be declared beforehand.
* Type names are assigned and the types are declared without their members.
  We have already described how [we rename symbols](./design.md#symbol-renaming).
* The type members are declared: Member declaration follows a similar pattern:
    - First, all the names are assigned.
    - Signatures are declared.
    - After all signatures are declared, the method and property bodies are added.
      This makes sure that cyclic references between symbols work correctly.

The translation from the metadata classes into ILSpy type system is handled by the MetadataContext class.

## Expression Translation

The Coberec expressions are converted into ILSpy internal abstract tree called [ILAst](https://github.com/icsharpcode/ILSpy/blob/master/doc/ILAst.txt).
The CodeTranslator class performs this transformation.

The ILAst (`ILInstruction` nodes in code) is a tree made mainly of IL instructions.
It is specific with semantic information (types, methods and other symbols).
ILSpy then uses this tree for transformations on the code, before it is converted into a C# syntax tree.
The semantics of ILAst are closer to IL than C#, but not in every aspect -- for example, it contains a node for `async`/`await` and for `yield return`, which are purely C# concepts.
The tree is not designed to be easy to build -- it performs almost no validation on construction, the API generally not very intuitive to use, and most mistakes result in a strange behaviour.
Another major complication is that it does not have information about the result type of a given expression -- this is because the type information is unclear only from reading the IL.

The advantage of using ILAst for code generation is that the tree does not describe the syntax at all.
That means that `using` statements, implicit/explicit conversions, choosing the right syntax to call a specific method overload are handled for us by ILSpy.

The CodeTranslator recursively maps the Coberec expressions into a StatementBlock block structure.
StatementBlock is a thin wrapper around the ILSpy ILInstruction node.
It contains:

* list of the nodes that are the effect of that expression -- list of ILInstruction or Block.
* result value -- one ILInstruction, or nothing
* type of the result value, since ILInstruction does not contain that information

Some translations are fairly obvious and simple; for example, the TranslateNot method translates the NotExpression:

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
For example, control flow (conditions, loops and breaks) is represented by basic blocks and jumps in the ILAst.
The ILAst has a concept of BlockContainer, which is an ILInstruction that contains a list of basic blocks that form a small control flow graph.
The blocks are connected by (conditional) branches and a Leave instruction is used to exit the entire BlockContainer.
The CodeTranslator creates a BlockContainer for ConditionalExpression, LoopExpression and BreakableExpression.
Since the block container can not return a value while conditional and breakable expressions may, we introduce a helper variable that stores the result.
In simpler cases, this variable will get elided by ILSpy in the following ILAst transformations.

The translation of anonymous functions is quite tricky.
When the function is immediately assigned into a variable, we translate it into a local function; otherwise, we translate it to a lambda function.
Creating lambda or local function is quite similar in the ILAst model, and it is handled by the TranslateFunction and TranslateLocalFunction methods.
The functions are translated as separate methods, we only keep the callee context when it comes to local variables.
Each lambda function also need a matching .NET delegate assigned.
As discussed in the [Functions and Delegates section](./design.md#functions-and-delegates), functions have a special FunctionType which does not have a direct .NET mapping.
If a FunctionConversionExpression immediately converts the function to a delegate, we use the delegate from there.
Otherwise we pick any matching delegate like `Func<...>` or `Action<...>`.

> Note that looking for a matching delegate might sometimes fail.
> Functions that take too many arguments or take them by reference might not have a matching delegate.
> In such case, the translation will fail, and the user will have to declare the delegate explicitly.

## API Usability

The expression and metadata API is made to be easy to use and debug.

When we raise errors early, it takes less effort to debug them.
We have implemented many checks that may raise errors when the expressions or signatures are constructed.
Most importantly, we always check that the expression types match.
We also check other invariants, such that an abstract type is not instantiated, etc.
Since the types are immutable, we can check the invariants in the constructor.
When the With method is invoked, we revalidate the new object.
Sometimes, it is essential to assign two properties in one With call not to break any invariants.
For example, changing a static method to an instance method in a MethodCallExpression must be done together with assigning null to the `target`.

However, there is a limit on what we can do during the initialization; we can only work with the information provided in the symbol signatures.
For example, we can not validate that a reference conversion is valid as we do not have enough information.
The type reference does not contain any information about base types, so we can not know if the cast is permitted.
However, we can still perform some checks on a best effort basis -- for example, a sealed type can not be converted into a different sealed type.

Often, the expression will be allowed by the validation in constructors, but the translation will fail later.
We did our best to throw a meaningful error message even in these cases.
The error message will usually contain the stringified expression that caused the problem.

All expressions and metadata types have a ToString method that produces a string representation.
Not only does it help with error messages, debuggers will invoke the ToString and show it as a variable tooltip.
Also, for printf-style debugging (or, WriteLine debugging in .NET), the ToString is very useful.
We try to keep the representation reasonably concise while being clear about types.
That is a compromise -- for example, constants are formatted as `{value}:{type}` (`100:int` for integers).
Often the details are not necessary, but since the string representation is mostly going to be used for debugging the problematic cases, it seems better to be explicit.

There is still a number of things that can be done to improve error messages and the string representation of the expressions.
We can already format the expressions into a structure where we can track which expression produced which part of the string.
That could allow us to implement a formatter that will highlight the problematic places based on errors from the translator.
Apart from fancy features, we should simply collect cases when the translation fails in ILSpy or on internal asserts and handle them correctly.

> We tried tracking the creation location of each expression using [C# caller information attributes](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information).
> It could provide the user with very valuable information for finding the root issue; however, it is quite problematic.
> Most of the expressions are created through helper methods, not the constructors generated from the GraphQL Schema.
> Either we would have to pass the caller information from every helper method, or we would only see information about the helper used.
> We could try to collect entire stack traces, but that would have a significant performance impact.

## Testing

The main goal of our project is to provide an abstraction that helps with the correctness of code generators.
To accomplish that, we certainly should not cause even more problems by bugs in our code.
No testing can catch all issues, and there is still work in testing that could be done.
However, we have a number of different tests; they at least ensure that we do not break what already works.

We have many tests where we generate a piece of code, and we manually accept that the result corresponds to the tested Expression.
For asserting that the code is as expected, we use the [CheckTestOutput](https://github.com/exyi/CheckTestOutput) library.
For each test, there is a file with the expected output.
If the code generator produces the same output as the contents of the file, the test passes.
Otherwise, it prints out a diff, and the test fails.
In the likely case that the output is different but still correct, we can simply stage the new output version in git which marks the new version as correct.

We have a custom model of metadata, and we offer a function to convert reflection symbols into our symbols.
Since there were several bugs in these helpers, we have implemented tests that check every symbol in the .NET standard library.
However, these tests are disabled by default as there were stability issues with different .NET framework versions, and the tests require significant time to run.

The GraphQL Schema transpiler is implemented on top of the Expression API, so this project is a complete end to end test of the library.
The generated code is sometimes quite complex, so most bugs we discovered now also have a corresponding simpler test which runs on the Expression API directly.
We also use the CheckTestOutput library for the GraphQL project, but we do not have as many tests here.
However, the simplicity of GraphQL allows us to generate random schemas using [FsCheck](https://github.com/fscheck/FsCheck).
Large random GraphQL types are good for uncovering edge cases in the symbol renaming logic and also problems with reference cycles.
To run into the problematic cases faster, we seed FsCheck with the [Big List of Naughty Strings](https://github.com/minimaxir/big-list-of-naughty-strings/).

> We do have a lot of small and isolated test cases, but we are still missing tests for more complex method bodies.
> The GraphQL Schema only generates quite simple methods, although there is a lot of them.

Next: [Conclusion](./conclusion.md)
