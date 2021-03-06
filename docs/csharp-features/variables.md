# Variables

Variables are declared using the `Coberec.ExprCS.LetInExpression` for the scope of the inner expression. The variable itself is a `ParameterExpression`, which essentially contains its type and name

```csharp
var myVar = ParameterExpression.Create(TypeSignature.Int32, "myVar")

// int myVar = 42;
// return myVar + myVar;
return Expression.LetIn(
    myVar, Expression.Constant(42),
    target: Expression.Binary("+", myVar, myVar)
)
```

The variable is valid only inside of the `target` expression, any usage outside will cause an error.

> Note that the variables may be renamed, in case of a collision, even when `EmitSettings.SanitizeSymbolNames` is disabled.

### Mutating a variable

By default variables can't be mutated, but it's easily enabled by using the `CreateMutable` method. The reason is that when generating complex piece of code, many different components will use the same variables. Making it explicit that this variable may be mutated, makes mistakes less likely.

Variable assignment is done using the `variable.Assign(newValue)` method.

```csharp

var mutableVar = ParameterExpression.CreateMutable(TypeSignature.Int32, "mutableVar");

// var mutableVar = 42
// mutableVar +  { mutableVar = 32; return mutableVar; }

return Expression.LetIn(
    mutableVar, Expression.Constant(42),
    target: Expression.Binary("+",
        mutableVar,
        new [] {
            mutableVar.Assign(Expression.Constant(30))
        }.ToBlock(mutableVar)
    )
);
```

You can also see that variable assignment may be done in any subexpression, including the binary operator. It may not lead to the nicest possible code, but it works and in this case even produces pretty output.

### Method Parameters

Method parameters are basically the same things as variables, except that they are defined inside `Coberec.ExprCS.MethodDef`. Method parameters are also immutable by default, but you can change when you want.


Let's say we have the following declaring type and method:
```csharp
// public static TestClass
var declaringType = TypeSignature.StaticClass("TestClass", NamespaceSignature.Parse("NS"), Accessibility.APublic);
// public int M(int a)
var method = MethodSignature.Static(
    "M",
    declaringType,
    Accessibility.APublic,
    TypeSignature.Int32,
    new MethodParameter(TypeSignature.Int32, "a"));
```

And we want the body `a++; return a`:

```csharp
var argA = ParameterExpression.CreateMutable(TypeSignature.Int32, "a");
var body = new [] {
    argA.Assign(Expression.Binary("+", argA, Expression.Constant(1)))
}.ToBlock(result: argA);
```

Then we can just create new `MethodDef` from it:

```csharp
var methodDef = new MethodDef(method, new [] { argA }, body);
```

And add it to a type and register to `MetadataContext`:

```csharp
cx.AddType(TypeDef.Empty(declaringType).AddMember(methodDef));
```
