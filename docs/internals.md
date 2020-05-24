# How does it work inside

The project has two parts - the expression-based API for generating C# code and a program that transforms GraphQL Schema into C# immutable classes. First, we'll have a look at the ExprCS API.

## ExprCS API

The model is declared in the GraphQL schema in three files:
* `metadata.gql` - contains the symbol signatures and references (like `MethodSignature` and `MethodReference`)
* `expression.gql` - contains declaration of the `Expression` type and all the expression types
* `composition.gql` - contains the symbol definitions - the types that compose metadata with expressions

The GraphQL definitions are then translated into partial C# classes.
Additional helper methods are declared on these classes (these are in the `src/Coberec.ExprCS/ModelExtensions` directory)

The API user declares the `TypeDef`s and registers them into `MetadataContext` using the `AddType` method.
At this point, not much is done, the type is only registered and are in a "Waiting for commit" stage.
When `CommitWaitingTypes` is invoked, the types and their content will be actually converted in ILSpy's internal representation.
It is at this point, that most of the tree will be type checked (that the referenced symbols actually exist and have the expected properties)

### Metadata translation

The reason why type definitions are translated and checked in bulk in the `CommitWaitingTypes` is that the types may be referencing each other.
If the the types were translated and registered one after another, the backward references would be invalid and the registration to fail.

The commit process performed by the `CommitWaitingTypes`  has these steps:
* Types are ordered topologically in order of inheritance. The base types are part of the core type definition, so all base types and implemented interfaces must be declared beforehand.
* Type names are assigned and the types declared without their members. See [Symbol Name Sanitization](./symbol-name-sanitization.md) section for the naming details.
* The type members are declared: Member declaration follows a similar pattern:
    - First, all the names are assigned.
    - Signatures are declared
    - After all signatures are declared, the method and property bodies are added. This makes sure that cyclic references between symbols work correctly.

The translation from ExprCS metadata classes into ILSpy's type system is handled by the `MetadataContext` class.

### Expression translation

The ExprCS expression are converted into ILSpy's internal abstract tree - [the ILAst tree](https://github.com/icsharpcode/ILSpy/blob/faea7ee90d636fe8d2bc6a2f7f7b00dada9f01b2/doc/ILAst.txt).
The transformation is performed by the `CodeTranslator` class.

The ILAst (`ILInstruction` nodes in code) is a tree made mainly of IL instructions.
It contains specific semantic information (types, methods and other symbols).
ILSpy then uses this tree for transformations on the code, before it's converted into C# syntax tree.
The semantics of ILAst are closer to IL than C#, but not in every aspect - for example, it contains node for `async`/`await` and for `yield return`, which are purely C#'s concepts.
The tree is not designed to be built very easily, as it performs almost no validation on creation.
Another major complication is that it does not have information about the result type of a given expression - this is simply because this information is unclear just from reading the IL.

The advantage of using ILAst for code generation is that the tree does not describe syntax at all.
This means that `using` statements, implicit/explicit conversions, choosing the right syntax to call a specific method overload are handled for us by ILSpy's logic.

The `CodeTranslator` recursively maps the ExprCS expressions into a `StatementBlock` block structure.
`StatementBlock` is basically a thin wrapper around the ILSpy's `ILInstruction` node and contains
* list of the nodes that are the effect of that expression - list of `ILInstruction`s or `Block`s
* result value - one `ILInstruction`, or nothing
* type of the result value, since `ILInstruction` does not contain that information

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

Other expressions are a bit more involved.
For example, control flow (conditions, loops and breaks) are represented by basic blocks and jumps in the ILAst.
The ILAst has a concept of `BlockContainer` which is an `ILInstruction` that contains a list of basic blocks that form a small control flow graph.
The blocks are connected by (conditional) branches and the entire `BlockContainer` is exited by a `Leave` instruction.
The `CodeTranslator` creates the block container for `ConditinalExpression`, `LoopExpression` and `BreakableExpression`.
Since the block container can not return a value while conditional and breakable expressions may do so, we introduce a helper variable that stores the result.
In simpler cases, this variable will get elided by ILSpy in following ILAst transformations.

Next tricky translation is the translation of anonymous functions.
When the function is assigned into a variable right ahead, we translate it into a local function.
Otherwise we translate it into a lambda.
Creating lambda or local function is quite similar in the ILAst model and it's handled by the `TranslateFunction` and `TranslateLocalFunction` methods.
The functions are basically translated as separate methods, while keeping the callee context, when it comes to references to local variables.
Lambdas also have to be assigned a valid .NET delegate, since they have a virtual function type in the ExprCS tree (see [Functions as Values](csharp-features/functions-as-values.md) page)


## GraphQL Schema generator

Second part of the project is the C# code generator from GraphQL Schema.
The program first parses the GraphQL schema using the [GraphqlParser](https://github.com/graphql-dotnet/parser/).
The GraphQL syntax tree is translated into a schema definition model called MetaSchema.
The MetaSchema is then translated into the ExprCS model by the `CsharpBackend` class and it's companions.
After that, C# code is emitted using the mechanism described above.

In older versions, this translator used RAW ILSpy API to generate the C# code and some parts (notably Equals and GetHashCode implementation) since create direct ILSpy's metadata and the ILAst.
Since ExprCS API is actually generated from the GraphQL Schema, that version was used to bootstrap the Coberec project.

### MetaSchema

The GraphQL is just a syntax to describe the schema of JSON-like data. The schema is transformed into a schema definition declared in project Coberec.MetaSchema (it's the schema of the schema). In the GraphQL schema language, the MetaSchema could look like this:

```GraphQL
"An annotation of a declaration."
type Directive {
    name: String
    "An object (aka string-value pair collection) of arguments of the annotations."
    args: AnyObject
}

"Represents a type that can be used in a field and so on"
type Type = ActualType | NullableType | ListType
"Represents a value that can be either `null` or of the `type`"
type NullableType {
    type: Type
}
type ListType {
    elementType: Type
}
"A reference to TypeSpec of the name"
type ActualType {
    name: String
}

"Specifies properties of a value type"
type TypeSpec {
    name: String
    directives: [Directive]
    core: TypeDefCore
}
type TypeDefCore = PrimitiveType | UnionType | Interface | CompositeType

"Custom primitive type"
type PrimitiveType {
}

"Represents a value that can be of one of the types."
type UnionType {
    options: [Type]
}

type TypeField implements NamedEntity {
    name: String @validateName
    description: String
    directives: [Directive]
}

type CompositeType {
    fields: [TypeField]
    implements: [TypeSpec]
}

type Interface {
    fields: [TypeField]
}

type DataSchema {
    entities: [Entity]
    types: [TypeSpec]
}

```

### GraphQL Loader

This is the front-end that lets the GraphqlParser parse the code into a syntax tree and then transforms the syntax tree into the schema. It can also transform validation error locations back into the source code locations.

### C# Backend

This part tells ILSpy how to generate the code. ILSpy is a .NET decompiler, not a code generation tools, so it's usage is a bit tricky, but it provides a decent service. The point is that they have very well-engineered logic that produces C# which does exactly what was in the .NET binary while the code looks pretty natural. Of course, we don't want to produce a .NET assembly and then decompile it back, this would bring a lot of other complications (with usage of higher-level features). However, the (almost) sweet spot is to cut the ILSpy decompiler approximately in the half and use it's internal representation of the code - the `ILAst` tree for code and `ITypeSystem` for metadata.

The Coberec.CSharpGen project contains an implementation of ITypeSystem related classes (nothing very groundbreaking about that) and code that produces the classes, properties and methods in the way we need. The coordinator of all this work is the `CSharpBackend` class which invokes the right helper methods that do the hard work. Then, there is a collection of helper functions that implement parts of the generated code. The most important of them (like `AddProperty`, `ImplementValidateIfNeeded`) create metadata members while linking to function that creates the proper ILAst implementation.

Very important component is the `SymbolNamer` which contains the logic that makes sure that all members are renamed to something that looks like C# (it's in PascalCase) and does not collide with other names at the same time. If you want to add something to the generated code, make sure to use it. The renaming is done by simple greedy algorithm, so the names of the early-added members have precedence over the ones added later.

All of this uses ILSpy's type system and IL AST. It uses quite it's default implementation to load referenced symbols and custom implementation of metadata in the `Coberec.CSharpGen.TypeSystem` namespace. In order to work with some external types, they have to be explicitly referenced and loaded as many decisions are made based on the used type properties (if they're a structs or classes, how the namespaces are used, ...).

After all the types and their members are created, ILSpy told to do decompilation of the just created typesystem. Only after this step is invoked, the actual function bodies are instantiated, so if you see an exception from this phase, it's probably not ILSpy bug :)

### CLI

This entire pipeline is invoked by the Coberec.CLI project. It does not do much beyond the naive argument parsing.

### Tests

Since one of main priorities of this project is to create code that is actually correct, tests are a crucial part of the codebase.
Most of the tests are what you'd expect from unit tests, some of them however use bit smarter tricks.
For the schema to C# translator, we rely on [FsCheck](https://github.com/fscheck/FsCheck) - a property based tester that generates a random schema definitions.
The random schemas are translated to C# and then we check that the emitted code compiles using the C# compiler.
Since these tests only check that the code compiles and there is no way to check that the emitted code would behave correctly, there are few tests checking that the code is the same as expected.
These tests are very likely to fail on every modification the generated code, so they should print a diff of what is changed and let us accept that change by staging the affected expected file (we can also view the diff in any git UI that you like).
See the [CheckTestOutput](https://github.com/exyi/CheckTestOutput) documentation for more details on the acceptance tests.
