# Example: Automatic ToString implementation

In this example (or how-to guide), we'll show how to implement `ToString` method. It will got through all properties of the type and include them the resulting string. For example, this will be what our code will produce:

```csharp
class MyClass {
    public int A { get; }
    public string B { get; }


    public override string ToString()
    {
        return "MyClass { A = " + A + ", B = " + B + " }";
    }
}
```

The properties will be given - we'll get a `TypeDef` and just add the `ToString` method to it. By the way, this is simplified implementation of automatic `ToString`s from Coberec.GraphQL.

We'll be implement a method `TypeDef ImplementToString(TypeDef declaringType)`, you can have a look at the full [implementation in our test suite](TODO). This will be the skeleton:

```csharp

private TypeDef ImplementToString(TypeDef declaringType)
{
    var toStringSgn = MethodSignature.Override(declaringType.Signature, MethodSignature.Object_ToString);

    var toStringDef = MethodDef.Create(toStringSgn, @this => {
        // TODO: the body
    });

    return declaringType.AddMember(toStringDef);
}
```

First, we'll need a list of all property signatures we are interested in. For this example, we'll just take all properties that are not static. Also note that we are not including inherited properties:

```csharp
var properties =
    declaringType.Members
    .OfType<PropertyDef>()
    .Select(p => p.Signature)
    .Where(p => !p.IsStatic);
```

Then, we'll need to know how to format a specific property. Since `string.Concat` accepts objects, we can simply access the property and let `string.Concat` call the default `ToString` for us. We'll leave it separate function, since we'll show how to also support arrays bellow.

```csharp
Expression formatProperty(PropertySignature property)
{
    return @this.Read().ReadProperty(property);
}
```

Then, we'll implement a function that yields the string fragments that the result will be composed from:

```csharp
IEnumerable<Expression> stringFragments()
{
    yield return Expression.Constant(declaringType.Signature.Name);
    yield return Expression.Constant(" {");
    var first = true;
    foreach (var property in properties)
    {
        if (first)
            first = false;
        else
            yield return Expression.Constant(",");
        yield return Expression.Constant(" " + property.Name + " = ");
        yield return formatProperty(property);
    }
    yield return Expression.Constant(" }");
}
```

In the end, we'll just use the `Coberec.ExprCS.ExpressionFactory.String_Concat` helper to concatenate the string. Note that it will also collapse two subsequent literals, that we are emitting from the `stringFragments` function.

```csharp
var toStringDef = MethodDef.Create(toStringSgn, @this => {
    Expression formatProperty(PropertySignature property) { ... }
    IEnumerable<Expression> stringFragments() { ... }
    return ExpressionFactory.String_Concat(stringFragments());
});
```
