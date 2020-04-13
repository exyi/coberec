# Metadata representation

This chapter is about using and building symbols - descriptors of types, methods, properties, ... In `Coberec.ExprCS` we must be very explicit which symbol we want to use in code, so it's important to understand how it's handled.

## Loading symbols

In order to use a type or a type member in the code, we must get the descriptor somewhere. There is few different ways to  create the symbols (this applies to method, properties, types, ...)
* We can create it ourrself. The descriptor (as almost everything) is just a data class and we can fill the fields. However, for more complex scenarios, this is the hard way of doing things as it would get very noisy.
* Ask the `Coberec.ExprCS.MetadataContext` class to get a type. We must first create the context specifying the project dependencies and then we can load symbols from the dependencies. More specifically:
    * `context.FindTypeDef` gets a type signature from a name
    * `context.FindType` gets a type reference from a name
    * `context.GetTopLevelTypes` lists all available types. We can optionally specify which module (~= assembly) are we interested in
* When we have a type signature or reference, we can use it to list its members using the `Coberec.ExprCS.MetadataContext` class
    * `context.GetMemberMethods` returns list of all method references that are declared in the specified type. The search does not include inherited members.
    * `context.GetMemberMethodDefs` returns list of all method signatures that are declared in the specified type. The search does not include inherited members.
    * ...other kinds of members have similar methods
* We can also create the signatures and references from the System.Reflection classes:
    * `Coberec.ExprCS.TypeSignature.FromType`: System.Type -> type signature
    * TODO: other types


## References vs Signatures

You might (should?) be wondering what is the difference between signature and reference mentioned above. In System.Reflection, we have a `Type` that is used everywhere when we need to represent a type. Here, one the other hand, we split the use cases and in different cases there is different information in the object. When we want to represent a type as it was declared in C# (or `.dll`), we don't know which types are used as generic parameters and we are not interested in that (we are also not interested in arrays, and other types which are used but not defined). When we want to create a instance using `new` keyword, we need that information about the generic parameter so we can't use the same type as we got from type declaration.

Signatures represent what is written at the place of declaration. Most importantly, the generic parameters are not filled in, they are just parameters at this point.

References represent the symbol when it's used. At this point, it must contain the generic parameter. They may be substituted by type parameters owned by the declaring method/type. See the section Generic for more detail.

In case of types, special things like arrays, references, pointers and functions may be used in the reference context, so the type reference is a union of these cases. When it is just a type (with generic parameters), it is represented by `Coberec.ExprCS.SpecializedType`

When we use `Coberec.ExprCS.MetadataContext` to list all types, we get a list of `Coberec.ExprCS.TypeSignature`s. We can't use them directly in the generated code (for example, we can't declare variable of type `List<>`), so we need to convert them to the references (represented by `Coberec.ExprCS.TypeReference` and `Coberec.ExprCS.SpecializedType`). There is a few way to do so:
* `Coberec.ExprCS.TypeSignature.Specialize(genericArgs)` method - we specify the generic arguments
* `Coberec.ExprCS.TypeSignature.SpecializeByItself()` method puts into the generic arguments themselves. This is like converting `List<>` to `List<T>`. It is only useful when we are working in the context, where these parameters are valid, otherwise the code will not compile. This is the case in the type itself (but not in derived classes).

There are two options for listing members of a type using `Coberec.ExprCS.MetadataContext` - either we want to list the signatures or the references.
* For methods, the `context.GetMemberMethods(SpecializedType type, params TypeReference[] typeArgs)` method requires us to specify the type arguments
* Other member types can't have type arguments, so we just need the `SpecializedType type` argument

## Defining types

When we want to define new type and let Coberec generate the C# code for it, we will first need to define its signature. We may choose a static method on `Coberec.ExprCS.TypeSignature` to help us with that. For example, `public interface MyInterface` in namespace `MyNamespace`:

```csharp
var ns = NamespaceSignature.Parse("MyNamespace");
var type = TypeSignature.Interface("MyInterface", ns, Accessibility.APublic);
```

Then, we have to create its members (see below). Then we can collect them in a `Coberec.ExprCS.TypeDef` instance. That's done by creating an empty `TypeDef` from the signature and then adding the members:

```csharp
var typeDefinition = TypeDef.Empty(type)
                     .AddMember(someMethod)
                     .AddMember(anotherMethod);
```

More details about defining all kinds of .NET types is on [Defining Types](defining-types.md) page.

## Defining methods

Methods are declared similarly to type - we first have to create the signature and then add the body to form a `Coberec.ExprCS.MethodDef`. Identity function for integers would look like:

```csharp
var methodSignature =
    MethodSignature.Static("MyMethod", declaringType: type, Accessibility.APublic, returnType: TypeSignature.Int32, new MethodParameter(TypeSignature.Int32, "a"));

var method = MethodDef.Create(methodSignature, parameter => parameter);
```

The method body is an `Coberec.ExprCS.Expression` instance, which is used to represent the code.

Note that interface and abstract method don't have the body, they are created using `InterfaceDef` helper:

```csharp
var method = MethodDef.InterfaceDef(methodSignature)
```

## Defining properties

Properties are basically just two methods - the getter and the setter. They are represented by `Coberec.ExprCS.PropertySignature` and `Coberec.ExprCS.PropertyDef`.

```csharp
var signature = PropertySignature.Create(name, declaringType, type, getter: Accessibility.APublic, setter: null, isStatic: false);
var getter = MethodDef.Create(signature.Getter, thisP => ...);
var property = new PropertyDef(signature, getter, setter: null);
```

Often, you'll just want to declare an [C# Auto-Implemented Property](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties). While this has to be declared in the full form that, there is a helper for that in `Coberec.ExprCS.PropertyBuilders`

```csharp
// public static int MyProperty { get; set; }
var (property, field) = PropertyBuilders.CreateAutoProperty(
    declaringType,
    name: "MyProperty",
    propertyType: TypeSignature.Int32,
    isReadonly: false,
    isStatic: true
)
```

## Defining fields

Fields are represented by `Coberec.ExprCS.FieldSignature` and `Coberec.ExprCS.FieldDef`. There is not much to say about fields:

```csharp
public readonly string 
var signature = new FieldSignature(
    declaringType,
    name: "F1",
    Accessibility.APublic,
    TypeSignature.String,
    isStatic: false,
    isReadonly: true
);
var definition = new FieldDef(signature);
```

## Generics

Type with parameters (such as `List<T>`) are the main reason for the distinction between signatures and references. The signature is a type without the type parameters while reference has all the parameters filled in.

### Using generic types

Using generic types should be quite easy, we just have to create a reference from a signature by filling in the type parameters.

```csharp
TypeSignature genericList = TypeSignature.FromType(typeof(List<>));
TypeReference listOfInt = genericList.Specialize(TypeReference.FromType(typeof(int)));
TypeReference listOfString = genericList.Specialize(TypeReference.FromType(typeof(string)));
```

It's very similar when working with fields, methods or properties. As you can see from the `MethodReference` definition, it not only contains it's type parameters, but also type parameters of it's declaring type:

```graphql
type MethodReference {
    signature: MethodSignature
    typeParameters: [TypeReference]
    methodParameters: [TypeReference]
}
```
We can also get the original signature without parameters from a reference by accessing the `Signature` property, which is quite helpful, since there are only helpers for getting `MethodReference` and not `MethodSignature`. For example, this is how we might use the method `ImmutableArray.Create<T>(T item)`:

```csharp
TypeReference myType = ...
MethodSignature createSgn = MethodReference.FromLambda(() => ImmutableArray.Create<int>(0)).Signature;
MethodReference createRef = createSgn.Specialize(myType);

Expression array = Expression.StaticMethodCall(createRef, someInstance);
```

> If you are wondering, the type parameters for declaring type of nested types are inlined in a single array. This is mostly done for simplicity, since generic types nested in generic types are not seen very often, but we may use as an excuse, that .NET does the same thing in assembly metadata, as we can [see on sharplab.io](https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEBLANgHwAEAmARgFgAoQgZgAIS6BhAHgEEA+Ogbyrv4b1GAERYAhLr0oCZdPgIC+VBUA) 

> To make life a bit simpler, there is a implicit conversion from `TypeSignature` to `TypeReference`. As you might have guessed now, it fails when there are any type parameters. So, since generics are not used very often in .NET, it makes most cases simpler. However, when working with generic, one has to be a bit careful to use `Specialize(...)` instead.

### Creating generic types

First, we need to create the generic parameters. To prevent collisions, in Coberec.ExprCS parameters are not identified only by name, but also a unique id. That is created for us when we invoke `GenericParameter.Create("T")`. Then, we'll create a new class signature with that parameter

```csharp
var paramT = GenericParameter.Create("T");
// namespace NS { public class MyContainer<T> }
var myContainerSgn = TypeSignature.Class(
    "MyContainer",
    NamespaceSignature.Parse("NS"),
    Accessibility.APublic,
    genericParameters: new [] { paramT }
);
```

We'll build a single element container, so let's add a property `public T Item { get; set; }`

```csharp
var (item_field, item_prop) = PropertyBuilders.CreateAutoProperty(
    myContainerSgn,
    name: "Item",
    propertyType: paramT,
    isReadOnly: false
);
```

Notice that we can use `paramT` as a type reference. This also means that we can specialize other generic types by our type parameter. Let's add a `List<T> ToList()` method.

```csharp
var listType = TypeSignature.FromType(typeof(List<>))
               .Specialize(paramT);
var toListSgn = MethodSignature.Instance(
    "ToList",
    myContainerSgn,
    Accessibility.APublic,
    returnType: listType
);
```

Notice that we have to specialize `List<T>` by our type parameter `T`. It would not work if it was specialized by `List`'s `T`, since they have different internal id. Then we'll create the body:

```csharp

var toListDef = MethodDef.Create(toListSgn, thisParam => {
    var resultVar = ParameterExpression.Create(listType, "result");
    var listCtor = MethodReference.FromLambda(() => new List<int>())
                   .Signature
                   .Specialize(paramT);
    var listAdd = MethodReference.FromLambda<List<int>>(l => l.Add(0))
                  .Signature
                  .Specialize(paramT);
    return Expression.LetIn(
        // result = new List<T>()
        resultVar, Expression.NewObject(listCtor),
        new [] {
            // result.Add(this.Item)
            resultVar.Read().CallMethod(listAdd,
                thisParam.Read().ReadField(item_field.Signature.SpecializeFromDeclaringType())
            )
        }.ToBlock(resultVar)
    );
});
```

Note that we had to use `SpecializeFromDeclaringType()` method on our field. That's because the field contains a generic parameter from the declaring type that we have to fill in, otherwise conversion to `FieldReference` would fail. This method fills the type parameter by itself, making `MyContainer<?>.Item` into `MyContainer<T>.Item`.

Similar issue would occur if we'd need to reference the type `MyContainer<T>`. Let's say we'd like to implement a `CopyFrom(MyContainer<T> other)` method:

```csharp
var copyFromSgn = MethodSignature.Instance(
    "CopyFrom",
    myContainerSgn,
    Accessibility.APublic,
    returnType: TypeSignature.Void,
    new MethodParameter(
        myContainerSgn.SpecializeByItself(),
        "other"
    )
);
```

In this case, the `myContainerSgn.SpecializeByItself()` is equivalent to `myContainerSgn.Specialize(paramT)`, which just turns `MyContainer<?>` into `MyContainer<T>`. Note that both `SpecializeByItself` and `SpecializeFromDeclaringType` only make sense to use when declaring contents of the referenced type. Otherwise, this will just fail later, since the type reference will be undefined.

For completeness, here is the `CopyFrom` body:

```csharp
var copyFromDef = MethodDef.Create(copyFromSgn, (thisParam, otherParam) => {
    var field = item_field.Signature.SpecializeFromDeclaringType();
    return thisParam.Read().AssignField(
        field,
        otherParam.Read().ReadField(field)
    );
});
```

To actually turn this into something useful, we'll create the `TypeDef` and add it to `MetadataContext`:

```csharp
var myContainerDef =
    TypeDef.Empty(myContainerSgn)
    .AddMember(item_field, item_prop, toListDef, copyFromDef);

cx.AddType(myContainerDef);
```
