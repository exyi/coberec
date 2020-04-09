# Creating objects

In .NET object is created primarily by calling it's constructor, which is actually very similar to [calling methods](calling-methods.md).

This simple example creates a `List<int>` with capacity 100:

```csharp
// in metadata MethodReference is just a method
MethodReference ctor = MethodReference.FromLambda(() => new List<int>(0));
Expression expr = Expression.NewObject(ctor, Expression.Constant(100));
```

### Structs

Structs are created in the same way as normal classes. Only difference is that you can also get the default value using `Coberec.ExprCS.Expression.Default`.

### Boxing

Boxing is when a struct or primitive type is put into a proper object so that it may be used in place of implemented interfaces or `object`. In ExprCS, this is done by using `ReferenceConversionExpression`.


```csharp
Expression time = something_of_type(typeof(TimeSpan));

// convert to object
time.Box()
 // convert to IEquatable<TimeSpan>
time.ReferenceConvert(TypeReference.FromType(typeof(IEquatable<TimeSpan>)))
```

> The same methods also works for reference types, but in that case it's not creating the boxed object, just treating reference type differently

### Arrays

There are  `Coberec.ExprCS.Expression.NewArray` and `Coberec.ExprCS.ExpressionFactory.MakeArray` methods. See [arrays.md] for more details.
