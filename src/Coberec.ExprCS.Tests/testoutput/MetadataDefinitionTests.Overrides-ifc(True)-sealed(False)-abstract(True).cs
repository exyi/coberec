namespace MyNamespace
{
	public interface Base
	{
		bool P1 {
			get;
		}

		object M1();

		U0 M2<U0, U1>(U1 a);
	}
	public abstract class C : Base
	{
		public bool P1 => default(bool);

		public abstract object M1();

		public U0 M2<U0, U1>(U1 a)
		{
			return default(U0);
		}
	}
}

