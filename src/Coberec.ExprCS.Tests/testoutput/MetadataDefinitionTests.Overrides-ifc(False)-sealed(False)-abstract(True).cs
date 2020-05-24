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
	public abstract class C : BaseC
	{
		public override bool P12 => default(bool);

		protected abstract override object M1();

		public override U0 M2<U0, U1>(U1 a)
		{
			return default(U0);
		}
	}
}

