# COBEREC Generator

Coberec is a C# code generator project. Actually, two projects in one:

* Coberec.GraphQL generates C# data classes from a model written in GraphQL Schema language.
* Coberec.ExprCS is a generic abstraction for generating C# code. It's an API built on ILSpy decompiler, that makes it easy to generate C# code safely. More on that below.

The tool is aiming at generating **COrrect REadable and BEautiful Code**, with priority on the correctness. I have not really fiddled with formatting and nice syntax, the goal is to produce code that is precise and which behavior is easy to predict.

The C# code generator is based on the awesome [ILSpy decompiler](https://github.com/icsharpcode/ilspy) which makes sure that it produces quite nice looking code that always represents what was intended. C# is a very complex language and it would be very hard to accomplish the goals without using ILSpy's backend.

## GraphQL Schema -> C# classes

COBEREC translates a domain model written in GraphQL Schema language into C# immutable classes, so you can declare your domain very easily without any boilerplate. As an intermediate representation, we use a simple representation of the schema, so you can also fairly easily consume it and create db schema, user interfaces or whatever yourself. Well, simply... if you don't care much about correctness in edge cases or if you already have a simple and robust backend (like ExprCS and ILSpy 😉)


### Schema

First, you need the schema. To define it, you can use the fundamental features of [GraphQL Schema](https://graphql.org/learn/schema/):

```graphql
# declared a type
type User {
    # non-nullable field id
    id: ID!
    firstName: String
    lastName: String
    photoUrl: Url
}

# declares custom scalar type that can be represented by a single string
scalar Url

# another type
type Robot {
    id: ID!
    name: String
}

# a type that is either a Robot or a User
union Creature = Robot | User

# you can also define interfaces for things with common properties
```

More on this subproject is on [a separate page](docs/graphql-gen.md)

## ExprCS

This is the cooler part (IMHO 🙂) of the project that is built on top of GraphQL codegen but is also a basis for it. I'd be sad if nobody would be using the API, so I'm using it myself on itself ;)

ExprCS is an abstraction for generating C# code using a semantic tree similar to `System.Linq.Expressions`. The tree format is declared in GraphQL and it's actually quite simple, see [src/Coberec.ExprCS/Schema](src/Coberec.ExprCS/Schema). Metadata declares type/method/property signatures, expression declares the actual code and composition declares types that glue all that together - method/type/property/field definitions - basically a signature + code.

### Semantic model of the code

When creating expressions, methods, and types, every symbol must have a known type. We are not building the resulting code in the terms of its syntax, but what it will do. For example, it doesn't care whether a `MethodCallExpression` will be the actual method call in C# or whether it's an invocation of a custom operator. Or, when you want to convert `int` to `long`, you just have to use the `NumericConversionExpression`, no matter that this conversion may be implicit in C#. The resulting code will be clean of it, the conversion will be implicit and the operator method call will be the operator usage.

The Expression API is very similar to `System.Linq.Expressions` that may also be used for generating code, but only for runtime-created functions. The major similarity is that everything we handle has a known type, every method call points to the exact overload. This makes it easy to catch errors early and to build very generic code generators (and helpers for them).

### Expressions

Another similarity to `System.Linq.Expressions` is that everything is an expression, there are no statements. This does not mean that the generated code will always be a single expression, it will get expanded into a reasonable C# form.

How can we run multiple methods after each other or declare variables in the expressions? This is no problem, the expression tree supports [inline blocks](./docs/csharp-features/blocks.md) and [variables](./docs/csharp-features/variables.md). The difference from using statements is that you can use the block/variable definition everywhere - in a method argument, inside a binary operator expression, ... And it will get sorted out automatically, later.

### Why?

Why do we need another tool for generating code? We can just concatenate strings or use Roslyn syntax tree when we want to be fancy, right? Being explicit about exact symbols might be a much more annoying than just copy-pasting a piece of code into a template, so what's the point?

To be clear, this approach does not fit all use cases very well. If I'd be in a need of a prime table in a C# array, I'd just write a shell script for that. Generating entire API clients or data classes from GraphQL is a different story, however. There is simply too many edge cases emerging from the user's possibilities and glitches of the C# language. Given enough time, every user will need something different, more config options will be added making the templates much more complicated... You can have a look at [NSwag's templates](https://github.com/RicoSuter/NSwag/blob/42d3b64/src/NSwag.CodeGeneration.CSharp/Templates/Client.Class.liquid) if you don't trust this :)

Expressions, which make the output code make everything composable and allow for much nicer code structure. There might an option to include null checks a method, which might be implemented as a conditional prepend of a piece of code to the method body. There is a few more edge cases with handling the nulls (like, the parameter might not be a nullable type), but these will be handled by a different function, independent on the callee. Or, you could even write a transform that would add parameter check into all public methods in the code.

As another example, you might want to handle different types that do a similar thing in the generated code. For example, the user might want to decide, if plain arrays, `List`, `ImmutableArray` or `ImmutableList` should be used in the API. The types have very similar API - for example, you can create all of them from `IEnumerable<T>`, but the way how it's done is different. Composition allows you to write helper functions that will be able to handle all cases based on the supplied type. Or, again, you could even have a postprocessing step that will replace array usage by `List<T>` usage :)

Since all types are known when the result is produced, the backend will handle many edge cases for you - are you using the correct type called `List`? Want to be sure to end up calling the right method overload? Wanted to use the reference `==` instead of an overloaded operator? But don't want to have code cluttered with explicit conversions and `global::System.Collection.Generic`? Thanks to ILSpy (mostly), this is handled for you.

### Names

One big problem is naming the generated symbols. When the names of types, methods, and properties come from the input (the GraphQL schema, for example), there is a sheer amount of edge cases that must be handled, since not every GraphQL identifier is a valid C# identifier (and this also depends on the context...)

For example, you might want to capitalize the first letter of the properties - convert to PascalCase, so it looks like idiomatic C# API. Since GraphQL is case sensitive, it allows you to have both properties `myProp` and `MyProp`, and this could get you into trouble with non-unique names.

GraphQL also allows almost any sequence of letters to be a identifier - specifically all C# keywords and problematic names like `ToString` or `GetHashCode`. This is a valid GraphQL type:

```csharp
type GetHashCode {
    getHashCode: String
    this: String
    equals: Boolean
    a: String
    get_A: String
}
```

All of these edge cases are handled by `Coberec.ExprCS` (the abstraction), not `Coberec.GraphQL`. The user of ExprCS gets these for free (well, symbol renaming must be enabled, since you don't want it in all cases).

Maybe you now think that this does not make sense to handle, these are artificial counterexamples... The reality, unfortunately, is that some of these are pretty easy to hit in larger. This is not in the GraphQL version, but github has "+1" and "-1" properties [in their v3 API](https://developer.github.com/v3/emojis/), which may break a lot of automated code generators (if they had that in a machine-readable form...). Glitches just add up quickly and fixing them manually in automated pipelines would be annoying.

### Metadata

Details are on a [separate page](src/metadata.gql), so just briefly. Metadata describes the types, methods, properties, fields, ...  - it's something like the System.Reflection, except that you can create your own symbols. The same classes are used for the types you create and for the types you use from referenced libraries, so there is not much distinction between.

One of the major pain points of automatic code generation is naming the symbol so that the names never collide and they are valid to use in C#. Coberec has a generic solution for most of these issues, [see Symbol Name Sanitization](./docs/symbol-name-sanitization.md)

## 

<!-- While in System.Reflection, you can create type out of nowhere using `typeof(X)` or `Type.GetType(...)`, this is not exactly possible here. The context is not implicit, we need to have a `MetadataContext` which contains the information about referenced libraries and also new types defined by you. -->
