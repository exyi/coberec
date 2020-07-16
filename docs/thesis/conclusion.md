# Conclusion

We have tested the Expression-based abstraction on the GraphQL Schema compiler which is also used in the project.
Thus, we can be quite sure that the library actually works and that the API is not painful to use.

The GraphQL Schema compiler creates a significant amount of symbols that may collide with each other.
It itself also does not contain any logic to prevent the collisions, it depends solely on the logic provided by the ExprCS abstraction.
We also have quite an extensive test suite for the name collisions, so this part of the project seems to be quite dependable.
On the other hand, it does not stress the expression translation too much -- all generated methods are fairly simple.

## Future Work

The library is certainly not feature-complete, there is still a number of C# features that can not be accessed by code generators using the Expression tree.
We have a near-complete list of them in [our GitHub project](https://github.com/exyi/coberec/issues).
These features can be however accessed through the [ILSpy Fallback API](./design.md#ilspy-fallback), so none of these issues should be a critical blocker.

We would like to adopt Coberec to work with the C# Source Generators, as we have mentioned.
It will likely only mean that we will implement ILSpy type system for the metadata exposed from the C# compiler.

It could be interesting to add support for translating our Expression into Linq Expressions to allow runtime code generation using the same API as build-time generation.
There are real use cases where having a single code generator for both would be an advantage.
For example, in DotVVM web framework, web pages are translated into a C# form at runtime.
It would be good to add support for build-time compilation while keeping the support runtime translation.
Both have their advantages -- build-time compilation would be more transparent and have lower startup, while runtime code generation is easier to setup and allows to rebuild without application restart.

