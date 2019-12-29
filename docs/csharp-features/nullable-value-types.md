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
