# COBEREC Generator

Coberec is a C# code generator project. Actually, two projects in one:

* **Coberec.GraphQL** generates C# data classes from a model written in GraphQL Schema language.
* **Coberec.ExprCS** is a generic abstraction for generating C# code. It is an API built on ILSpy decompiler that makes it easy to generate C# code safely. More on that below.

**TL;DR**: It is like `System.Linq.Expressions` for producing code in the text form, instead of a runtime delegate. See the [example below](#example)

The tool is aiming at generating **COrrect REadable and BEautiful Code**, with priority on the correctness. We have not fiddled with formatting and nice syntax. The goal is to produce code that is precise and which behaviour is easy to predict.

The C# code generator is based on the awesome [ILSpy decompiler](https://github.com/icsharpcode/ilspy), which makes sure that it produces quite nice looking code that always represents what was intended. C# is a very complex language, and it would be tough to accomplish the goals without using ILSpy's backend.

## Table of contents



## GraphQL Schema -> C# classes

COBEREC translates a domain model written in GraphQL Schema language into C# immutable classes so that we can declare the domain model very easily without any boilerplate. As an intermediate representation, we use a simple representation of the schema, which allows us to consume it in a different process and create a database schema, user interfaces or anything. Well, simply... if you don't care much about correctness in edge cases or if you already have a simple and robust backend (like ExprCS and ILSpy 😉)


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

# we can also define interfaces for types with common properties

interface WithId {
    id: ID!
}
```

More on this subproject is on [a separate page](docs/graphql-gen.md)

## ExprCS

ExprCS is the cooler part of the project (IMHO 🙂). ExprCS is built on top of the GraphQL code generator, but it is also a basis for it. I would be sad if nobody would be using the API, so I'm using it the project on itself ;)

ExprCS is an abstraction for generating C# code using a semantic tree similar to `System.Linq.Expressions`. The tree format is declared in GraphQL, and it is actually quite simple, see [src/Coberec.ExprCS/Schema](src/Coberec.ExprCS/Schema). **Metadata** declares type/method/property signatures, **expression** declares the actual code and **composition** declares types that glue all that together - method/type/property/field definitions - in principle the signature + the code.

### Semantic model of the code

When creating expressions, methods, and types, every symbol must have a known type. We are not building the resulting code in terms of its syntax, but rather in terms of the semantics. For example, it does not matter whether a `MethodCallExpression` will be the actual method call in C# or whether it is an invocation of a custom operator. Alternatively, when we want to convert `int` to `long`, we just have to use the `NumericConversionExpression`, no matter if this conversion will be implicit or explicit in C#. The resulting code will be most likely clean of the explicit conversion, and the operator method call will be the operator.

The Expression API is very similar to `System.Linq.Expressions` that may also be used for generating code, but only at runtime. When we call the `Compile` method on `Expression<TDelegate>`, IL is emitted and then compiled by JIT, when we invoke the delegate. In ExprCS we generate C# code, and we also can declare types, methods and fields. The major similarity is that everything we handle has a known type; every method call points to the exact method overload; every identifier is known to be a variable, parameter, property or a field, and so on. This makes it easy to catch errors early and to build very generic code generators (and helpers for them).

### Expressions

Another similarity to `System.Linq.Expressions` is that everything is an expression; there are no statements. That does not mean that the generated code will always be a single expression; the expressions will get expanded into a reasonable C# form. When everything is an expression, writing generic helpers gets less complicated, as we do not have to worry if the arguments are going to expressions or statements. For example, we do not distinguish between `if` statement or `if` expression (the `a ? b : c` ternary operator), there is a single `ConditionalExpression`.

How can we run multiple methods after each other or declare variables in the expressions? This is no problem, the expression tree supports [blocks](./docs/csharp-features/blocks.md) and [variables](./docs/csharp-features/variables.md). The difference from using statements is that we can use the block/variable definition everywhere - in a method argument, inside a binary operator expression, and so on. Later, it will get sorted out automatically.

### Why?

Why do we need another tool for generating code? We can just concatenate strings or use Roslyn syntax tree when we want to be fancy, right? Being explicit about exact symbols might be a much more work than just copy-pasting a piece of code into a template, so what is the point?

Our approach does not fit all use cases very well. If we'd would be in need of a prime table in a C# array, we would simply write a simple script for generating that. Generating entire API clients or data classes from GraphQL is a different story, however. There are too many edge cases emerging from the user's possibilities and glitches of the C# language. Given enough time, every user will need something different, and more config options will be added, making the templates much more complicated. Please have a look at [NSwag's templates](https://github.com/RicoSuter/NSwag/blob/42d3b64/src/NSwag.CodeGeneration.CSharp/Templates/Client.Class.liquid) to see our point.

The expressions make the logic more composable and allow for a nicer code structure. Instead of checking a configuration flag on many places in the template, we can rather conditionally apply a transformation to a method or entire type.

Since all types are known when we are producing the code, the backend will handle many edge cases for us: Are we using the correct type called `List`? Will it end up calling the right method overload? Are we using the reference `==` instead of an overloaded operator? While not having code cluttered with explicit conversions and fully qualified names like `global::System.Collection.Generic`? Thanks to ILSpy (mostly), this is handled automatically.

### Names

One big problem is naming the generated symbols. When the names of types, methods, and properties come from the input (the GraphQL schema or OpenAPI spec, for example), we must handle a sheer amount of edge cases, since not every GraphQL identifier is a valid C# identifier, and it also depends on the context.

For example, we might want to capitalize the first letter of the properties - convert to PascalCase, so it looks like idiomatic C# API. Since GraphQL is case sensitive, it allows both properties `myProp` and `MyProp` in one type, and this could get us into trouble with non-unique names.

In GraphQL, almost any sequence of letters may be an identifier - specifically all C# keywords and problematic names like `ToString` or `GetHashCode`. The following is a valid GraphQL type:

```csharp
type GetHashCode {
    getHashCode: String
    this: String
    equals: Boolean
    a: String
    get_A: String
}
```

All of these edge cases are handled by ExprCS (the abstraction), not `Coberec.GraphQL`. The user of ExprCS gets these for free (when symbol renaming is enabled).
See more details at [Symbol Name Sanitization](docs/symbol-name-sanitization.md) page.

<!--Maybe you now think that this does not make sense to handle, these are artificial counterexamples... The reality, unfortunately, is that some of these are pretty easy to hit in larger. This is not in the GraphQL version, but github has "+1" and "-1" properties [in their v3 API](https://developer.github.com/v3/emojis/), which may break a lot of automated code generators (if they had that in a machine-readable form...). Glitches just add up quickly and fixing them manually in automated pipelines would be annoying.-->

### Example

Let us show the very basics of the provided API. In this example, we will show how to create the simplest of programs - the Hello World.

```csharp
// First, we declare the symbol signatures:

// namespace MyApp.HelloWorld {
var ns = NamespaceSignature.Parse("MyApp.HelloWorld");
// public class Program {
var programType = TypeSignature.Class("Program", ns, Accessibility.APublic);
// public static int Main() {
var mainMethod = MethodSignature.Static("Main", programType, Accessibility.APublic, returnType: TypeSignature.Int32);

// get the Console.WriteLine reference
var writeLineRef = MethodReference.FromLambda(() => Console.WriteLine(""));

// then we build the actual expression tree
var body = new [] {
    // we invoke the WriteLine method
    Expression.StaticMethodCall(writeLineRef, Expression.Constant("Hello world!"))
}.ToBlock(
    // and return 0
    result: Expression.Constant(0)
);

// after all, we just add the method with the body into the type
var type = TypeDef.Empty(programType).AddMember(
    MethodDef.Create(mainMethod, body)
);

// create a default context
var cx = MetadataContext.Create();
cx.AddType(type);
// and produce a string with the output.
var csharp = cx.EmitToString();
```

More examples are on separate pages:
* [Automatic ToString implementation](docs/examples/auto-toString.md)
<!-- TODO -->

### Complete ExprCS documentation

* [C# -> ExprCS API cheatsheet](docs/csharp-features/cheatsheet.md)
    - [Accessing Fields](docs/csharp-features/accessing-fields.md)
    - [Accessing Properties](docs/csharp-features/accessing-properties.md)
    - [Calling Methods](docs/csharp-features/calling-methods.md)
    - [Creating objects](docs/csharp-features/creating-objects.md)
    - [Blocks](docs/csharp-features/blocks.md)
    - [Variables](docs/csharp-features/variables.md)
    - [Conditions](docs/csharp-features/conditions.md)
    - [Boolean Expressions](docs/csharp-features/boolean-expressions.md)
    - [Boolean Expressions](docs/csharp-features/boolean-expressions.md)
    - [Arrays](docs/csharp-features/arrays.md)
    - [Functions as Values](docs/csharp-features/functions-as-values.md)
    - [References - `ref` returns, ...](docs/csharp-features/ref-returns.md)
    - [Nullable value types](docs/csharp-features/nullable-value-types.md)
* [Metadata definitions](docs/metadata.md)
* [Symbol Name Sanitization](docs/symbol-name-sanitization.md)
* [Working with external symbols](docs/external-symbols.md)
* [Internals](docs/internals.md)

Most of API is also covered by C# documentation comments. We recommend using an IDE to explore it. Alternatively, you can also browse the [Doxygen generated documentation](https://exyi.cz/coberec_doxygen/d7/d5f/namespaceCoberec_1_1ExprCS.html).

<!-- ### Metadata

Details are on a [separate page](docs/metadata.md), so just briefly. Metadata describes the types, methods, properties, fields, ...  - it's something like the System.Reflection, except that you can create your own symbols. The same classes are used for the types you create and for the types you use from referenced libraries, so there is not much distinction between.

One of the major pain points of automatic code generation is naming the symbol so that the names never collide and they are valid to use in C#. Coberec has a generic solution for most of these issues, [see Symbol Name Sanitization](./docs/symbol-name-sanitization.md) -->

## 

<!-- While in System.Reflection, you can create type out of nowhere using `typeof(X)` or `Type.GetType(...)`, this is not exactly possible here. The context is not implicit, we need to have a `MetadataContext` which contains the information about referenced libraries and also new types defined by you. -->
