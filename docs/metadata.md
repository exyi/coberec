# Metadata representation

This chapter is about using and building symbols - descriptors of types, methods, properties, ... In `Coberec.ExprCS` you must be very explicit which symbol you want to use in code, so it's important to understand how is it handled.


## Loading symbols

In order to use a type or a type member in the code, you must get the descriptor somewhere. There is few options of creating the symbols (this applies to method, properties, types or anything)
* You can create it yourself. The descriptor (as almost everything) is just a data class and you can fill the field yourself. However, for more complex scenarios, this is the hard way of doing things.
* Ask the `Coberec.ExprCS.MetadataContext` class to get a type. You must first create it while specifying the project dependencies and then you can load symbols from the dependencies. More specifically:
    * `context.FindTypeDef` gets a type signature from a name
    * `context.FindType` gets a type reference from a name
    * `context.GetTopLevelTypes` lists all available types. You can optionally specify which module (basically equivalent to assembly) you are interested in
* When you have a type, you can use it to list its members using the `Coberec.ExprCS.MetadataContext` class
    * `context.GetMemberMethods` returns list of all method references that are declared in the specified type. The search does not include inherited members.
    * `context.GetMemberMethodDefs` returns list of all method signatures that are declared in the specified type. The search does not include inherited members.
    * ...other types of members have similar methods
* You can also create the signatures and references from the System.Reflection classes:
    * `Coberec.ExprCS.TypeSignature.FromType`: System.Type -> type signature
    * TODO: other types


## References vs Signatures

You might (should?) be wondering what is the difference between signature and reference mentioned above. In System.Reflection, you have a `Type` that is used everywhere when you need to represent a type. Here, one the other hand, we split the use cases, because in different cases there is different information in the object. When you want to represent a type as it was declared in C# (or `.dll` file), you don't know which type is substituted for generic parameter and you are not interested in that (you are also not interested in arrays, references, ...). When you want to create a instance using `new` keyword, you need that information about the generic parameter so you can't use the same type as you got from type declaration.

Signatures represent what is written at the place of declaration. Most importantly, the generic parameters are not filled, they are just parameters at this point.

References represent the symbol when it is used. At this point, it must contain the generic parameter. They may be substituted by type parameters owned by the declaring method/type. See the section Generic for more detail.

In case of types, special things like arrays, references, pointers and functions may be usually used in the reference context, so the type reference is a union of these cases. When it is just a type (with generic parameters), it is represented by `Coberec.ExprCS.SpecializedType`

When you as `Coberec.ExprCS.MetadataContext` to list all types, you get a list of `Coberec.ExprCS.TypeSignature`s. You can't use them directly in the generated code (which makes sense, you can't declare variable of type `List<>`), so you need to convert them to the references (represented by `Coberec.ExprCS.TypeReference` and `Coberec.ExprCS.SpecializedType`). There is a few way to do so:
* `Coberec.ExprCS.TypeSignature.Specialize` method - you just specify the generic arguments
* `Coberec.ExprCS.TypeSignature.SpecializeByItself` method puts into the generic arguments themselves. This is like converting `List<>` to `List<T>`. It is only useful when you are working in the context, where these parameters are valid, otherwise the code will not compile. This is the case in the type itself (but not in derived classes!).

There are two options for listing members of a type using `Coberec.ExprCS.MetadataContext` - either you want to list the signatures or the references. Of course, for listing references you need to specify the generic arguments.
* For methods, the `context.GetMemberMethods(SpecializedType type, params TypeReference[] typeArgs)` method required you to specify them
* Other member types can not have generic args, so it is enough to specify type `SpecializedType type` argument

## Defining types

You probably want to define new type and let Coberec generate the C# code for it. First you need to create its signature. You may choose a static method on `Coberec.ExprCS.TypeSignature` to help you with that. For example, `public interface MyInterface` in namespace `MyNamespace` is declared like this:

```csharp
var ns = NamespaceSignature.Parse("MyNamespace");
var type = TypeSignature.Interface("MyInterface", ns, Accessibility.APublic);
```

Then, you have to create its members. When you have them, you can collect them in `Coberec.ExprCS.TypeDef` instance. That's best done by creating an empty `TypeDef` from the signature and then adding the members

```csharp
var typeDefinition = TypeDef.Empty(type)
                     .AddMember(someMethod)
                     .AddMember(anotherMethod);
```

## Defining methods

Methods are declared similarly to type - you first have to create the signature and then add the body to form a `Coberec.ExprCS.MethodDef`. Except that in case of a method, the body is expression. Identity function for ints would look like:

```csharp
var methodSignature =
    MethodSignature.Static("MyMethod", declaringType: type, Accessibility.APublic, returnType: TypeSignature.Int32, new MethodParameter(TypeSignature.Int32, "a"));

var method = MethodDef.Create(methodSignature, parameter => parameter);
```

Note that interface and abstract method don't have the body, they are created like this:

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

## Defining fields

Fields are represented by `Coberec.ExprCS.FieldSignature` and `Coberec.ExprCS.FieldDef`.

## Generics

TODO
