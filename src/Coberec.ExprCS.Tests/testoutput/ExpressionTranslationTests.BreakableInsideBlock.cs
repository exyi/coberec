using System.Threading;

namespace NS
{
	public class C
	{
		public static int M()
		{
			int.Parse("123456789");
			if (Thread.CurrentThread.IsBackground)
			{
				goto IL_0009;
			}
			int.Parse("123456789");
			goto IL_0009;
			IL_0009:
			int.Parse("123456789");
			return 12;
		}
	}
}

