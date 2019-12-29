using System;
using System.Collections.Immutable;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace NS
{
	public class C
	{
		public static DateTime M()
		{
			return DateTime.MinValue;
		}
	}
	public class D
	{
		public static OpCode M()
		{
			return OpCodes.Call;
		}
	}
	public class E
	{
		public static int M()
		{
			return Marshal.SystemDefaultCharSize;
		}
	}
	public class F
	{
		public static ImmutableList<int> M()
		{
			return ImmutableList<int>.Empty;
		}
	}
}

