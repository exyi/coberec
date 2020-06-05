# Introduction

Modern software engineering practices often lead to need of lots of boilerplate code.
Such code is not only annoying to write, but also makes reading the code tedious.
While the programming practices are likely going to reduce the maintenance cost as a whole,
the boilerplate code is often troublesome to maintain -
for example, multiple symbols might have to be renamed instead of a single one.

> We will use the term **symbol** as a generic term for a method, function, variable, property, type, etc. See [Wikipedia - Symbol](https://en.wikipedia.org/wiki/Symbol_(programming))

Since boilerplate code is a very common software affliction, people have invented many cures to it.
However, as it goes in software engineering, there is no silver bullet and thus a lot of room for further development.

Very generic approach is to generate the code that we don't like to write at the project build.
Obviously, this requires us to write program that emits the desired code, but often, just a simple script can save a lot of trouble.
In more complex scenarios, when the code is generated from complex set of metadata such as OpenAPI specification, the code generators stop being simple scripts.
Build-time code generation usually does not offer very good integration with the other code, since the generator does not have the knowledge of existing symbols.
For example, code generation is suitable for API clients, but it would be hard to apply it to generating getters and setters for a class.

> **Building a project** is usually not as simple as executing the compiler.
> The build process is executed by a build system and one of the build steps may be code generation.

In this work, we'll study the code generation. It is very useful to briefly investigate the other approaches as well, though.

As suggested by many internet discussions, for almost every problem with repetitive code, a choice of another programming language would eliminate it.
Choice of programming language is however a complicate decision -
It is very hard to undo during the project lifetime and it would often backfire on another problem that was easier in the original language.

Many programming languages support some kind of compile-time meta-programming to eliminate the issue with boilerplate code.
The capabilities differ greatly and we will explore them briefly in a following chapter (TODO: link?).
Compared to code generation, the meta-programming system usually is quite limited in capabilities, but offers much better integration with language.
The meta-programs are executed by the compiler and usually have at least some information about the existing symbols.
At the time of writing, C# does not have any system like this, but there is a prototype of [Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/)

Similar approach is to perform the meta-programming at runtime.
Platforms like .NET and JVM offer a rich reflection API that allows any program to explore existing types and their members.
It is even possible to create new methods, implement interfaces and override classes at runtime.
This approach is chosen by many .NET libraries to do serialization (TODO newtonsoft, Jint), dependency injections (TODO link MS extensions DI),  (TODO link Dapper, EF).
The limitation is, that we can not declare any new API during runtime, because the compiler has to know about it at compile time to allow the programmer to use it.
This limitation however does not exist in dynamically typed languages, which makes the technique even more powerful (and dangerous)

## Code generation abstraction

In basics, code generation is done very easily by printing out code fragments.
This approach works very well in simple cases, when the output space is fairly limited.
For example, a program to pre-generate a list of prime numbers for a hash function could be just a few lines.

As the space of possible outputs rises, a lot of complications may arise.
When the symbols names are variables, we have to sanitize them, since our target language likely does not support any string to be identifier.
We have to make sure, that there are no name collisions after the sanitization.
When referencing existing symbols we have to extra careful to reference the expected ones -
in C#, we could reference everything by full names to avoid problems.
Being overly explicit prevents issues, but afflicts readability of the generated code.

The goal of this project is to provide an abstraction for source code generators that will help to solve these issues.
The API user will provide a semantic model of the desired code and our library will translate it to a matching C# code.

Obviously, abstractions come with a cost.
We will sacrifice ease of use, since the API will force the user to be overly explicit about with type is used, which method is invoked, etc.
We will also sacrifice execution speed, as the library will perform checks and transformations on the model that are would unnecessary if we were emitting the code directly.
