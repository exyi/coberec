# References - `ref` returns, ...

In .NET both ref returns and `ref` parameters (and `out`, `in`) are represented by "by ref" types. The type name ends with ampersand (`&`). In reflection, you can recognize the type by (`IsByRef` property)[https://docs.microsoft.com/en-us/dotnet/api/system.type.isbyref?view=netframework-4.8] and you can obtain the type by calling `Type.GetType("System.Int32&")`.

In Coberec.ExprCS this is quite similar, you will not have to care where to put the C# `ref` keyword, just which expression or variable is of reference type and when are these references dereferenced. The reference type is represented by `Coberec.ExprCS.ByReferenceType` (which is member of `TypeReference` union) and you can use it in parameters and returns types. It's usage in fields is limited to `ref struct`, otherwise it's invalid (see the (C# docs)[https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#ref-struct-types] for more info) <!-- TODO: tests&validation for this -->

### Creating reference

To create a reference you just apply the `Coberec.ExprCS.Expression.FieldAccess` or `Coberec.ExprCS.Expression.ArrayIndex`, they return reference by default.

```csharp
// parameter of type string[]
var a = ParameterExpression.Create(TypeReference.FromType(typeof(string[])), "a");
// expression "a[12]" returns a reference to the 13th element of a
// type of the expression is `string&`
var aRef = Expression.ArrayIndex(
    a,
    Expression.Constant(11)
)

```

### Reading reference value

To read reference value, you can just use `Coberec.ExprCS.Expression.Dereference` expression:

```csharp
// the expression a[12] that has the value of 13th element
// the of the expression is `string`
var aVal = Expression.Dereference(aRef);
```

or like this if your prefer the fluent syntax:

```csharp
var aVal = aRef.Dereference();
```

### Writing to references

To read reference value, you can just use `Coberec.ExprCS.Expression.ReferenceAssign` expression:

```csharp
var myValue = Expression.Constant("abc");
// the expression `a[12] = "abc"`
// the of the expression is `void`, you can't chain the assignment as you can in C-like languages
var aVal = Expression.ReferenceAssign(target: aRef, value: myValue);
```

or `aRef.ReferenceAssign(value: myValue)` if you prefer the fluent syntax.

### Reference passing

You can pass the reference as any other value (unlike in C#). For example, this C# method:

```csharp
public static ref int M(ref int r)
{
    return ref r;
}
```

will have parameter `ParameterExpression.Create(new ByReferenceType(TypeSignature.Int32), "r")` and body will be just the parameter:

```csharp
var refP = ParameterExpression.Create(new ByReferenceType(TypeSignature.Int32), "r");
Expression body = refP;
```
