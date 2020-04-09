namespace NS
{
	public class C
	{
		public static int M()
		{
			return f(a: true) + f(a: false);
			int f(bool a)
			{
				return a ? 1 : 2;
			}
		}
	}
}

