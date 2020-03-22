# Blocks

Blocks are a way to execute multiple expression in one expression. It essentially provides a way to use statements, but with the advantage that you can still embed the block into any other expression.

Because block is also an expression, it must return a value. When you don't want to return any, it's fine, you can just return `void` using the `Expression.Nop`.

So, block is a list of expression that will be executed in order (the `body`) and then the `result` will be computed and returned.


### Example

```csharp
Console.Write("Enter the output file path: ");
return Console.ReadLine();
```

Can be generated from as

```csharp
MethodReference writeLineM = MethodReference.FromLambda(() => Console.WriteLine(""));
MethodReference readLineM = MethodReference.FromLambda(() => Console.ReadLine());
Expression expr = Expression.Block(
    ImmutableArray.Create(
        Expression.StaticMethodCall(
            writeLineM,
            Expression.Constant("Enter the output file path: ")
        )
    ),
    Expression.StaticMethodCall(readLineM)
);
```

### Helpers

When you first have a sequence of expressions and want to create a block from that, you can use the `ToBlock` extension method:


```csharp
Enumerable.Range(1, 30)
.Select(i => $"Line {i}")
.Select(Expression.Constant)
.Select(a => Expression.StaticMethodCall(writeLineM, a))
.ToBlock()
```

The result is `Nop` by default, but you can of course change it.
