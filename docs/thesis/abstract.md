We present a library for writing C# code generators.
It is designed to handle the edge cases that arise when code is automatically generated in a programming language designed more for humans than for computer programs.
The library automatically avoids all kinds of name collisions while keeping the generated code clean of too much explicitness.
Code generation is a common approach to reduce the amount of repetitive typing programmers have to do.
However, many code generators run into the edge cases fairly quickly, making the approach seem unreliable.
Handling it, on the other hand, may significantly complicate the code generator, or clutter the created program.
While our API is not as easy to use as simple templating engines are, we claim to have the solution to the issue of unreliability.
