# C# from GraphQL Schema Generator

To demonstrate that the concept works, we have reimplemented a C# code generator using the ExprCS abstraction.
It translates a domain model written in GraphQL Schema language into immutable classes.
The model in GraphQL is very concise while the generator produces quite rich classes with equality, ToString implementation and methods for easier modification of the immutable objects.
The point of this chapter is not to explain its usage in depth, as there is more detailed [documentation](https://github.com/exyi/coberec/blob/master/docs/graphql-gen.md) on the GraphQL Schema compiler.


Since we use this code generator to create parts of the Expression and metadata API, we will briefly explain how it works.
The Expression and metadata are quite broad types, and we want them to be immutable, so we are using this code generator to reduce the amount of boilerplate we had to write.
On top of the basic generated API, we still provide helper methods to make it easier to create the objects.

To illustrate how the generated API looks, let us show simple GraphQL Schema examples and how they compile to C#.

A simple type with properties `a` and `b`, the `b` is an array and must contain at least one element.

```gql
type T {
    a: Int
    b: [String] @validateNotEmpty
}
```

The entire class is in the [CodeGeneratorTests.ThesisExample.cs](https://github.com/exyi/coberec/blob/master/src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.ThesisExample.cs) file.
The important features of the class are:

* The properties `A` and `B` of types `int` and `ImmutableArray<string>`
* A constructor `T(int a, ImmutableArray<string> b)`.
* A more general constructor `T(int a, IEnumerable<string> b)`.
* Implemented Equals method, GetHashCode method and the `==` and `!=` operators. The types are equal when all properties are equal.
* Implemented ToString method.
* `With(...)` method that creates a new instance with modified properties. The arguments are all optional, so `x.With(a: 1)` sets `A` and `x.With(b: ...)` sets `B` and `x.With(a: 1, b: ...)` sets both.

The type validates the constraints when it is created and throws a ValidationErrorException when it is invalid.
The exception contains a ValidationErrors instance with information on which field was invalid with a proper error message.
When there is more than one validation error, all should be present in the list.
This is convenient in the case of the Expression class as it is often validated with many rules.

The types also contain Create method that returns a `ValidationResult<T>`.
Instead of throwing the exception, it always returns an object which contains either the created instance or the validation errors.
In cases when we are not sure about the validity, the `Create` method might be more convenient.
The `ValidationResult<T>` is a monad implementing `Select` and `SelectMany` methods which allow usage of the [C# query syntax](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/from-clause); similarly as shown on [Mark Seemann blog](https://blog.ploeh.dk/2020/06/29/syntactic-sugar-for-io/).

In C#, default argument values are quite limited.
The default may only be of a primitive type, string or `default(T)` (null for classes, "zeros" for structs).
This a complication for the `With` method -- we would like to have the current property value as the default; the intended signature would be `With(int a = this.A, ImmutableArray<string> b = this.B)`, but this is not possible in C#.
As a workaround, we have introduced a `OptParam<T>` type.
It recognizes two states -- either it has a value of type T, or it has no value; somewhat like the Option or Maybe known from other languages.
Moreover, it has an implicit conversion from T, so it is constructed automatically when the parameter is used.
This is the simplified implementation:

```csharp
public readonly struct OptParam<T>
{
    public readonly T Value;
    public readonly bool HasValue;

    public OptParam(T v)
    {
        this.Value = v;
        this.HasValue = true;
    }

    public static implicit operator OptParam<T>(T val) =>
        new OptParam<T>(val);
}
```

The `default(OptParam<T>)` has `HasValue = false` and the implicitly constructed instance has `HasValue = true`.
The With method will have the following signature with both parameters optional.

```csharp
public ValidationResult<T> With(
    OptParam<int> a = default,
    OptParam<ImmutableArray<string>> b = default
)
```

> It may seem that we could simply use null as the default value for our properties in the With method.
> However, that would make setting a field to null impossible.
> Furthermore, it would add an edge case where With and constructor behave differently, which could lead to unexpected behaviour.

We also have support for [GraphQL unions](https://graphql.org/learn/schema/#union-types) which are less straightforward to represent in C#.
We are using inheritance to represent one option of many, but the union types also have helper methods to make creating and processing the unions easier.
In the ExprCS API, Expression and TypeReferences are unions.
A simpler example of a generated union may found in the [CodeGeneratorTests.SimpleUnionType.cs](https://github.com/exyi/coberec/blob/master/src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleUnionType.cs) file.

Apart from the basics like constructors, equality and ToString, we automatically implement some helpers.
Each case of the union has a factory method on the type, for example, we can use `Expression.Constant(...)` instead of `new Expression.ConstantCase(...)`.
We have a method called Match that helps with exhaustive matching.
It takes a lambda function for each case of the union, invokes one of them and returns the result value.
The principle is similar to the C# switch expression, but Match enforces that all cases are covered.
For non-exhaustive matching, we recommend using the [switch expression](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression).

The GraphQL Schema compiler existed before the Expression API, which is the reason why the Expression API may be generated by from GraphQL.
Initially, the project was based purely on the ILSpy decompiler.
By now, it is mostly migrated to the Expression API while some parts still use the ILSpy Fallback API.

The code generator is published as [`Coberec.CLI` NuGet package](https://www.nuget.org/packages/Coberec.CLI/).
Since the GraphQL generator currently depends on the Expression API, and the Expression API is built from GraphQL Schema, we use the code generator NuGet package.
The project is not hard to bootstrap from historical source codes, but the cyclic dependency is easier to break using the published binary package.

> We could also do it vice-versa and reference the Expression API library NuGet package to build the code generator.
> However, the code generator was much more stable than the Expression API when we were developing the project.


Next: [Implementation of the API](./internals.md)
