namespace MyNamespace
{
	public class MyType
	{
		public readonly string Equals2;
	}
	public class MyType2
	{
		public bool Equals()
		{
			return true;
		}
	}
	public class MyType3
	{
		public bool Equals2(object obj2)
		{
			return true;
		}
	}
	public class MyType4
	{
		public static bool Equals2(object obj2)
		{
			return true;
		}
	}
	public class MyType5
	{
		public override bool Equals(object obj2)
		{
			return true;
		}
	}
}

