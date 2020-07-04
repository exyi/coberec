# API overview

In this chapter, we will briefly show how is the library used.
We want to familiarize the reader with the usage before we discuss more implementation details.
For more complete guides and examples, please see the [documentation](https://github.com/exyi/coberec/).

## Hello world

First, we will show a simple program, that creates a program that prints "Hello world!".
Even though the result is elementary, we will see the boilerplate that is needed to initialize the source code generator.


```csharp
// First, we declare the symbol signatures:

// namespace MyApp.HelloWorld {
var ns = NamespaceSignature.Parse("MyApp.HelloWorld");
// public class Program {
var programType = TypeSignature.Class("Program", ns, Accessibility.APublic);
// public static int Main() {
var mainMethod = MethodSignature.Static("Main", programType, Accessibility.APublic, returnType: TypeSignature.Int32);

// get the Console.WriteLine reference
var writeLineRef = MethodReference.FromLambda(() => Console.WriteLine(""));

// then we build the actual expression tree
var body = Expression.Block(
    new [] {
        // we invoke the WriteLine method
        Expression.StaticMethodCall(writeLineRef, Expression.Constant("Hello world!"))
    },
    // and return 0
    result: Expression.Constant(0)
);

// after all, we just add the method with the body into the type
var type = TypeDef.Empty(programType).AddMember(
    MethodDef.Create(mainMethod, body)
);

// create a default context
var cx = MetadataContext.Create();
cx.AddType(type);
// and produce a string with the output.
var csharp = cx.EmitToString();
```

First, we need to create the signatures of the symbols we plan to declare, and we need to create a MethodReference to the `Console.WriteLine` method.
For convenience, we can create the reference from a C# lambda function - otherwise, we could create it from a MethodInfo (from Reflection) or find the method in the MetadataContext.
The method body is fairly simple, we only call the method and return 0, so we create a block with the two expressions.

To format it into a string, we need a [MetadataContext](./design.md#metadata-context) - the class that holds information about all symbols in the referenced assemblies and the symbols we have declared.
We do not need any references beyond the standard library, so we do not have to specify any parameters; otherwise, we could pass an array of references into the Create method.
After all, we call the `EmitToString` method, which finally invokes the entire machinery including ILSpy and produces the code:


```csharp
using System;

namespace MyApp.HelloWorld
{
    public class Program
    {
        public static int Main()
        {
            Console.WriteLine("Hello world!");
            return 0;
        }
    }
}
```

## Expression API

The core of our API is the `Expression` class.
It incorporates the core model and some helper methods to construct the model more conveniently.
We discussed how we are going to represent more complex code figures in the Design chapter.
For completeness and clarity, let us skim over the basic C# constructs.
The table below shows how the Expression represents various C# code fragments.

| C# | ExprCS Expression
|-----|-----|
| `1` | `Expression.Constant(1)`
| `"abc"` | `Expression.Constant("abc")`
| `a + b` | `Expression.Binary("+", a, b)`
| `a * b` | `Expression.Binary("*", a, b)`
| `a >> b` | `Expression.Binary(">>", a, b)`
| `a & b` | `Expression.Binary("&", a, b)`
| `a && b` | `Expression.And(a, b)`
| <code>a &#124;&#124; b</code> | `Expression.Or(a, b)`
| `!a` | `a.Not()`
| `a ? b : c` | `Expression.Conditional(a, b, c)`
| `if (a) { ...}` | `Expression.IfThen(a, ...)`
| `a ?? b` | `a.NullCoalesce(b)`
| `a is null` | `a.IsNull()`
| `default(T)` | `Expression.Default(typeT)`
| `MyClass.Method(a)` | `Expression.StaticMethodCall(myMethodReference, a)`
| `a.Method(b)` | `a.CallMethod(myMethodReference, b)`
| `a.MyProperty` | `a.ReadProperty(myPropertyReference)`
| `MyClass.MyProperty` | `Expression.ReadStaticProperty(myPropertyReference)`
| `a.MyField` (read reference) | `a.AccessField(myFieldReference)`
| `a.MyField` (read value) | `a.ReadField(myFieldReference)`
| `a.MyField = b` | `a.AssignField(myPropertyReference, b)`
| `new Abc(a)` | `Expression.NewObject(construtorReference, a)`
| `new [] { a, b, c }` | `ExpressionFactory.MakeArray(a, b, c)`
| `new T[x]` | `Expression.NewArray(elementType, x)`
| `a[x]` (read reference) | `Expression.ArrayIndex(a, x)`
| `{ A1; A2; ... return X }` (block) | `Expression.Block(A, result: X)`
| `{ A1; A2; ... return X }` (block) | `Expression.Block(A, result: X)`
| `new T?(x)` | `ExpressionFactory.Nullable_Create(x)`
| `x.Value` (when `x: Nullable<T>`) | `ExpressionFactory.Nullable_Value(x)`
| `"abc" + x` | `ExpressionFactory.String_Concat(Expression.Constant("abc"), x)`
| `Type a = value; rest...` | `Expression.LetIn(ParameterExpression.Create(Type, "a"), value, rest)`
| `myFunction(a)` (for functions) | `myFunction.Invoke(a)`
| `a => a` (create function) | `Expression.Function(aParameter.Read(), aParameter)`
| `(Func<int, int>)(a => a)` | `theLambda.FunctionConvert(TypeReference.FromType(typeof(Func<int, int>)))`
| `a += b` | `a.Ref().ReferenceCompoundAssign("+", b)`
| `a.Property += b` | `a.PropertyCompoundAssign(property, "+", b)`
| `a.field += b` | `a.FieldCompoundAssign(field, "+", b)`

> By far, not all C# constructs are in the table.
> It is possible to express some using these basic components.
> Unfortunately, some constructs can not be expressed this API.
> In such case, users may fall back to using the ILSpy tree directly, as we have shown in the [ILSpy Fallback](./design.md#ilspy-fallback) chapter.

More detailed documentation is available in the attachment or [on the web](https://github.com/exyi/coberec/blob/master/docs/csharp-features/cheatsheet.md).

## Metadata definitions

The second aspect of C# code is the types, methods, properties and fields.
We have designed a broad API for defining symbols in the rich .NET type system.

Namespaces are easy to declare, we can just call `NamespaceSignature.Parse("MyNamespace")`.

### Types

First, we declare a few variables for brevity.

```csharp
var ns = NamespaceSignature.Parse("MyNamespace");
// also public accessibility, for brevity
var @public = Accessibility.APublic;
```

| C# | Coberec.ExprCS
|-----|-----|
| `internal class C` | `TypeSignature.Class("C", ns, Accessibility.AInternal)`
| `public class C` | `TypeSignature.Class("C", ns, @public)`
| `public abstract class C` | `TypeSignature.Class("C", ns, @public, isAbstract: true)`
| `public sealed class C` | `TypeSignature.SealedClass("C", ns, @public)`
| `public static class C` | `TypeSignature.StaticClass("C", ns, @public)`
| `public struct C` | `TypeSignature.Struct("C", ns, @public)`
| `public interface C` | `TypeSignature.Interface("C", ns, @public)`

All of these helper methods have an additional argument for a list of generic type parameter.
A class `public class GenericClass<T>` may be declared as follows.

```csharp
var paramT = GenericParameter.Create("T");
var genericClass = TypeSignature.Class("GenericClass", ns, @public, genericParameters: new [] { paramT })
```

Interface implementations and base types are not specified in the signature, but the `TypeDef`.
The following example shows how to declare a derived class and an interface.

```csharp

var derivedClass =
    TypeDef.Empty(
        TypeSignature.Class("Derived", ns, @public),
        extends: justClass.Specialize()
    );

var classImplementingInterface =
    TypeDef.Empty(TypeSignature.Class("Implements", ns, @public))
            .AddImplements(myInterface.Specialize());
```

To add contents to the TypeDef, we may use the `.AddMember(memberDefinition)` method.

### Methods

For brevity, we will have a variable with declaring type and public accessibility.

```csharp
var @public = Accessibility.APublic;
var declType = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), @public, isAbstract: true);
```

| C# | Coberec.ExprCS
|-----|-----|
| `public void M()` | `MethodSignature.Instance("M", declType, @public, returnType: TypeSignature.Void)`
| `public static void M()` | `MethodSignature.Static("M", declType, @public, returnType: TypeSignature.Void)`
| `public abstract void M()` | `MethodSignature.Abstract("M", declType, @public, returnType: TypeSignature.Void)`
| `public virtual void M()` | `MethodSignature.Virtual("M", declType, @public, returnType: TypeSignature.Void)`
| `public override void M()` | `MethodSignature.Override(declType, overridenMethod)`
| `public override string ToString()` | `MethodSignature.Override(declaringType, MethodSignature.Object_ToString)`

All method may have parameters and generic type parameters:

```csharp
// public void MethodWithParams(string p1, ref double byReferenceParameter, int withDefaultValue = 0)
var parameters = new [] {
    new MethodParameter(TypeSignature.String, "p1"),
    new MethodParameter(TypeReference.ByReferenceType(TypeSignature.Double), "byReferenceParameter"),
    new MethodParameter(TypeSignature.Int32, "withDefaultValue").WithDefault(0)
};
var methodWithParameters =
    MethodSignature.Instance("MethodWithParams", declType, @public, returnType: TypeSignature.Void, parameters);

// public T GenericMethod<T>()
var paramT = GenericParameter.Create("T");
var genericMethod =
    MethodSignature.Static("GenericMethod", declType, @public, returnType: paramT, new [] { paramT });
```

After we declare the method signature, we will need to add a body and create a MethodDef.
The body must have a way to reference the method parameters through the ParameterExpression.
For this reason, MethodDef contains the list of ParameterExpression, although it is a bit redundant with the list of parameters in the signature.
The ParameterExpression assign every parameter an id by which we can reference it from the body expression.

```gql
type MethodDef {
    signature: MethodSignature
    argumentParams: [ParameterExpression]
    body: Expression?
    implements: [MethodReference]
}
```

> Note that the MethodDef also contains a list of implemented methods.
> The `implements` field is there to enable explicit interface implementations.

To implement an interface method or override a method from the base type, it would not make sense to specify all the metadata again.
We have added a helper method MethodDef.Override that copies the return type, argument and name from the base method.

Since it would be a bit noisy to declare the parameters twice, we have a set of helper methods that make the method declarations less cumbersome.
The method `MethodDef.CreateWithArray` gets a signature, creates the ParameterExpression list and calls a lambda to create the body expression.
In instance (not static) methods, the first argument is `this` - the object on which we call the method.
In the following example, we call another instance method while passing it all parameters as we got them:

```csharp
var definition = MethodDef.CreateWithArray(
    methodWithParameters,
    args => args[0].Read().CallMethod(anotherInstanceMethod, args.Skip(1))
)
```

In case we have a fixed number of parameters, we can use an overload of the Create method.
Instead of having the arguments in an array, we get them in separate arguments to the lambda function.
For example, we could declare a static method for number addition in the following way:

```csharp
var definition = MethodDef.CreateWithArray(
    methodWithParameters,
    (argA, argB) => Expression.Binary("+", argA, argB)
)
```

### Fields

Declaring fields is relatively simple, fields are mostly a pair of the name and a type.

| C# | Coberec.ExprCS
|-----|-----|
| `public readonly int F` | `FieldSignature.Instance("F", declType, @public, TypeSignature.Int32)` |
| `public int F` | `FieldSignature.Instance("F", declType, @public, TypeSignature.Int32, isReadonly: false)` |
| `public static readonly int F` | `FieldSignature.Static("F", declType, @public, TypeSignature.Int32)` |
| `public static int F` | `FieldSignature.Static("F", declType, @public, TypeSignature.Int32, isReadonly: false)` |

Field definition does not have any other info about the type, except for a documentation comment.
It is created simply by calling the constructor: `new FieldDef(signature)`

### Properties

.NET property is basically a pair of methods - the getter and the setter.
Both of these are optional, properties without a setter are quite common while the ones without a getter are rare.
Property signatures and definitions are created from the two methods, but we have a helper method prepared, that declares the method and property at the same time.

| C# | Coberec.ExprCS
|-----|-----|
| `public int P { get { } }` | `PropertySignature.Instance("P", declType, TypeSignature.Int32, getter: @public, setter: null)` |
| `public int P { set { } }` | `PropertySignature.Instance("P", declType, TypeSignature.Int32, getter: null, setter: @public)` |
| `public int P { get { } private set { } }` | `PropertySignature.Instance("P", declType, TypeSignature.Int32, getter: @public, setter: Accessibility.APrivate)` |
| `public static int P { get { } }` | `PropertySignature.Static("P", declType, TypeSignature.Int32, getter: @public, setter: null)` |
| `public static int P { get { } set { } }` | `PropertySignature.Static("P", declType, TypeSignature.Int32, getter: @public, setter: @public)` |
| `public abstract int P { get { } }` | `PropertySignature.Abstract("P", declType, TypeSignature.Int32, getter: @public)` |
| `public override int P { ... }` | `PropertySignature.Override(declType, overriddenPropertySignature)` |

Many properties in C# programs are the [automatically defined properties](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties).
We have a helper that defines it - it defines the backing field and the property and returns them in a tuple.
Both have to be added to the TypeDef.
For getter-only properties, the field is used to assign a value in the constructor.

| C# | Coberec.ExprCS
|-----|-----|
| `public int P { get; }` | `PropertyBuilders.CreateAutoProperty(declType, "P1", TypeSignature.Int32)` |
| `protected int P { get; }` | `PropertyBuilders.CreateAutoProperty(declType, "P2", TypeSignature.Int32, accessibility: Accessibility.AProtected)` |
| `public int P { get; set; }` | `PropertyBuilders.CreateAutoProperty(declType, "P3", TypeSignature.Int32, isReadOnly: false)` |
| `public static int { get; }` | `PropertyBuilders.CreateAutoProperty(declType, "P4", TypeSignature.Int32, isStatic: true)` |

### Documentation Comments

All member definitions have a `doccomment` field.
In C#, documentation comments must be valid XML, so the documentation comments in the metadata must conform.
We can either set the field while we create the definition, or use the `With(doccomment: ...)` method.

```csharp
var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
var td =
    TypeDef.Empty(type)
    .With(doccomment: new XmlComment("<summary> My type </summary>"));

var fieldSgn = FieldSignature.Instance("Field", type, Accessibility.AInternal, TypeSignature.Int32);
td = td.AddMember(
    new FieldDef(fieldSgn, new XmlComment("<summary> My field </summary>"))
);
```

More detailed documentation of the API may be found [in Github project](https://github.com/exyi/coberec/#complete-exprcs-documentation).


Next: [C# from GraphQL Schema Generator](./graphql-generator.md)
