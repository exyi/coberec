namespace NS
{
	public class C
	{
		public static int M()
		{
			return f(a: true) + f(a: false);
			extern int f(bool a);
		}
	}
}

