# COBEREC Generator

This an experimental C# code generator that generates data classes from model written in GraphQL Schema language. It is a tool aiming at generating **COrrect REadable and BEautiful Code**, with priority on the correctness.


## GraphQL Schema

COBEREC translates a domain model written in GraphQL Schema language into C# immutable classes, so you can declare your domain very easily without any boilerplate. As an intermediate representation, we use a simple representation of the schema, so you can also fairly easily consume it and create db schema, user interfaces or whatever yourself. Well, simply if you don't care about correctness in edge cases or if you already have a simple and robust backend...

## CSharp generator

The C# code generator is based on the awesome ILSpy decompiler which makes sure that it produces quite nice looking code that always represents what was intended. C# is a very complex language and it would be very hard without using ILSpy's backend.


## Usage

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
union Create = Robot | User

# you can also define interfaces for things with common properties (if composition does not fit your needs)
```
### Compiler

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
### Configuration

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

### Validation

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
