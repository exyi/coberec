using System;

namespace NS
{
	public class MyClass
	{
		public string A { get; }

		public ValueTuple<bool, bool> B { get; }

		public override string ToString()
		{
			return string.Concat("MyClass { A = ", A, ", B = ", B, " }");
		}
	}
	public class SecondClass
	{
		public override string ToString()
		{
			return "SecondClass { }";
		}
	}
}

