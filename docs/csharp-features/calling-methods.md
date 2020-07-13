# Calling methods

Most C# code just calls some methods and takes the data from one call to another. There are basically two kinds of method calls - static and instance. In any case, you'll need a `Coberec.ExprCS.MethodReference` that you want to call.

### Static methods

Static methods class are best created using `Coberec.ExprCS.Expression.StaticMethodCall` method:

```csharp
MethodReference myMethod = ...;
Expression call = Expression.StaticMethodCall(
    myMethod,
    // method arguments
    Expression.Constant("55")
);
```

### Instance methods

For invoking an instance method you'll need the instance - the invocation target. This should be just another expression on which you can use extension method `CallMethod`.

```csharp
Expression myTarget = ...;
MethodReference myMethod = ...;

Expression call = myTarget.CallMethod(
    myMethod,
    // method arguments:
    Expression.Constant(false)
)
```

Note that this `CallMethod` extension can also call static methods where the target is the first argument. This is very useful for calling extension methods in the same way as you'd call instance method (extension methods are just annotated static method)

### Getting the `MethodReference`

See [metadata.md](../metadata.md) for more detailed information.

Method reference is just a description of the method (including all type parameters, unlike in `MethodSignature`) and there is multiple ways to obtain the description.

If you are calling system function, the easiest way is probably to use the `MethodReference.FromLambda` helper:

```csharp
// produces `int.Parse("55")`
Expression.StaticMethodCall(
    MethodReference.FromLambda(() => int.Parse("")),
    Expression.Constant("55")
);
```

If you are calling your own function, you should know the `MethodSignature` that the method was declared by. If it does not have any generic parameters, you can just use the implicit conversion `MethodSignature -> MethodReference`. Otherwise, you'll have to call `Specialize` and fill in the generic args:

```csharp

// suppose I have declared a method like this
// `class C { public static void M<T>(T a) { ... } }`
MethodSignature myMethod = ...;

// now, I want to call the method with a parameter of type `int`
// C.M<int>(12345)
Expression.StaticMethodCall(
    myMethod.Specialize(TypeSignature.Int32),
    Expression.Constant(12345)
)
```

> See [Defining Methods](../metadata.md#defining-methods) for a complete guide how to declare such methods.


In case you'd like to call a method from a referenced library and you can not load it from the lambda, you can use `MetadataContext` to obtain it's reference. You can use the `Coberec.ExprCS.MetadataContext.GetMemberMethods` to list methods on a type and `Coberec.ExprCS.MetadataContext.FindType` to get the type reference.
