# C# -> Coberec cheatsheet

Simple mapping table on how to encode C# expression in Coberec expression tree.

## Expression constructs

| C# | Coberec.ExprCS | more details
|-----|-----|----|
| `1` | `Expression.Constant(1)`
| `"abc"` | `Expression.Constant("abc")`
| `a + b` | `Expression.Binary("+", a, b)`
| `a * b` | `Expression.Binary("*", a, b)`
| `a >> b` | `Expression.Binary(">>", a, b)`
| `a & b` | `Expression.Binary("&", a, b)`
| `a && b` | `Expression.And(a, b)` | [Boolean Expressions](boolean-expressions.md)
| <code>a &#124;&#124; b</code> | `Expression.Or(a, b)` | [Boolean Expressions](boolean-expressions.md)
| `!a` | `a.Not()` | [Boolean Expressions](boolean-expressions.md)
| `a ? b : c` | `Expression.Conditional(a, b, c)` | [Conditions](conditions.md)
| `if (a) { ...}` | `Expression.IfThen(a, ...)` | [Conditions](conditions.md)
| `a ?? b` | `a.NullCoalesce(b)`
| `a is null` | `a.IsNull()`
| `default(T)` | `Expression.Default(typeT)`
| `MyClass.Method(a)` | `Expression.StaticMethodCall(myMethodReference, a)` | [Calling Methods](calling-methods.md)
| `a.Method(b)` | `a.CallMethod(myMethodReference, b)` | [Calling Methods](calling-methods.md)
| `a.MyProperty` | `a.ReadProperty(myPropertyReference)` | [Accessing Properties](accessing-properties.md)
| `MyClass.MyProperty` | `Expression.ReadStaticProperty(myPropertyReference)` | [Accessing Properties](accessing-properties.md)
| `a.MyField` (read reference) | `a.AccessField(myFieldReference)` | [Accessing Properties](accessing-properties.md)
| `a.MyField` (read value) | `a.ReadField(myFieldReference)` | [Accessing Properties](accessing-properties.md)
| `a.MyField = b` | `a.AssignField(myPropertyReference, b)` | [Accessing Fields](accessing-fields.md)
| `new Abc(a)` | `Expression.NewObject(construtorReference, a)` | [Creating Objects](creating-objects.md)
| `new [] { a, b, c }` | `ExpressionFactory.MakeArray(a, b, c)` | [Arrays](arrays.md)
| `new T[x]` | `Expression.NewArray(elementType, x)` | [Arrays](arrays.md)
| `a[x]` (read reference) | `Expression.ArrayIndex(a, x)` | [Arrays](arrays.md)
| `{ A1; A2; ... return X }` (block) | `Expression.Block(A, result: X)` | [Blocks](blocks.md)
| `new T?(x)` | `ExpressionFactory.Nullable_Create(x)` | [Nullable value types](nullable-value-types.md)
| `x.Value` (when `x: Nullable<T>`) | `ExpressionFactory.Nullable_Value(x)` | [Nullable value types](nullable-value-types.md)
| `"abc" + x` | `ExpressionFactory.String_Concat(Expression.Constant("abc"), x)`
| `Type a = value; rest...` | `Expression.LetIn(ParameterExpression.Create(Type, "a"), value, rest)` | [Variables](./variables.md)
| `myFunction(a)` (for functions) | `myFunction.Invoke(a)`  | [Functions as Values](functions-as-values.md)
| `a => a` (create function) | `Expression.Function(aParameter.Read(), aParameter)` | [Functions as Values](functions-as-values.md)
| `(Func<int, int>)(a => a)` | `theLambda.FunctionConvert(TypeReference.FromType(typeof(Func<int, int>)))`  | [Functions as Values](functions-as-values.md)
| `a += b` | `a.Ref().ReferenceCompoundAssign("+", b)` | [References - Compound Assignment](ref-returns.md#compound-assignments)
| `a.Property += b` | `a.PropertyCompoundAssign(property, "+", b)` | [Accessing Properties](accessing-properties.md)
| `a.field += b` | `a.FieldCompoundAssign(field, "+", b)` | [Accessing Fields](accessing-fields.md)
