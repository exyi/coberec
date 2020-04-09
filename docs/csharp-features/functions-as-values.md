# Functions as values

In .NET you can use delegates when you want to treat methods as values - you can create delegate from a method with matching signature, pass it around however you want and then you can call it by the `Invoke` method (or just using the parenthesis in C#). In Coberec, it's a slightly different (for reasons explained at the end of this page).

There is a special kind of type `Coberec.ExprCS.FunctionType` that represent a function that may be invoked using the `Coberec.ExprCS.InvokeExpression`. The `FunctionType` just contains information about its arguments and the return type:

```graphql
type FunctionType {
    params: [MethodParameter]
    resultType: TypeReference
}
```

The `InvokeExpression` will then expect an instance of the function and the expected arguments.

### Creating functions

Anonymous function is simply created by the `Coberec.ExprCS.Expression.Function`:

```csharp
// parameterless `() => 1`

Expression fn1 = Expression.Function(Expression.Constant(1));

// (bool a) => a ? 1 : 2

ParameterExpression pA = ParameterExpression.Create(TypeSignature.Boolean, "a");
Expression fn2 = Expression.Function(
    Expression.Conditional(pA, Expression.Constant(1), Expression.Constant(2)),
    pA
);
```

### Calling functions

Calling those functions is as simple as `myFunction.Invoke(argument1)`:

```csharp
Expression invocation = fn2.Invoke(Expression.Constant(true));
```

### Connecting with delegates

Since .NET uses delegates, there must be a way convert between Coberec functions and delegates. There is a `Coberec.ExprCS.FunctionConversionExpression` which handles exactly that - it can convert between compatible delegates and functions (even between two delegates, which is a bit annoying in C#). In the generated code, most of these conversion will be elided, so it won't affect the prettiness of the generated code.

In fact, the `FunctionType` may only be used for local variables and inside expressions, it can't be used in method signatures, in fields, ... The reason is simply that it's not a .NET concept, so everything has to be explicitly converted to delegates when passed across these boundaries.

You might want to return the `fn2` function declared above as `Func<bool, int>`:

```csharp
var func = TypeReference.FromType(typeof(Func<bool, int>));
Expression delegate2 = fn2.FunctionConvert(func);
```

> Note that you can convert FunctionType into matching delegate using the `Coberec.ExprCS.FunctionType.TryGetDelegate` method.


### Local Functions

Declaring a local function is as simple as creating function and assigning it into a variable. It won't be translated into a delegate, but a C# local function.

For example (using the `fn2` function declared above):

```csharp
ParameterExpression localFn2 = ParameterExpression.Create(fn2.Type(), "fn2");

// fn(true) + fn(false)
Expression myBody =
    Expression.Binary("+",
        localFn2.Read().Invoke(Expression.Constant(true)),
        localFn2.Read().Invoke(Expression.Constant(false))
    )
    .Where(localFn2, fn2) // declares the variable inside this expression
```

### Why?

What is the reason to use special `FunctionType` instead of just using delegates?

First, delegates a quite annoying to work with, since you always have to specify the type. This is ok in C#, but in Coberec you always have to explicit about types and the burden adds up. It feels more convenient to actually have native function, especially when working with local function, like you have seen in the demo above.

Second, it would not be possible to do it only using delegates, given the ExprCS architecture. All expressions and types are immutable, and thus can't contain itself. Since delegates are allowed to return themselves, this means that TypeSignature of a delegate can't contain its signature. `FunctionType` on the other hand contains the signature, so we can immediately check that argument and result types match.
