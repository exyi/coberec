namespace MyNamespace
{
	public abstract class BaseC
	{
		public abstract bool P1 {
			get;
		}

		protected virtual object M1()
		{
			return null;
		}

		public abstract U0 M2<U0, U1>(U1 a);
	}
	public sealed class C : BaseC
	{
		public sealed override bool P12 => default(bool);

		protected override object M1()
		{
			return (object)(object)1;
		}

		public override U0 M2<U0, U1>(U1 a)
		{
			return default(U0);
		}
	}
}

