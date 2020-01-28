# Accessing fields

Fields store the data in classes and may read and written to from C# code. When you want to access a field, you'll first need a `Coberec.ExprCS.FieldReference`, but it's main purpose it to hold reference to its `Getter` and `Setter`. In the expression tree, there is no such node as property read, every action with properties is represented by method calls.

### Accessing fields

There is one universal expression that performs field access, the `Coberec.ExprCS.Expression.FieldAccess`. It returns a [reference](TODO), which may be read or written to.

```csharp
FieldReference myProperty = ...;
Expression target = ...; // the instance, we'll be accessing

// `instance.field`
Expression value = target.AccessField(field).Dereference();
// assign back different value (a `default`)
Expression assignment = myInstance2.AccessField(field).ReferenceAssign(Expression.Default(field.ResultType()));

```

Or, if your field is static:

```csharp

// `DeclaringClass.Property`
Expression value = Expression.StaticFieldAccess(myProperty).Dereference();
```

You can also use helper methods `ReadField` and `AssignField` if you want to avoid working with references:

```csharp
Expression value = target.ReadField(field);
Expression assignment = target.AssignField(field, Expression.Default(field.ResultType()));
```

Note that the target might be a reference and if you are assigning to a struct field, the target should be a reference. Otherwise, the struct will be cloned before the value is assigned and the result dropped.

### Getting the `FieldReference`

See [metadata.md](../metadata.md) for more detailed information.

Field reference is just a description of the field (including all type parameters, unlike in `FieldSignature`) and there is multiple ways to obtain the description.

If you are getting a field from standard library, the easiest way is probably to use the `PropertyReference.FromLambda` helper:

```csharp
// produces `"abc".Item1`
myTuple.ReadField(
    PropertyReference.FromLambda<(string, int)>(s => s.Item1)
);
```

If you are accessing your own field, you should know the `FieldSignature` that the property was declared by. If it does not have any generic parameters, you can just use the implicit conversion `FieldSignature -> FieldReference`. Otherwise, you'll have to call `Specialize` and fill in the generic args:

```csharp

// suppose I have declared a field like this
// `class C<T> { public T item; }`
FieldSignature myField = ...;
Expression cInstance = ...;

// now, I want to read the field from C<int>
cInstance.ReadProperty(
    myField.Specialize(TypeSignature.Int32),
)
```

> See [Declaring Fields](declaring-fields.md) for a complete guide how to declare such properties (TODO)


In case you'd like to get a field from a referenced library and you can not load it from the lambda, you can use `MetadataContext` to obtain it's reference. You can use the `Coberec.ExprCS.MetadataContext.GetMemberField` get it from a type and name. And `Coberec.ExprCS.MetadataContext.FindType` to get the type reference.
