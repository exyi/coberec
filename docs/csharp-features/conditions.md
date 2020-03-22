# Conditions

Since everything is an expression, we only have one node to represent both `if (...) ... else ...` and the ternary operator expression `... ? ... : ...`. Any expression must return a value, but `void` is also a valid expression return type.


### Expression `if`s

The conditional expression (`Coberec.ExprCS.Expression.Conditional`) returns one of the branches, based on the condition value


```csharp
Expression myString = ...;
// equivalent to `myString is null ? "<empty>" : myString
Expression cond = Expression.Conditional(
    myString.IsNull(),
    Expression.Constant("<empty>"),
    myString
);
// will actually translate to `myString ?? "<empty>"`
```

### Statement ifs

If you want to create a conditional "statement" that does not return any value, you can just return void. To create an empty statement (to omit an else block, for example), you can use `Expression.Nop`.


```csharp
// if (a > 10) Console.WriteLine(a);

Expression cond2 = Expression.Conditional(
    Expression.Binary(">", a, Expression.Constant(10)),
    Expression.StaticMethodCall(
        MethodReference.FromLambda(() => Console.WriteLine(1)),
        a),
    Expression.Nop
);

```

if you want to omit the else block, you can also use `Expression.IfThen` as a shortcut.

When you will need more than one statement in the condition body, have a look at [blocks](blocks.md)

### Conditions

See a guide on [boolean expression](boolean-expressions.md) for more details. TLDR: you can use `Expression.And` and `Expression.Or` for the short-circuiting `&&` and `||` operators. `Expression.Binary` shall be used for the all other binary operators (bitwise and, or, xor).
