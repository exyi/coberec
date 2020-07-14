Code generators often generate invalid code for some inputs.
Issues such as name collisions reduce the reliability of code generation.
Programmers are forced to handle these cases manually, which may break build pipelines and lower productivity.
This work presents a library to simplify building reliable C# code generators.
The library automatically avoids all kinds of name collisions while keeping the generated code clean of too much explicitness.
While our API is not as easy to use as templating engines are, we claim to have the solution to the issue of unreliability.
