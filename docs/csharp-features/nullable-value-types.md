# Nullable value types

Nullable value types are just instances of a struct `System.Nullable<...>`, so you can work with it like you'd do with any struct - get its properties, create new instance, ... C# however offers syntactic sugar to make their handling more convenient and so does `Coberec.ExprCS`.


### Reading

When you have an expression of type `Nullable<X>` you can check if it's null or not using the `HasValue` property. There is a helper method `Coberec.ExprCS.ExpressionFactory.Nullable_HasValue` for that. Expression `a.HasValue` is equivalent to

```csharp
ExpressionFactory.Nullable_HasValue(a);
```

To get the value (or exception, if `null` is present), you can use the `Value` property, or our helper:

```csharp
ExpressionFactory.Nullable_Value(a);
```

### Constructing

To construct an instance of `Nullable<T>` of value null, it's enough to create a default value (since it's a struct) - `Expression.Default(TypeSignature.NullableOfT.Specialize(myType))`

To construct an instance with some value, you just [call the constructor](constructor.md). To save you some work, there is a helper prepared - `Coberec.ExprCS.ExpressionFactory.Nullable_Create` that just wraps the value in a `Nullable<T>`.

### Null Helpers

These helpers work the in same way for reference types and for `Nullable<T>`, which makes them quite useful for code that is null-agnostic:

#### `expr.IsNull()`

```csharp
Expression a = ...;

// `a == null` or `!a.HasValue`, based on the type of `a`
a.IsNull()
```

#### `expr.NullCoalesce(alternative)`

This method executes the alternative when expr is null, it's similar to C# `??` operator.
