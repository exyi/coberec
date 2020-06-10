# Approaches to reducing boilerplate code

As we suggested in the introduction, people came up with plenty of approaches for reducing boilerplate code.
In this chapter, we will explore some of them.
It is important to keep the alternatives in mind to choose the right one for a given situation.
Moreover, we will see many good idea that we reuse in our work.

## .NET Reflection

.NET has an API for runtime type introspection.
Not only does it support listing the type members and their metadata, we can also invoke them.
Many .NET libraries rely on custom attributes or naming conventions to automatically discover types, invoke methods on them or list object properties for serialization or pretty-printing.

Common application is automatic dependency injection.
An example might be the [Scrutor library](https://github.com/khellang/Scrutor), which automatically registers types into ASP.NET Core service collection.
It scans specified assembly for classes based on implemented interfaces, naming conventions or similar constraint.
The classes are registered into the ASP.NET Core collection, which in turn creates instances while automatically resolving dependencies specified in the service constructor.

All of this reduces boilerplate code that would be only initializing the service classes and it is very easy to use.
Of course, there are costs:

* **transparency**: We cannot easily look at the code to see why wrong service is created. It is also hard to debug, since it is not our code that is being executed.
* **startup performance**: Reflection is quite slow and we have to all the type information for types that may not even be needed.
* **throughput performance**: Reflection is very slow when we get to invoke a lot methods using it. For example, using Reflection for serialization into JSON would be prohibitively expensive, while creating few instances of service classes is probably fine.

## Linq.Expressions + Reflection.Emit

In .NET, it is not only possible to use existing types and methods using Reflection, we can also create new methods and new implementation of interfaces.
The technique of code generation of very often used to improve throughput when using Reflection.
Probably all serializers use it to achieve reasonable performance.

> As a side note, it is also possible to inspect the bytecode of existing methods.
> The [Jil JSON serializer](https://github.com/kevin-montrose/Jil) uses it to determine the order of fields in memory, as it is a little bit faster to access them in the same order.
> See [Jil: Optimizing Member Access Order](https://github.com/kevin-montrose/Jil#optimizing-member-access-order) for more details.

We do not have to emit IL instructions manually, which would be quite cumbersome.
.NET provides a very good abstraction called Linq Expression.
It is an abstract tree, semantically similar to the C# language, so the API to create the code is very accessible to C# developers.
See the documentation of the [`System.Linq.Expressions.Expression` class](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression?view=netframework-4.7.2) for more details on the API.

Combined with System.Reflection.Emit, we can even define new implementation of interfaces.
This is sometimes used to automatically declare decorators for services for tracing, logging and similar tasks.
Interesting usage comes from a library Refit that utilizes it to make writing API clients easier.
The user only declares an interface annotated with an URL and Refit automatically implements with code making the request.

```csharp
public interface IGitHubApi
{
    [Get("/users/{user}")]
    Task<User> GetUser(string user);
}
```

This example will run a HTTP GET request to the url `/users/NAME` when we call `GetUser("NAME")`.
Of course, we have to define the methods and the input and output types (the `User` in this case), but otherwise the boilerplate is reduced to bare minimum.

With runtime code generation, we significantly reduce the performance problem with throughput.
On the other hand, startup time may rise significantly.
Furthermore, it is quite unfriendly to runtime which can not JIT new code - such as WebAssembly.
This did not used to be a big concern for the .NET community, but maybe we will a shift in the future, if WebAssembly-based computing gains traction.
In a similar fashion, relying heavily on reflection and runtime code generation afflicts tree shaking using dotnet linker.
It is not impossible to use it, but the need to register the types referenced by reflection makes the usage less streamlined and the linker itself is less efficient.

As we will show in the Design (TODO: link) section, Linq Expression from the .NET framework had very significant influence on the design of our API.
Even though we do not generate bytecode at runtime but C# code before compilation, the expression tree looks very similar.

## F# type providers

At the time of writing this work, C# does not have any mechanism for making compiler plugins or macros.
However, other .NET language, F# has a way to write compiler plugins.
It is called type providers, since it is a type parametrized by a string configuration options.
There are some limitations, but the plugin is free to use the options in arbitrary code that produces a new type.
Unlike macros in many other languages, this mechanism may generate not only code, but also new API.

It is used to create types based on example JSON files, OpenAPI schema, database schema and even result schema of SQL queries. (TODO links)

There are two options for writing the type provider.
Either it is **erasing**, then it is only a virtual type and all method invocations on it are inlined by the F# compiler.
Such type may only be used in F# as other compilers do not understand the types.
Alternatively, the type provider may be **generative** - create a real .NET type.
In that case, the type may be from C# or any other .NET language - it is just a matter of referencing the F# project.
Unfortunately for the C# developers, not many type providers are generative, so this scenario not used in practice all too much.

We really like the concept of type providers, but the F# compiler API is unfortunately quite cumbersome.
It would be nice to steal the ease of use for the end users, but we do not see a way to replicate it for C#.

## Scala macros

## D's mixin

## Dynamic (JSON deserialization)

Note that C# has a `dynamic` keyword, which allows programmers to do exactly this kind of things.
In our experience, it not used very widely, since C# programmers do not want to lose their type safety.

## IL weaving

## C# source generators

## Roslyn tree
