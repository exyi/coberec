# Boolean Expressions

Boolean expression are mostly used as conditions for [if ... else expression](conditionals.md).

## Comparisons

Most conditions are just a usage of a binary operator. That can be created by `Coberec.Expression.Binary` method:

```csharp
Expression a = ...;
// a < 12
Expession condition = Expression.Binary("<", a, Expression.Constant(12));

// other C# operators are supported in a similar way
```

For null checks, there are helpers to make the code less verbose and work for both reference types and `Nullable<T>`:

```csharp
Expression a = ...;

// a == null
a.IsNull()

// a != null
a.IsNull().Not()
```

Note that `BinaryExpression` only supports native operators on primitive, not user-defined operators. To invoke a user defined operator, you must call the appropriate method (static method called `op_Something`). This also mean, that `==` and `!=` always do reference comparison for non-primitive types.

## Logical operators

The main combinators for boolean expression are `&&` and `||` operators. In C# these operators are "short circuit", which mean that the right operand is evaluated only when needed. For this reason, these operators are not represented by `BinaryExpression`, but as `ConditionalExpression`. To create `And` / `Or` expression, there are helpers `Coberec.ExprCS.Expression.And` and `Coberec.ExprCS.Expression.Or`


```csharp

// a > 10 && a <= 20

Expression.And(
    Expression.Binary(">", a, Expression.Constant(10)),
    Expression.Binary("<=", a, Expression.Constant(20)),
)

```
