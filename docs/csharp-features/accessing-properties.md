# Accessing properties

Properties in .NET are just thin wrappers for methods. We have `Coberec.ExprCS.PropertyReference`, but it's main purpose it to hold reference to its `Getter` and `Setter`. In the expression tree, there is no such node as property read, every action with properties is represented by method calls.

### Getting the accessors

You can obtain the accessor methods from a `PropertyReference` very easily - using the `Setter` and `Getter` methods

```csharp
PropertyReference myProperty = ...;

MethodReference getter = myProperty.Getter();
MethodReference setter = myProperty.Setter();

// then, you can call it like any other method
Expression getterCall = myInstance.CallMethod(getter);
```

Note that `Getter` and `Setter` may return null, if the corresponding method does not exist.

### Accessing properties

For little bit more convenience and better error handling, there are helpers for reading and writing property values:

```csharp
PropertyReference myProperty = ...;

// `instance.Property`
Expression value = myInstance.ReadProperty(myProperty);
// assign back to a second instance - `instance2.Property = instance.Property`
Expression assignment = myInstance2.AssignProperty(myProperty, value);

```

Or, if your property is static:

```csharp

// `DeclaringClass.Property`
Expression value = Expression.StaticPropertyRead(myProperty);
// `DeclaringClass.Property = 0`
Expression assignment = myInstance2.AssignProperty(myProperty, Expression.Constant(0));
```

> Unlike when working with fields, you can't have a reference to a property so there is no PropertyAccess method.

You can also use C# compound assignment (operators like `+=`, `&=`, ...) with properties

```csharp

// instance.Property += 100
Expression assignment = myInstance.PropertyCompoundAssign(myProperty, "+", Expression.Constant(100));
```

### Getting the `PropertyReference`

See [metadata.md](../metadata.md) for more detailed information.

Property reference is just a description of the property (including all type parameters, unlike in `PropertySignature`) and there is multiple ways to obtain the description.

If you are getting a property from standard library, the easiest way is probably to use the `PropertyReference.FromLambda` helper:

```csharp
// produces `"abc".Length`
Expression.Constant("abc").ReadProperty(
    PropertyReference.FromLambda<string>(s => s.Length)
);
```

If you are accessing your own property, you should know the `PropertySignature` that the property was declared by. If it does not have any generic parameters, you can just use the implicit conversion `PropertySignature -> PropertyReference`. Otherwise, you'll have to call `Specialize` and fill in the generic args:

```csharp

// suppose I have declared a property like this
// `class C<T> { public T Item { get; set; } }`
PropertySignature myProperty = ...;
Expression cInstance = ...;

// now, I want to read the property from C<int>
cInstance.ReadProperty(
    myProperty.Specialize(TypeSignature.Int32),
)
```

> See [Defining Properties](../metadata.md#defining-properties) for a complete guide how to declare such properties.


In case you'd like to get a property from a referenced library and you can not load it from the lambda, you can use `MetadataContext` to obtain it's reference. You can use the `Coberec.ExprCS.MetadataContext.GetMemberProperty` get it from a type and name. And `Coberec.ExprCS.MetadataContext.FindType` to get the type reference.
