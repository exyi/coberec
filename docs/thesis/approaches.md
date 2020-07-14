# Approaches to Reducing Boilerplate Code

Developers came up with plenty of techniques to avoid writing boilerplate code.
In this chapter, we will explore some of them.
When programming, it is crucial to keep the alternatives in mind to choose the right one for a given situation --
no solution is perfect for every problem.
Moreover, we will see many good ideas that we can reuse in our work.

## .NET Reflection

.NET has an [API for runtime type introspection](https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/reflection).
Not only does it support listing the type members and their metadata, but we can also invoke them.
Many .NET libraries rely on custom attributes or naming conventions to automatically discover types, invoke methods or list object properties.

The usual application is automatic dependency injection.
An example might be the [Scrutor library](https://github.com/khellang/Scrutor), which automatically registers types into the ASP.NET Core service collection.
It scans the specified assembly for classes implementing particular interfaces, based on the type name or a similar pattern.
The discovered classes are registered into the service collection, which in turn automatically creates instances of them.
The services may have dependencies -- it may request other services in its constructor parameters.
The dependency injection framework automatically resolves the dependencies; again, by using reflection.

All of this reduces boilerplate code that would be only initializing the service classes, and it is straightforward to use.
Of course, there are costs:

* **Transparency**:
  We cannot just look at the code to see why it does something unexpected.
  It is also hard to debug, since it is not our code that is being executed.
* **Startup Performance**:
  Reflection force runtime to load all metadata for types which impose additional startup costs.
* **Throughput Performance**:
  Invoking methods via reflection is significantly slower than a standard invocation.
  For example, using reflection for serializing an object into JSON would be prohibitively expensive, while creating few instances of the services is probably fine.

## Linq.Expressions + Reflection.Emit

In .NET, not only it is possible to use existing types and methods using reflection, but we can also create new methods and new implementations of interfaces.
In addition to using existing types and methods via reflection, .NET also permits creation of new methods and new implementations of interfaces.
This technique is often used to improve performance of code using reflection.
Probably all serializers use it to achieve reasonable performance.

> As a side note, it is also possible to inspect the bytecode of existing methods.
> The Jil JSON serializer uses it to determine the order of fields in memory, as it is a little bit faster to access them in this order.
> See [Jil: Optimizing Member Access Order](https://github.com/kevin-montrose/Jil#optimizing-member-access-order) for more details.

We do not have to emit IL instructions manually, which would be quite cumbersome.
.NET provides an excellent abstraction called Linq Expressions.
It is an abstract tree semantically similar to C#, so the API is very accessible to C# developers.

> See the [`System.Linq.Expressions.Expression` documentation](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression?view=netframework-4.7.2) for more details on the Expression API.

When combined with Reflection Emit, we can even define new types, implement interfaces and override virtual methods.
This is sometimes used to automatically declare service decorators for tracing, logging and similar tasks.

Refit, a library that makes writing API clients easier, uses Reflection Emit in a bit unusual way.
The user only declares an interface annotated with an URL and Refit automatically implements it with a class that makes the requests.

```csharp
public interface IGitHubApi
{
    [Get("/users/{user}")]
    Task<User> GetUser(string user);
}
```

This example will run an HTTP GET request to `/users/NAME` when we call `GetUser("NAME")`.
We still have to define the methods and the input and output types (the `User` in this case), but otherwise the boilerplate is reduced to the bare minimum.

> See the [System.Reflection.Emit.AssemblyBuilder documentation](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder?view=netcore-3.1) for more information about creating new types.

Runtime code generation generally increases throughput while the startup time may rise significantly.
Furthermore, it is quite unfriendly to runtimes which do not use JIT -- such as .NET on mobile devices or WebAssembly.
This limitation is not a big concern for the .NET community, but is may shift in the future, if WebAssembly-based computing gains traction.

Similarly, relying heavily on reflection and runtime code generation afflicts trimming of unused code performed by [.NET IL linker](https://github.com/mono/linker/blob/master/docs/illink-tasks.md).
The linker is not impossible to use, but the need to register all types referenced by the reflection makes the usage less streamlined and less efficient.

As we will show in the [Design chapter](./design.md), Linq Expressions from the .NET framework had a very significant influence on the design of our API.
Even though we do not generate bytecode at runtime but C# code before compilation, our expression tree looks very similar.

## F# Type Providers

At the time of writing this thesis, C# does not have any mechanism for making compiler plugins or macros.
However, F#, another .NET language, has a support for type providers.
Type providers allow users to use types parametrized by configuration options -- the provider is a plugin that generates the type during compilation.
The plugin is implemented in F# and is free to use the options in an arbitrary way.
There are some limitations, but almost any type may be produced.
Unlike macros in many other languages, this mechanism may generate new API, not only expressions or statements.
Type providers are used to create types based on
[an example JSON file](https://fsharp.github.io/FSharp.Data/library/JsonProvider.html),
[OpenAPI schema](https://github.com/fsprojects/OpenAPITypeProvider),
[database schema](https://fsprojects.github.io/SQLProvider/)
or even [result schema of SQL queries](https://github.com/demetrixbio/FSharp.Data.Npgsql).

There are two options for writing a type provider.
It may be **erasing**, then the type will not exist in the resulting assembly.
The F# compiler will inline all method invocations on the type.
The created type may only be used in F# as other compilers do not understand the type provider.
Alternatively, the type provider may be **generative** -- it creates a real .NET type.
In that case, we may use the type from C# or any other .NET language -- it is only a matter of referencing the F# project.
Unfortunately for the C# developers, not many type providers are generative, so this scenario is not used in practice too much.

The F# type providers are a neat technology that inspires similar features in other languages, but the F# compiler API is unfortunately quite cumbersome.
The type providers are very intuitive for the end users, but we do not see a way to replicate the ease of use in C#.

## Scala Macros

Scala is a functional programming language with a very capable macro system.
Scala macros are similar to the F# type providers -- the macros are basically plugins to the Scala compiler.
The macros look like functions, but the invocations are replaced by an expression returned by the macro.
The macros can also inspect and modify the expression provided in arguments.

For example, ScalaTest, a common test library, contains an [`assert` macro](https://www.scalatest.org/user_guide/using_assertions) that looks like the usual `assert` function.
However, it inspects the asserted expression and raises a helpful error message explaining why was the expression false.
Clearly, such level of integration with the calling code is not going to be possible with code generation.

Another common macro library is [Chimney](https://github.com/scalalandio/chimney).
Its purpose is to simplify mapping between similar types, similar to the reflection-based [AutoMapper](https://automapper.org/) library.
Both libraries reduce the boilerplate code, while Chimney does not have a runtime cost and mapping errors will arise during compilation, not at runtime.

The Scala macros are written in standard Scala code that runs during compilation.
The macro is basically a function that gets its arguments as expression trees and returns another expression tree.
To create an expression, we do not even have to use any API -- Scala has a syntax to create them.
String literal prefixed with `q` returns the expression that is in the quotes; `q"1 + 2"` returns an addition expression of the two constants.
As the usual string literals in Scala, the expression literal may be parametrized by other expressions and types.
For example `q"($myExpr * 1.2).asInstanceOf[$expectedType]"` multiplies `myExpr` by 1.2 while performing the type conversion.
Note that it is not the same as working with strings -- we do not have to worry about parenthesis around `$myExpr`.

The `q` literal makes writing macros very accessible, since the macro developers do not have to learn almost any new API.
Also, the macro language is not limited in any way as we may see in many other languages -- any Scala code is allowed.

## D `mixin`

D is another statically typed compiled language.
It supports static evaluation -- the D compiler can interpret almost any code during compilation.
By itself, static evaluation may only seem like an optimization.
However, in D, we can use it to generate blocks of code and include it anywhere in modules, class declarations, function bodies, etc.

We do it by using the [`mixin` keyword](https://dlang.org/articles/mixin.html) -- it looks like a function, and it works like `eval` from Javascript.
Since D is not an interpreted language, `eval` would make little sense, so `mixin` only works at compile time.
The compiler statically evaluates the argument and replaces `mixin` with the string value.
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

Unlike in Scala, we are manipulating strings, not a symbolic model of the code.
Note how we are referencing the `writeln` function in `hello`, but it is imported in `main`.
In such simple example, it looks like a subtle difference, but the `mixin` may use anything from the local scope, and there may be accidental collisions of identifiers.

Since the code generator is just a function returning a string, there is almost no way it could be more straightforward.
It can not integrate with the calling code so tightly as we have seen in Scala.
Yet, combined with D compile-time reflection, it can still offer much tighter integration than a separate code generation process.

## Dynamic Languages

So far, we have only discussed statically typed languages.
However, dynamically typed platforms have a lot to say to the problem of a boilerplate code.

The advantage of most dynamically typed platforms is that there is no problem to create a new type at runtime.
In Javascript, the `{}` expression creates an empty object and assigning to `obj.myField` creates a new field if it does not exist already.
We can also assign a field using the `obj["myField"] = value` notation, and any expression may be used instead of `"myField"`.
Unlike in C#, where we always have to declare the types in code, we just create any objects at runtime.
This means that there is no need to declare an expected type when we want to deserialize an object from JSON.

Using a [Proxy object](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy), we can even intercept fundamental operations on an object, like a field access.
Instead of generating API clients, this powerful mechanism may be used to create an object that translates method calls into API calls.
A simple demonstration of such API client (and more) may be found in [Alberto Gimeno's article on JS Proxies](https://medium.com/dailyjs/how-to-use-javascript-proxies-for-fun-and-profit-365579d4a9f8).

A slight disadvantage of such approach may be runtime performance.
However, in cases when Javascript is used, the runtime overhead of an API invocation does not matter too much.
On the other hand, the code size is critical for the client-side applications, so it a great advantage when the API client is only about 20 lines of code.

> Other languages than Javascript often have similar concepts, but we do not have the space here to go through all of them.
> For brevity, let us just focus on Javascript in this section.

The obvious disadvantage is the lack of guidance before running the program -- there are no types to guide the compiler or an IDE.
Moreover, we also lack of runtime validation of the objects.
If the server sent a string instead of a number in the JSON object, we will be unlikely to notice until we use that specific value.
On the contrary, such misbehaviour will crash during deserialization on a strongly typed platform like .NET.

> Interesting compromise could be to generate a TypeScript type definitions from a schema (such as GraphQL or OpenAPI).
> At runtime we would use the dynamic API clients, or at least dynamic object created from JSON deserialization.
> To achieve a relative type safety, the usages would be checked by TypeScript at build.

Note that C# has a `dynamic` keyword, which allows programmers to use C# as a dynamically typed language.
It supports almost anything we can do with the Javascript Proxy objects.
Yet, in our experience, it not used very widely, since C# programmers do not want to lose their type safety.
However, many libraries, including one of the most popular JSON (de)serializer, Newtonsoft.Json, supports it -- we can simply declare `dynamic myObject = JObject.Parse(myJson)` and use it as we would in JavaScript (see the [documentation for more details](https://www.newtonsoft.com/json/help/html/QueryJsonDynamic.htm))

## C# 9 Source Generators

C# is going to have an API for integrating source code generators into the compiler.
It is going to be a way to write plugins that add symbols to the compilation during the process.
Note that at the time of writing this work, C# source generators are still in preview, so we have not used them.

The API is quite straightforward -- the plugin simply adds new source files to the C# compilation object.
The difference from a separate code generation process is that the source generator has access to the metadata about the other compiled types.
We will have to wait to see what will turn up in the .NET ecosystem; all of this is still in the future.
It seems, however, that it should be possible to generate code that depends on the metadata of other types -- possibly replacing serializers, dependency injection frameworks and many more libraries that are currently reflection-based.

Let us show the code example from the [Source Generators announcement](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/):

\clearpage

```csharp
[Generator]
public class HelloWorldGenerator : ISourceGenerator
{
    public void Execute(SourceGeneratorContext context)
    {
        // begin creating the source we will
        // inject into the users compilation
        var sourceBuilder = new StringBuilder(@"
using System;
namespace HelloWorldGenerated
{
public static class HelloWorld
{
    public static void SayHello()
    {
        Console.WriteLine(""Hello from generated code!"");
        Console.WriteLine(
            ""The following syntax trees existed"" +
            ""in the compilation that created this program:"");
");

        // using the context, get a list of syntax trees
        // in the users compilation
        var syntaxTrees = context.Compilation.SyntaxTrees;

        // add the path of each tree
        foreach (SyntaxTree tree in syntaxTrees)
        {
            sourceBuilder.AppendLine(
                $@"Console.WriteLine(
                    @"" - {tree.FilePath}"");");
        }

        // finish creating the source to inject
        sourceBuilder.Append(@"
    }
}
}");

        // inject the created source into the users compilation
        context.AddSource("helloWorldGenerator",
            SourceText.From(
                sourceBuilder.ToString(),
                Encoding.UTF8
            )
        );
    }
}
```

We can see that the generated code is just a string -- there is no attempt to provide an API for building the code.
That is probably a good thing, since the API may be done by different projects (like our work) and it simplifies the core C# API that will have to be maintained for a long time.
At the time of writing, our project has no support for the Roslyn symbol model, but it seems like we should add support when C# code generators become stable.

> Note that even in this simple example, there is a bug in the code generator.
> The generated code prints a list of filenames in the current project.
> The filename is simply included in the string, so it will break if any file contains a quotation mark (`"`), and the compilation will fail.

It will be very interesting to observe how will this feature affect the .NET community.
While now, code generation is a bit shunned by many project for its unreliability and clumsiness, we can already see a massive interest in Source Generators on social media.

Unfortunately, it is probably not going to be possible to migrate existing libraries to benefit from code generation.
Most reflection-based libraries are configured at runtime by initializing it with an "options object".
When the logic is dynamic, or the code is generated at runtime, this is reasonable -- creating the options in C# is more approachable than learning a specific configuration DSL.
With compile-time code generation, it is not going to be possible to know what will come at the runtime.
So, either the generated code will have to be very generic to cover all the possible options, or we will see a revival of separate configuration files.

## IL Rewriting

Rewriting of the intermediate language is a powerful technique used by many .NET projects.
The point is to generate the boilerplate after the project is compiled.
In principle, the transformation may do anything to the compiled binary.
Although the power of IL rewriting is nearly unlimited, the transform should be reasonable to keep the code understandable.
Usually, the transformation only do the following:

* Overrides optional methods like ToString, GetHashCode and Equals.
  For example, [Fody.Equals](https://github.com/Fody/Equals) implements structural equality boilerplate.
* Instruments methods with logging, timing or exception handling.
  For example, [Fody.PropertyChanged](https://github.com/Fody/PropertyChanged) implements the INotifyPropertyChanged interface and instruments methods with calls of PropertyChanged; [Tracer](https://github.com/csnemes/tracer) adds tracing; and [Fody.MethodTimer](https://github.com/Fody/MethodTimer) uses Stopwatch class to measure execution time of a method.
* Replaces basic constructs.
  It may redirect method calls to a different method.
  For example, [Fody.Caseless](https://github.com/Fody/Caseless) makes all string comparisons case insensitive.
  Alternatively, [LoggerIsEnabled.Fody](https://github.com/wazowsk1/LoggerIsEnabled.Fody) adds a condition around logging statements.
* Replaces empty method bodies.
  For example [With.Fody](https://mikhail.io/2016/05/tweaking-immutable-objects-with-csharp-and-fody/) automatically implements a With methods for immutable objects.

All the mentioned examples are from the .NET ecosystem and use a common abstraction [Fody](https://github.com/Fody/Fody).
As the Fody authors claim in the project description, it greatly reduces the effort needed to integrate the rewriting step into .NET build.

> Manipulating the IL of an assembly as part of a build requires a significant amount of plumbing code.
> This plumbing code involves knowledge of both the MSBuild and Visual Studio APIs.
> Fody attempts to eliminate that plumbing code through an extensible add-in model.

Some other platforms also have projects that do source code or intermediate language rewriting.
In the Javascript ecosystem, it is usual to have multiple rewriting steps involved in the build:

* To replace new language features with the ones that even older browsers can run.
* To bundle multiple modules together to make the distribution over the internet more efficient.
* To minify the code to make the bundle smaller in size.

The concerns about older versions of the virtual machine and application size are much stronger on the web.
Still, even in the .NET ecosystem, there is a .NET IL Linker project that does tree shaking to reduce the binary size.
Such transformations are however a bit out of scope of this work.
We are slowly getting into the area of compiler optimizations, which may sometimes also help to reduce boilerplate, but it is not the main point.

The limitation of IL rewriting is that introducing new symbols is quite dubious.
The problem is that the project compilation runs before the rewriting step.
If we are adding symbols after the project is compiled, the project itself must not reference them.
Alternatively, there must be a dummy symbol that the C# compiler may use, and then the IL rewriter will redirect it to the new symbol.

The already mentioned [With.Fody](https://mikhail.io/2016/05/tweaking-immutable-objects-with-csharp-and-fody/) library has this exact issue.
It implements a `With` method to create a modified clone of an immutable object.
Given an object:

```csharp
public class MyClass
{
    public MyClass(int a, string b)
    {
        this.A = a;
        this.B = b;
    }

    public int A { get; }

    public string B { get; }
}
```

The Fody add-in will introduce a method for each property:
```csharp
public MyClass With(int value)
{
    return new MyClass(value, this.B);
}

public MyClass With(string value)
{
    return new MyClass(this.A, value);
}
```

This by itself would work well, but only if the project was a class library that itself never calls the `With` method.
Nevertheless, there is a way -- we introduce the dummy symbol.
We will add a generic `With<T>(T value)` method to the class, which the C# compiler will use in that project.
As the author claims in [the blog post](https://mikhail.io/2016/05/tweaking-immutable-objects-with-csharp-and-fody/):

> It is safe to call With methods in the same assembly where the class is defined: the calls get adapted to the real implementation automatically.

![Illustration of the symbol references. Dashed lines are introduced in the rewriting process by Fody.](./IL_rewriting_diagram.svg)

Another problem with IL Rewriting is that the .NET tooling is not well-prepared for it and some advanced development features might stop working.
For example, Edit & Continue feature does not work with [Fody](https://github.com/Fody/Fody/tree/c31ea96b5be6ce66b992614dda2af2c0a9bb91d2#edit-and-continue) nor with [PostSharp](https://doc.postsharp.net/requirements#incompatibilities), a proprietary tool doing IL Rewriting.

<!-- ## Roslyn tree TODO maybe -->

<!-- ## Summary

As we could expect, there is no silver bullet.
We have discussed many different ways programmers use to avoid writing boilerplate code.
It is not easy to compare them, since there may be many more hidden properties that apply in a given situation.
Nevertheless, we will rank each one for few criteria.
This is not a complete design decision table, just a summary of what we have discussed above.

The criteria are

* Compilation performance impact -- how much time is added to the build process.
* Startup performance impact -- how much time is added to the application startup.
* Throughput performance impact -- if the code is going to be slower when using given technology.
* Reliability -- how likely is the implementation going to be buggy.
* Transparency -- how clearly can the user see what is going on.

Reflection -->


## Summary

We have discussed many different ways programmers use to avoid writing boilerplate code.
With this background we might be able to tell how does code generation compare to alternatives.

**Transparency** -- The generated code can be simply read and debugged when it misbehaves.
It is true that this point really depends on output quality of the generator and complexity of the generated program.
However, we think this is the main advantage, so we focus on the output code quality with our library and the GraphQL Schema compiler.
The alternatives usually do not come even close the transparency we can offer with source generation.
Even with D `mixin` and presumably C# 9 Source Generators, we can not directly open the generated source file and place a debugger breakpoint in it.

**Runtime Performance** -- Source generation does not need any runtime initialization and we can generate fairly specialized and optimized code for the specific task.
Both startup time and throughput performance is probably going to be decent.
This is not an exclusive advantage, macros, IL rewriting are on par with code generation, since these also generates code.
However, in some cases the the speed may be afflicted by the increase in code size.

**API Creation** -- Code generation is nearly the only discussed approach that can introduce new API symbols.
While dynamic languages can introduce new symbols at runtime, it does not offer type safety and IDEs can not guide the user through autocompletion.
However, it is exactly when we generate large APIs that we hit a whole edge case parade -- the symbols must get valid names, there must not be any collisions, we must reference other symbols correctly, etc.

**Portable Principle** -- In principle, we can generate code in any text-based programming language.
In this work, we focus on C#, but we could have a similar project for any programming language.
By contract, macros and reflection-based tricks are mostly focused on a single platform -- there is not a way to create similar API for C# and for, let us say, Python.
However, writing a robust code generator is not as easy, so we only claim that the principle is portable.
Most of the work done on a code generator is unfortunately not going to be portable.
In theory, we could design a common abstraction, but is also rather a holy grail than a reasonable project -- there would be enormous amount of compromises to make.

Obviously, code generation is no silver bullet, there is also many problems compared to alternatives.

**Code Size** -- Generating a lot of boilerplate code is going to affect the application size, which may sometimes degrade performance.
It is most problematic for client-side web applications, where the size of Javascript code is crucial for reasonable load time.
It is a shame that we do not want to use code generation in JavaScript.
It is a very convenient language for code generation, since the number of edge cases is much lower compared to C#.

**Compilation Time** -- Another step in the build process is going to affect the time it takes to compile the project.
Not only does it make it slower, it also complicates the process and makes it less reliable (due to missing dependencies, bugs, and similar issues)

**Reliability** -- Especially when we generate a large API, the number of edge cases the code generator may hit is vast.
It is the issue we want to tackle in this work -- create an abstraction that will make it possible to write a reliable code generator.

Next: [Design](./design.md)
