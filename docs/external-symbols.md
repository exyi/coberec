# Loading external symbols

## References assemblies

The `Coberec.ExprCS` emitter must know everything about the symbols it treats, so if you want to use external libraries you must explicitly include them in the project. When the `MetadataContext` is created, you can fill in the parameter `references` with a list of paths to referenced libraries - see the `MetadataContext.Create` method

## Symbols in the generated project

Sometimes it is useful to generate code into a existing project that already contains some useful code. If you want to call into this code you can not add a assembly reference, since it is the same project you are just generating. In that case "external symbol API" should help.

The `Coberec.ExprCS.MetadataContext.AddType` has a `isExternal` parameter, when it is set to true, the added type will not be in the output, but everything will count with it. That means that any API from the project that is going to be used must be explicitly modelled.

```csharp
// public class MyClass
var signature = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
// public void MyMethod() { }
var myMethod =
    MethodDef.CreateWithArray(
        MethodSignature.Instance("MyMethod", signature, Accessibility.APublic, returnType: TypeSignature.Void),
        args => Expression.Nop
    );

var type = TypeDef.Empty(signature).AddMembers(myMethod);
cx.AddType(type, isExternal: true)
```

