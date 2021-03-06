# Conclusion

We have implemented a library for C# code generators.
It offers an [expression based API for modeling the resulting code](./API-overview.md), somewhat similar to Linq Expressions from the .NET framework.

The tool is aiming at generating *COrrect, REadable and BEautiful Code*, with priority on the correctness -- thus the name Coberec.
The C# code emitter is based on the ILSpy decompiler, which makes sure that it produces readable code that always represents what was specified in the API.
C# is a complex language, and it would be tough to accomplish our goals without using ILSpy's backend.

We demonstrated usage of the Coberec code generation library by implementing the [GraphQL Schema compiler](./graphql-generator.md).
This compiler is used in the project itself and thus well tested.
We are confident that the library works well and that the API is not painful to use.

The GraphQL Schema compiler creates a significant number of symbols that may collide with each other.
It itself also does not contain any logic to prevent the collisions, it depends solely on the logic provided by the [abstraction](./design.md#symbol-renaming).
We also have quite an extensive test suite for the name collisions, so this part of the project seems to be quite dependable.
On the other hand, the GraphQL compiler does not stress the expression translation too much -- all generated methods are fairly simple.

## Future Work

The library is certainly not feature-complete, there is still a number of C# features that can not be accessed by code generators using the Expression tree.
We have a near-complete list of them in [our GitHub project](https://github.com/exyi/coberec/issues).
These features can be however accessed through the [ILSpy Fallback API](./design.md#ilspy-fallback), so none of these issues should be a critical blocker.

We would like to adopt Coberec to work with the C# 9 Source Generators, as we have mentioned.
It will likely only mean that we will implement ILSpy type system for the metadata exposed from the C# compiler.

It could be interesting to add support for translating our Expression tree into Linq Expressions.
It would allow both runtime and build-time code generation using a single API.
There are real use cases where having a single code generator with both backends would be an advantage.
For example, in DotVVM web framework, web pages are translated into a C# form at runtime.
It would be good to add support for build-time page compilation while keeping the support for runtime compilation.
Both have their advantages -- build-time compilation would be more transparent and lead to a lower startup time, while runtime code generation is easier to setup and allows to recompile a web page without restarting the application.

