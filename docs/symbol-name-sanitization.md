# Symbol Name Sanitization

When generating code with large API surface, naming the created types, methods and properties might be a major pain.
One has to be very careful not to emit invalid identifiers and thus strip all invalid characters.
Also, the symbol casing should be adjusted so it conforms to C# code standard (to `PascalCase`, usually).
This may however lead to name conflicts, which can sometimes be very tedious to avoid (see "Member name edge cases" section bellow)

### Options

In `Coberec.ExprCS.EmitSettings`, there are two option controlling the automatic sanitization:

`SanitizeSymbolNames` - this one it on, by default.
When enabled, Coberec will do it's best to adjust the symbol names are valid C# identifiers and that the names don't collide.
It should work in all possible cases (where "works" means that it produces a valid).
We still consider the name conflict to be edge cases and don't really attempt to find minimal possible number of renames,
we just append `2` (or other digit) to the colliding name. Invalid characters are usually replaced by an underscore, or removed if they are whitespace.
Also, we try to preserve the original names, but there might be some false positives in the filter. If that's a problem, this option can be disabled.

`AdjustCasing` - this one it off, by default.
Not only will it fix invalid identifiers, it will also convert camelCase into PascalCase, as it's usually expected in C#.
Methods, properties, public fields and types are always converted to PascalCase. Parameters, variables and private fields are converted to camelCase.

### External symbols

This renaming only applies to types and methods defined in the generated code. When a symbol that can't be represented in C# is defined in a referenced assembly, there is not much that can be done about it, so Coberec just copies its name verbatim although it won't compile (the other assembly might be written in F#, for example).

Sometimes, there is a possibility to transform a direct invocation to reflection invocation, but Coberec does not do that for you. It won't help in all cases anyway, specifically in cases where such symbol would be used in a signature of a method, property, or field.

### Examples of handled cases

Suppose that we have both `AdjustCasing` and `SanitizeSymbolNames` enabled. Then declaration of such class:

```csharp
class A
{
    public int a { get; set; }
    public void B(int x);
    public string B(int y);
    public int B(string y);

    private int A(int y);
}
```

will be transformed to the following:

```csharp
class A
{
    // capitalized and renamed to prevent collision with the declaring type A
    public int A2 { get; set; }
    public void B(int x);
    // renamed to prevent collision with B(int) above
    public string B2(int y);
    // not renamed, this is a valid overload
    public int B(string y);
}
```

When there is a conflict of a private and public member, the public one will get a precedence. Otherwise, the order of definition decides.

> Note that collisions with keywords are not a problem, since in C# keywords [may be "escaped" by prefixing the identifier with @](https://github.com/dotnet/csharplang/blob/master/spec/lexical-structure.md#identifiers)

<!-- TODO: lock files -->

### Member name edge cases

Name collisions are not only about not having two properties with the same name.
It can get quite complex, when you take overrides and interface implementations into the play.
This section describes some of these cases and how they are handled in Coberec.

There is no way to avoid conflict with override methods, since they must have the same name. For example in

```csharp
class A {
    public virtual void X();
}
class B: A {
    public override void X();
    public string X();
}
```

the `override void X()` will get precedence and the other method will be renamed.
Since .NET does not support multiple inheritance, this approach should always successfully avoid the conflicts of member names.
As also shown above, in C# a type can not contain a member of the same name, so there may still occur a conflict with the type name:

```csharp
class A {
    public virtual void B() { }
}
class B: A {
    // this member can't be here, since B is the type name
    public override void B() { }
}
```

[See on sharplab.io](https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEBLANgHwAEAmARgFgAoEgAgEEaBvKm1mwgZhoDdsoMArgENc7FDQBCACgCUTGgF8qS6sUkh6TFm040I3GFCjYAJjDGTZ8lQqA===) that it's really not valid C#.
I'm not aware of any way to override that method `B` inside of type `B`, except for renaming the type to something else.
This means, that in case a type contains a override of the same name, the type will be renamed:

```csharp
class A {
    public virtual void B() { }
}
class B2: A {
    // this member can't be here, since B is the type name
    public override void B() { }
}
```

Similar issue may occur with interface implementations, but there is a possibility to [implement the method or property explicitly](https://github.com/dotnet/csharplang/blob/master/spec/interfaces.md#explicit-interface-member-implementations), which does not have the naming issues. For example, this code would also not compile:
 
```csharp
interface A {
    void B();
}
class B: A {
    public void B() { }
}
```

However, we don't have to rename the type `B`, we can rename the method `void B()` and add an explicit implementation which does not cause any name collisions.

```csharp
interface A {
    void B();
}
class B: A {
    public void B2() { }
    void A.B() {
        this.B2();
    }
}
```

Very similar problem are collisions with built-in .NET method like `GetHashCode`, `ToString`, or `Finalize`. It would not be possible to override those method in a type with the same name, and it would most likely confuse most C# developers quite a bit, so these names are forbidden for types.

<!-- TODO: tests & link from here to all these claims -->
