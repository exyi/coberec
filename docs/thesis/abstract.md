This work presents a library for implementing robust generators of C# code.
Existing code generators often generate invalid code for some inputs.
Issues such as name collisions reduce the reliability of code generation.
Programmers are then forced to handle these cases manually, which breaks build pipelines and lowers productivity.
Our library solves these issues.
It automatically avoids name collisions, and keeps the generated code clean and human-readable.
We compare our approach to other solutions such as reflection-based metaprogramming, macros, intermediate language rewriting and F# Type Providers.
<!-- While our API is not as easy to use as templating engines are, we claim to have the solution to the issue of unreliability. -->
