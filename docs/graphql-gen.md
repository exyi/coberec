# GraphQL Schema -> C# classes

This documentation page is about a code generator from GraphQL Schema which is built on top of Coberec.ExprCS. It translates a domain model written in GraphQL Schema language into C# immutable classes, so you can declare your domain very easily without any boilerplate and get all the nice properties of immutable model at the same time.

## Installation

To the tool, just install (or update) the [`Coberec.CLI`](https://www.nuget.org/packages/Coberec.CLI/) NuGet package

```
dotnet tool update -g Coberec.CLI
```

And then call it by `coberec`:

```
coberec --config schema/config.json schema/**.gql --outDir GeneratedSchema
```

The configuration is described [bellow](#configuration).

## Schema

First, you need the schema. To define it, you can use the fundamental features of [GraphQL Schema](https://graphql.org/learn/schema/):

### Scalars

Scalars are aliases for string that have a special meaning. As you will see later, you can attach validation rules to them

For example, Url might be a good candidate for being declared as scalar:

```graphql
scalar Url
```

### Types

Types just compose multiple things together in a field. I guess it's pretty much self descriptive

```graphql
# declared a type
type User {
    # non-nullable field id
    id: ID!
    # nullable field
    firstName: String
    lastName: String
    photoUrl: Url
}
```

### Unions

Unions are a bit alien technology for C# developers, it's a type that has one of many options.

For example, we might have another type, `Robot` and then we'd like to allow either Robot or a User in another field. For that we can create a union `Creature` that can be Robot or User

```graphql
type Robot {
    id: ID!
    name: String
}

union Creature = Robot | User
```

Then you can use it in any other type:

```graphql
type Task {
    description: String!
    assignees: [Creature!]!
}
```

### Interfaces

Interfaces seem a bit like unions, but interfaces declare common properties that the types must have. For example, we could declare a interface `WithId` to to have a common marker for anything that may be referenced.

```graphql
interface WithId {
    id: ID!
}

type Robot implements WithId {...}
type User implements WithId {...}
```

If you are looking for more complex examples, you can check out the ExprCS Schema. The entire abstract tree that we use to generate C# is actually declared in the language and it's actually quite simple: [src/Coberec.ExprCS/Schema](../src/Coberec.ExprCS/Schema)

## Validation

One of the main features of domain model created by this method is that it can enforce it's invariants in the sense that invalid object can't be even created. One of the ways to do that is simply make the invalid states unrepresentable by clever usage of the type system (namely unions) as suggested by https://fsharpforfunandprofit.com/posts/designing-with-types-making-illegal-states-unrepresentable/. Other part is to enforce more subtle validation rules - that the represented values actually can make sense.

Any field can be annotated by a `@validateXXX` directive. The validators can be registered in the configuration option `Validators`:

```json
{
    "Validators": {
        "customValidator": { "ValidationMethodName": "MyNamespace.MyClass.CustomValidator" }
    }
}
```

The validation method should be just a method that takes the validated value as parameter.

By default you can use `@validateNotEmpty` on arrays and string and `@validateRange` on Integers. Note that in GraphQL directives are written after the field - `age: Int @validateRange(low: 0, high: 150)`.


## Configuration

The configuration is basically a JSON file with the serialized EmitSettings class inside. The fundamental configuration options are:

* `Namespace`: Specifies C# namespace of the generated classes. It must be specified here or as a command line argument.
* `PrimitiveTypeMapping`: A dictionary that allows you to add primitive types that map to certain .NET type. For example `{ "Uri": "System.Uri" }` will allow you to use type `Uri` in the schema that will be directly translated to `System.Uri`. You are supposed to specify full name. This can be also used to override the default types.
* `AdditionalReferences`: A list of path to assemblies that should be taken into account when producing the code. In order to be safe and not produce too much boilerplate, ILSpy has to know metadata about the references and taking this from MSBuild is simply too complicated.
* `Validators`: Configuration of custom validators. See the validation section for more info.
* `ExternalSymbols`: A list of descriptions of symbols in the current project. This is required to let ILSpy know about your custom functions that may be used from the schema definition but itself may require a reference to the schema. See the validation section for more info.
* `EmitWithMethods`: If helper methods for creating a clone with some of the properties changed should be produced. These method are called `With`. (default is **true**)
* `EmitInterfaceWithMethods`: If the `With` methods should be also on interfaces. (default is **true**)
* `EmitOptionalWithMethods`: If the `With` methods should have optional parameters. (default is **true**)
* `WithMethodReturnValidationResult`: If the `With` method should return `ValidationResult<T>` or throw an exception when the validation fails. (default is **true**)
* `AddJsonPropertyAttributes`: If `[JsonProperty("originalNameFromSchema"]` attributes should be added to the properties if the names are somewhat changed. (default is **true**)


## Compiler

To transform your schema into C# code, you can run the Coberec.CLI program. Use it as follows: `Coberec.CLI.exe input1.gql ... input43.gql --outDir ./GeneratedClasses [--config coberec.config.json] [--namespace MyProject.Model]`

The parameters are:

* `--config configFile.json`: Json file used for configuration of code generator, see config docs for more info. Supports most JSON5 features (unquoted identifiers, trailing commas, C-style comments).

* `--out outFile.cs`: Generated code goes into the specified file. You can use `-` to write to std out.

* `--outDir outDirectory`: Generated code goes into the specified directory. Usually, 1 class goes into 1 file (except for name conflict and nested classes).

* `--namespace MyNamespace`: Specifies C# namespace of the generated classes. The same option can be set in the configuration file, but this one has precedence.

* `[--input] inputFile.gql`: Add the specified input file into the schema.

* `--verbose`: Prints a bit more information sometimes.

* `--invertNonNullable`: Makes non-nullable types nullable and vice versa. It's useful hack for the case when almost everything is non-nullable as it's the default with this option.

To run the compiler before build of your project, simply put these lines into the .csproj file:

```xml
<Target Name="BuildCoberec" BeforeTargets="BeforeCompile" Inputs="schema/**" Outputs="GeneratedSchema/**.cs">
    <Exec Command="dotnet ./path/to/Coberec.CLI.dll --config schema/config.json schema/**.gql --outDir GeneratedSchema --verbose" />
    <ItemGroup>
        <Compile Include="GeneratedSchema/**.cs" />
    </ItemGroup>
</Target>
```

## Generated code

The exact form of generated depends on the options you set in the configuration, but differences are not going to be huge. You can mostly disable features you don't need.

All generated classes implement structural equality, when you use `==` or `Equals` it will compare if all properties are equal and not if references are equal. By default, it also implement `ToString` method for easier debugging. Then it of course contains constructors and properties that you'd expect.

Scalars are of course the simplest of all, you can check out our [example from tests](../src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleScalarType.cs). It just contains a property `string Value`, matching constructor, validation and structural equality.

Composite types can get more ~~complex~~ interesting, you can also check out [an example](../src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleCompositeType.cs). The type has the following features:

* The properties `Field543` and `AbcSS`
* Constructor that matches them
* Another constructor that is less strict in the accepted types (takes `IEnumerable<T>` instead of `ImmutableArray<T>`)
* Static methods called `Create` that return a `ValidationResult<T>`. They may be very useful when you want to explicitly check if your arguments are valid without catching exceptions.
* Method called `With` that may be used for creating a updated copy of the type. The properties are immutable - you can't set a value, but you can clone the object with the new value. For example `a.With(abcSS: 14)` will "set" `AbcSS` to 14.
* Structural equality (ehh, this is quite noisy in C#...)
* Automatic ToString of form `Test123 {Field543 = [...], AbcSS = ...}`

Unions are even more fun, since C# does not really have support for this concept. Again, there is [an example](../src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleUnionType.cs). Basically, it contains

* Nested classes, each for one case of the union.
* Factory methods for each case. The form is `MyUnion.Case(args...)`
* Implicit conversions from the inner types (unless it's interface)
* Function called `Match` that may be used for switching on the cases. For example
        ```
        a.Match(test123: x => ProcessTest123(x),
                        @string: x => ProcessString(x))
        ```

Interfaces are very simple, but for completeness, there is also [an example](../src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleInterfaceType-default.cs)
