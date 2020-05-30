using Coberec.CoreLib;
using System;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public abstract class Expression : ITraversableObject, IEquatable<Expression>
	{
		public sealed class ConstantCase : Expression
		{
			public string Item {
				get;
			}

			public sealed override string CaseName => "Constant";

			public sealed override object RawItem => Item;

			public ConstantCase(string item)
			{
				Item = item;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public override T Match<T>(Func<string, T> constant, Func<string, T> constantExpression)
			{
				return constant(Item);
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
		}

		public sealed class ConstantExpressionCase : Expression
		{
			public string Item {
				get;
			}

			public sealed override string CaseName => "ConstantExpression";

			public sealed override object RawItem => Item;

			public ConstantExpressionCase(string item)
			{
				Item = item;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public override T Match<T>(Func<string, T> constant, Func<string, T> constantExpression)
			{
				return constantExpression(Item);
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
		}

		public abstract string CaseName {
			get;
		}

		public abstract object RawItem {
			get;
		}

		int ITraversableObject.PropertyCount => 1;

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create(CaseName);

		public abstract T Match<T>(Func<string, T> constant, Func<string, T> constantExpression);

		public abstract override int GetHashCode();

		object ITraversableObject.GetValue(int propIndex)
		{
			return RawItem;
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

