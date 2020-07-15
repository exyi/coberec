# Introduction

Modern software engineering practices often require writing many lines of repetitive code.
Such code is not only labour-intensive to write, but also tedious to read.
Programmers usually call the repetitive code with little information a [**boilerplate code**](https://en.wikipedia.org/wiki/Boilerplate_code).
While the programming practices are likely going to reduce the maintenance cost as a whole,
the boilerplate code often has a high maintenance cost -
for example, multiple [symbols (i.e. methods, properties or types)](https://en.wikipedia.org/wiki/Symbol_%28programming%29) might have to be renamed instead of a single one.

Many cures have been invented to the issue with boilerplate code, as it is a common software affliction.
However, there is no silver bullet and thus a lot of room for further development.

A generic approach is to automatically generate the code that is tedious to write manually.
This involves implementing programs that emit the desired code, but often, even a simple script can save a significant amount of time.
In more complex scenarios, when the code is generated from complex set of metadata such as OpenAPI specification, the code generators can get quite complex.
Build-time code generation usually does not offer very good integration with the remaining code, since the generator does not have the knowledge of the existing symbols.
For example, code generation is suitable for API clients, but it would be hard to apply for generating getters and setters for a class.

Building a project is usually not as simple as executing the compiler.
A build process may consist of many steps, one of which can be the code generation.
In this thesis, we will focus on generators that execute during the build.
Our project is a library for code generation of C#, and targets especially the more complex generators.
Nevertheless, it is very useful to briefly investigate alternative techniques as well.

<!-- As suggested by many internet discussions, for almost every problem with repetitive code, a choice of another programming language would eliminate it.
However, choice of programming language is a complicated decision.
It is very hard to undo during the project lifetime and it would often backfire on another problem that was easier in the original language.

Many programming languages support some kind of compile-time metaprogramming to eliminate the issue with boilerplate code.
The capabilities differ greatly and we will explore them briefly in a [following chapter](./approaches.md).
Compared to code generation, the meta-programming system is usually quite limited in capabilities, but offers much better integration with the hand-written code.
The meta-programs are executed by the compiler and usually have at least some information about the existing symbols.
At the time of writing, C# does not have any meta-programming system, but there is a prototype of [Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) -- a plugin API in the compiler.

A similar approach is to perform the meta-programming at runtime.
Platforms like .NET and JVM offer a rich reflection API that allows any program to explore existing types and their members.
It is even possible to create new methods, implement interfaces and create derived classes at runtime.
This approach is chosen by many .NET libraries to do serialization (such as [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) or [Jil](https://github.com/kevin-montrose/Jil)), dependency injections (e.g. [ASP.NET Core dependency injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-3.1)), ORM mapping (e.g. [Dapper](https://github.com/StackExchange/Dapper) and [Entity Framework](https://github.com/dotnet/efcore)).
The limitation is, that we can not declare any new API during runtime, because the compiler would have to know about the symbols at compile time to allow the programmer to use them.
However, this limitation does not exist in dynamically typed languages, which makes the technique even more powerful (and less safe to use). -->


## The Code Generation Library

Code generation is done simple by printing out code fragments.
This approach works very well in simple cases, when the output space is fairly limited.
For example, a program to pre-generate a list of prime numbers for a hash function could be just a few lines in any reasonable scripting language.

As the space of possible outputs rises, a lot of complications may appear.
When the symbol names are variable, we have to sanitize them, since our target language likely does not allow any string to be an identifier.
Then, we have to make sure that there are no name collisions after the sanitization.
When referencing existing symbols we have to be extra careful to really reference the expected symbol, and not a different one with the same name.
In C#, everything could be referenced by a full name to avoid this problem.
In general, being overly explicit prevents issues, but it afflicts readability of the generated code.

The goal of our project is to provide an abstraction for source code generators that will help to solve these issues.
The user of the API will provide a semantic model of the desired code and our library will translate it to a matching C# code.

As always, the abstraction comes with a cost.
We will sacrifice ease of use, since the API will force the user to be overly explicit about which type is used, which method is invoked, etc.
We will also sacrifice execution speed, as the library will perform checks and transformations on the model that would be unnecessary if we were emitting the source code directly.

This thesis is organized as follows.
In section 2 we discuss existing solutions and practices to reduce boilerplate code.
In section 3 we describe the design of our code generation library.
Then, we describe how is our library used and how it is implemented.

Next: [Approaches to Reducing Boilerplate Code](./approaches.md)
