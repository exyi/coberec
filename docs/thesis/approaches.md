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

Scala is another functional programming language and it has a very capable macro system.
Scala macros are similar to F# type providers - macros are basically plugins to the Scala compiler.
However, it is not only a separate type created from simple metadata.
Scala macros look like a function, but can also inspect and modify the expression provided in arguments.

For example, a common test library ScalaTest contains an [`assert` macro](https://www.scalatest.org/user_guide/using_assertions) that looks like a plain old `assert` function.
However, it inspects the asserted expression and raises a nice error message explaining why the expression is false.
Clearly, such level of integration with the calling code is not going to be possible with code generation.

Another common macro library is [Chimney](https://github.com/scalalandio/chimney).
Its purpose is to simplify mapping between similar types, similar to a reflection-base .NET library [AutoMapper](https://automapper.org/).
Both of these libraries reduce the boilerplate code in a similar fashion, while Chimney does not have a runtime cost and mapping errors will arise during compilation, not at runtime.

Scala macros are written in standard Scala code that runs at compilation.
The macro is basically a function that gets its arguments as expression trees and returns another expression tree.
To create an expression, we do not even have to any API - Scala has a syntax to create them.
String literal prefixed with `q` returns the expression that is written inside; `q"1 + 2"` return an addition expression of the two constants.
As usual Scala string literals, the expression literal may be parametrized by other expressions and types.
For example `q"(${myExpr}.asInstanceOf[Double] * 1.2).asInstanceOf[${expectedType}]"` multiplies `myExpr` by 1.2 while performing the type conversions.
Note it is not the same as working with strings - we do not have to worry about parenthesis around `${myExpr}`.

The `q` literal makes writing macros very accessible, since the users do not have to learn any new API for building them.
Also, the macro language is not limited in any way as we may see in many other languages - it is just Scala code.

## D `mixin`

D is another statically typed compiled language.
D supports static evaluation - almost any code may interpreted by the compiler during compilation.
By itself, static evaluation may just seem like a useful optimization.
However, in D, we can use it to generate blocks of code and include it anywhere in modules, class declarations, function bodies, etc.

We do it by using the `mixin` keyword - it looks like a function, but includes the string parameter in its place.
As a simple demonstration of the concept, this is a mixin Hello World:

```dlang

void main()
{
    import std.stdio;
    mixin(hello());
}

string hello()
{
    return "writeln(\"hello world\");";
}
```

Unlike in Scala, in D, we are really manipulating string, not a symbolic model of the code.
Note how we are referencing the `writeln` function in `hello`, but it is imported in `main`.
In such simple example, it looks like a subtle different, but the mixing may use anything from the local scope and there may be accidental collisions of identifiers.

Since the code generator is just a function returning string, there is almost no way it could be simpler.
It can not integrate with the calling so tightly as we have seen in Scala.
Yet, combined with D compile-time reflection, it can still offer much tighter integration than a separate code generator process.

## Dynamic (JSON deserialization)

All discussed languages were statically typed.
However, dynamically typed platforms have a lot to say about the problem of boilerplate code.

Obvious advantage of dynamically type platforms is that there is no problem to create new types at runtime.
For example, in Javascript, the `{}` creates an empty object and simply assigning to a new field creates it.
We can also assign to a variable field using the `obj["myField"] = value` notation; and any expression may be used instead of `"myField"`.
Unlike in C# where we have to declare the types in code, we just create them at runtime.
This means that there is no need to declare an expected type when we want to deserialize an object from JSON.

Using a [`Proxy` object](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy), we can even intercept fundamental operations on an object, like field access.
Instead of generating API clients, this powerful mechanism may be used to create an object that translates method calls into API calls.
A simple demonstration of such API client (and more) may be found in [Alberto Gimeno's article about JS Proxies](https://medium.com/dailyjs/how-to-use-javascript-proxies-for-fun-and-profit-365579d4a9f8)

A slight disadvantage of such approach may be runtime performance.
However, in cases when Javascript is used, the throughput of API invocations does not matter too much.
On the other hand, the code size is a very important consideration for client-side Javascript applications, so it a great advantage when an API client that is only 20 lines of code.

> Other languages than Javascript often have similar concepts, but we do not have the space here to go though all of them.
> For brevity, let us just focus on Javascript in this section.

The obvious disadvantage is the lack of guidance before the program runs - there are no types to guide the compiler or an IDE.
We may also lack runtime validation of the objects.
If the server were to send a string instead of a number in the JSON object, we would be unlikely to notice until we show it to the user.
On the contrary, such misbehavior will crash during deserialization on a strongly typed platform like .NET.

> Interesting compromise could be to use the dynamic API clients, or at least dynamic object created from JSON deserialization.
> To achieve the type safety, a TypeScript definition could be generated from a common schema.

Note that C# has a `dynamic` keyword, which allows programmers use C# as a dynamically type language.
It supports almost anything we can do with the Javascript Proxy objects.
Yet, in our experience, it not used very widely, since C# programmers do not want to lose their type safety.
However, one of the most popular JSON (de)serializer, Newtonsoft.Json, supports it - we can simply declare `dynamic myObject = JObject.Parse(myJson)` and use it as we would in JS (see the [documentation for more details](https://www.newtonsoft.com/json/help/html/QueryJsonDynamic.htm))

## C# source generators

Note that at the time of writing this work, C# source generators are still in preview, so we have not tried using them.

C# source generators is a way to write plugins to the C# compiler.
The API is quite straightforward - the plugins simply adds new files to the C# compilation object.
The different from separate code generation process it that the source generator has access to information about other compiled objects.
We will have to wait to see what will turn up in the .NET ecosystem, all this is still in the future.
It seems, however, that it should be possible to generate code that depends on the metadata of other types - possibly replacing serializers, dependency injection frameworks and many more libraries that are reflection-based today.

Let us have a look at the code example from the [Source Generators announcement](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/):

```csharp
[Generator]
public class HelloWorldGenerator : ISourceGenerator
{
    public void Execute(SourceGeneratorContext context)
    {
        // begin creating the source we'll inject into the users compilation
        var sourceBuilder = new StringBuilder(@"
using System;
namespace HelloWorldGenerated
{
public static class HelloWorld
{
    public static void SayHello()
    {
        Console.WriteLine(""Hello from generated code!"");
        Console.WriteLine(""The following syntax trees existed in the compilation that created this program:"");
");

        // using the context, get a list of syntax trees in the users compilation
        var syntaxTrees = context.Compilation.SyntaxTrees;

        // add the filepath of each tree to the class we're building
        foreach (SyntaxTree tree in syntaxTrees)
        {
            sourceBuilder.AppendLine($@"Console.WriteLine(@"" - {tree.FilePath}"");");
        }

        // finish creating the source to inject
        sourceBuilder.Append(@"
    }
}
}");

        // inject the created source into the users compilation
        context.AddSource("helloWorldGenerator", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    public void Initialize(InitializationContext context)
    {
        // No initialization required for this one
    }
}
```

We can see that the generated is just a string, there is no attempt to provide an API for building the source.
That might be a good thing, since that may be done by different projects (like our work) and simplifies the core API that will have to be maintained for a long time.
At the time of writing, we have no support for the Roslyn symbol model, but it seems like we should add support when C# code generators become stable.

> Note that even in this simple example, there is a bug in the code generator.
> The generated code prints a list of file names in the current project.
> The file name is simply included in the string, so it will break if any file contains a quotation mark (`"`), the compilation will fail.

It will be very interesting to observe how will this feature affect the .NET community.
While now, code generating is a bit shunned by many project for its unreliability and clumsiness, we can already see a massive interest in it on social media.

Unfortunately, it is not going to be possible to migrate existing libraries to benefit from code generation.
Most reflection-based libraries are configured at runtime by initializing it with an "options object".
When the logic is dynamic or the code is generated at runtime, this is reasonable - creating the options in C# is more approachable than learning a specific configuration DSL.
With compile-time code generation, it is not possible to know what will come at the runtime.
So, the generated code will have to be very generic to cover all the possible options, or we will se a revival of separate configuration files.

## IL weaving


## Roslyn tree
