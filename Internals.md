## How does it work inside

As you probably know, the program transforms GraphQL schema definition into C# immutable classes. Both ends of this process are handled by our dependencies - (GraphqlParser)[https://github.com/graphql-dotnet/parser/] and (ILSpy)[https://github.com/icsharpcode/ilspy]. It may seem that this repository contains only some duckape to make this two libraries collaborate :) Unfortunately, the ducktape forms a cluster of significant size and complexity, so it's got some structure to it.


### MetaSchema

The GraphQL is just a syntax to describe the data schema that is transformed into a schema definition declared in project Coberec.MetaSchema (it's the schema of the schema). In the GraphQL schema language, the metaschema could look like this:

```GraphQL
"An annotation of a declaration."
type Directive {
    name: String
    "An object (aka string-value pair collection) of arguments of the anotations."
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

This is the front-end that lets the GraphqlParser parse the code into a syntax tree and then transforms the syntax tree into the schema. It can also transform back error paths into the source code location.

### C# Backend & ILSpy

This part tells ILSpy how to generate the code. ILSpy is a .NET decompiler, not a code generation tools, so it's usage is a bit tricky, but it provides a decent service. The point is that they have very well-engineered logic that produces C# which does exactly what was in the .NET binary while the code looks pretty natural. Of course, we don't want to produce a .NET assembly and then decompile it back, this would bring a lot of other complications (with usage of higher-level features). However, the (almost) sweet spot is to cut the ILSpy decompiler approximately in the half and use it's internal representation of the code - the `ILAst` tree for code and `ITypeSystem` for metadata.

The Coberec.CSharpGen project contains an implementation of ITypeSystem related classes (nothing very groundbreaking about that) and code that produces the classes, properties and methods in the way we need. The coordinator of all this work is the `CSharpBackend` class which invokes the right helper methods that do the hard work. Then, there is a collection of helper functions that implement parts of the generated code. The most important of them (like `AddProperty`, `ImplementValidateIfNeeded`) create metadata members while linking to function that creates the proper ILAst implementation.

Very important component is the `SymbolNamer` which contains the logic that makes sure that all members are renamed to something that looks like C# (it's in PascalCase) and does not collide with other names at the same time. If you want to add something to the generated code, make sure to use it. The renaming is done by simple greedy algorithm, so the names of the early-added members have precedence over the ones added later.

All of this uses ILSpy's type system and IL AST. It uses quite it's default implementation to load referenced symbols and custom implementation of metadata in the `Coberec.CSharpGen.TypeSystem` namespace. In order to work with some external types, they have to be explicitly referenced and loaded as many decisions are made based on the used type properties (if they're a structs or classes, how the namespaces are used, ...).

After all the types and their members are created, ILSpy told to do decompilation of the just created typesystem. Only after this step is invoked, the actual function bodies are instantiated, so if you see an exception from this phase, it's probably not ILSpy bug :)

### CLI

This entire pipeline is invoked by the Coberec.CLI project. It does not do much more than that, except for some naive argument parsing.

### Tests

Since one of main priorities of this project is to create code that is actually correct, tests are a crucial part of it. Most of the tests are what you'd expect from unit tests, some of them however use bit smarter tricks. For the schema to C# translator, we rely on [FsCheck](https://github.com/fscheck/FsCheck) - a property based tester that generates some random schema definitions, they are translated to C# and then we check that the code compiles using Roslyn Compiler Platform. Since these tests only check that the code compiles and there is no way to check that the code "does the right things", there are few tests that the code is exactly the same as expected. These are very likely to yell at anyone who modifies the generated code, fortunately they should print a diff of what is changed and let you accept that change by staging the affected expected file (you can also view the diff in any git UI that you like).
