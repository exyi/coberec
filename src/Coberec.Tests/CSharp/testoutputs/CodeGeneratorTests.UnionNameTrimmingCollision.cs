using System;

namespace GeneratedProject.ModelNamespace
{
	public abstract class Expression : IEquatable<Expression>
	{
		public sealed class ConstantCase : Expression
		{
			public string Item {
				get;
			}

			public ConstantCase(string item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(Expression b)
			{
				ConstantCase constantCase;
				return (object)(constantCase = (b as ConstantCase)) != null && Item == ((ConstantCase)b).Item;
			}

			public override TResult Match<TResult>(Func<ConstantCase, TResult> constant, Func<ConstantExpressionCase, TResult> constantExpression)
			{
				return constant(this);
			}
		}

		public sealed class ConstantExpressionCase : Expression
		{
			public string Item {
				get;
			}

			public ConstantExpressionCase(string item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(Expression b)
			{
				ConstantExpressionCase constantExpressionCase;
				return (object)(constantExpressionCase = (b as ConstantExpressionCase)) != null && Item == ((ConstantExpressionCase)b).Item;
			}

			public override TResult Match<TResult>(Func<ConstantCase, TResult> constant, Func<ConstantExpressionCase, TResult> constantExpression)
			{
				return constantExpression(this);
			}
		}

		private protected abstract bool EqualsCore(Expression b);

		public virtual bool Equals(Expression b)
		{
			return (object)this == b || EqualsCore(b);
		}

		public static bool operator ==(Expression a, Expression b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Expression a, Expression b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as Expression);
		}

		public abstract TResult Match<TResult>(Func<ConstantCase, TResult> constant, Func<ConstantExpressionCase, TResult> constantExpression);

		public static Expression Constant(string item)
		{
			return new ConstantCase(item);
		}

		public static Expression ConstantExpression(string item)
		{
			return new ConstantExpressionCase(item);
		}
	}
}

